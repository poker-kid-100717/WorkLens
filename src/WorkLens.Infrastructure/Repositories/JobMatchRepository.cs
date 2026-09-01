using WorkLens.Core.Entities;
using WorkLens.Core.Interfaces;
using WorkLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace WorkLens.Infrastructure.Repositories;

public class JobMatchRepository : IJobMatchRepository
{
    private readonly WorkLensDbContext _db;

    public JobMatchRepository(WorkLensDbContext db) => _db = db;

    public Task<JobMatch?> FindAsync(int resumeId, int jobListingId, CancellationToken ct) =>
        _db.JobMatches.FirstOrDefaultAsync(m => m.ResumeId == resumeId && m.JobListingId == jobListingId, ct);

    public async Task<Dictionary<int, JobMatch>> GetForListingsAsync(int resumeId, IReadOnlyList<int> jobListingIds, CancellationToken ct)
    {
        var matches = await _db.JobMatches
            .Where(m => m.ResumeId == resumeId && jobListingIds.Contains(m.JobListingId))
            .ToListAsync(ct);

        return matches.ToDictionary(m => m.JobListingId);
    }

    public async Task UpsertAsync(JobMatch match, CancellationToken ct)
    {
        var existing = await _db.JobMatches.FirstOrDefaultAsync(
            m => m.ResumeId == match.ResumeId && m.JobListingId == match.JobListingId, ct);

        if (existing is null)
        {
            await _db.JobMatches.AddAsync(match, ct);
        }
        else
        {
            existing.MatchScore = match.MatchScore;
            existing.MatchingSkillsJson = match.MatchingSkillsJson;
            existing.MissingSkillsJson = match.MissingSkillsJson;
            existing.Summary = match.Summary;
            existing.ScoredAt = match.ScoredAt;
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
