using System.Text.Json;
using WorkLens.Core.Entities;
using WorkLens.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace WorkLens.Infrastructure.Services;

/// <summary>
/// Scores a batch of job listings against the active resume, reusing cached
/// <see cref="JobMatch"/> rows where one already exists so a feed page re-render
/// doesn't re-call OpenAI for jobs already scored. Called on-demand from the API
/// (not on every 7s feed poll) — see MatchController.
/// </summary>
public class ResumeMatchOrchestrator
{
    private readonly IResumeRepository _resumeRepo;
    private readonly IJobListingRepository _listingRepo;
    private readonly IJobMatchRepository _matchRepo;
    private readonly IResumeMatchingService _matchingService;
    private readonly ILogger<ResumeMatchOrchestrator> _logger;

    public ResumeMatchOrchestrator(
        IResumeRepository resumeRepo,
        IJobListingRepository listingRepo,
        IJobMatchRepository matchRepo,
        IResumeMatchingService matchingService,
        ILogger<ResumeMatchOrchestrator> logger)
    {
        _resumeRepo = resumeRepo;
        _listingRepo = listingRepo;
        _matchRepo = matchRepo;
        _matchingService = matchingService;
        _logger = logger;
    }

    /// <summary>Scores the given listing IDs against the active resume, skipping any already cached. Returns id -> JobMatch for all requested IDs that now have a score.</summary>
    public async Task<Dictionary<int, JobMatch>> ScoreListingsAsync(IReadOnlyList<int> jobListingIds, CancellationToken ct)
    {
        var resume = await _resumeRepo.GetActiveAsync(ct);
        if (resume is null) return new Dictionary<int, JobMatch>();

        var cached = await _matchRepo.GetForListingsAsync(resume.Id, jobListingIds, ct);
        var missingIds = jobListingIds.Where(id => !cached.ContainsKey(id)).ToList();

        foreach (var id in missingIds)
        {
            var listing = await _listingRepo.GetByIdAsync(id, ct);
            if (listing is null) continue;

            try
            {
                var result = await _matchingService.ScoreAsync(resume, listing, ct);
                var match = new JobMatch
                {
                    ResumeId = resume.Id,
                    JobListingId = listing.Id,
                    MatchScore = result.Score,
                    MatchingSkillsJson = JsonSerializer.Serialize(result.MatchingSkills),
                    MissingSkillsJson = JsonSerializer.Serialize(result.MissingSkills),
                    Summary = result.Summary,
                    ScoredAt = DateTimeOffset.UtcNow
                };
                await _matchRepo.UpsertAsync(match, ct);
                cached[id] = match;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to score job listing {Id} against resume {ResumeId}", id, resume.Id);
            }
        }

        await _matchRepo.SaveChangesAsync(ct);
        return cached;
    }

    public async Task<bool> HasActiveResumeAsync(CancellationToken ct) => await _resumeRepo.GetActiveAsync(ct) is not null;
}
