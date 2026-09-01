using System.Text.Json;
using WorkLens.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace WorkLens.Infrastructure.Services;

/// <summary>
/// Orchestrates a single refresh cycle. Network-bound provider fetches run in parallel,
/// then database mutations are applied sequentially so a scoped EF Core DbContext is
/// never used concurrently across multiple threads.
/// </summary>
public class JobFeedAggregatorService
{
    private readonly IEnumerable<IJobFeedProvider> _providers;
    private readonly IJobListingRepository _listingRepo;
    private readonly ISearchProfileRepository _profileRepo;
    private readonly IResumeRepository _resumeRepo;
    private readonly FeedRefreshState _state;
    private readonly ILogger<JobFeedAggregatorService> _logger;

    public JobFeedAggregatorService(
        IEnumerable<IJobFeedProvider> providers,
        IJobListingRepository listingRepo,
        ISearchProfileRepository profileRepo,
        IResumeRepository resumeRepo,
        FeedRefreshState state,
        ILogger<JobFeedAggregatorService> logger)
    {
        _providers = providers;
        _listingRepo = listingRepo;
        _profileRepo = profileRepo;
        _resumeRepo = resumeRepo;
        _state = state;
        _logger = logger;
    }

    public async Task RefreshAllAsync(CancellationToken ct)
    {
        var keywords = await ResolveKeywordsAsync(ct);

        var fetchTasks = _providers.Select(async provider =>
        {
            try
            {
                var listings = await provider.FetchAsync(keywords, ct);
                return new ProviderFetchResult(provider, listings, null);
            }
            catch (Exception ex)
            {
                return new ProviderFetchResult(provider, null, ex);
            }
        });

        var results = await Task.WhenAll(fetchTasks);

        foreach (var result in results)
        {
            if (result.Error is not null)
            {
                _logger.LogError(result.Error, "Feed provider {Source} failed", result.Provider.Source);
                _state.RecordFailure(result.Provider.Source, result.Error.Message);
                continue;
            }

            var listings = result.Listings!;

            try
            {
                await _listingRepo.UpsertRangeAsync(listings, ct);
                await _listingRepo.DeactivateMissingAsync(
                    result.Provider.Source,
                    listings.Select(l => l.ExternalId).ToList(),
                    ct);

                _state.RecordSuccess(result.Provider.Source, listings.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Persisting feed provider {Source} failed", result.Provider.Source);
                _state.RecordFailure(result.Provider.Source, ex.Message);
            }
        }

        await _listingRepo.SaveChangesAsync(ct);
        _state.LastFullRefreshAt = DateTimeOffset.UtcNow;
    }

    private async Task<List<string>> ResolveKeywordsAsync(CancellationToken ct)
    {
        var profiles = await _profileRepo.GetActiveAsync(ct);
        var keywords = new List<string>();

        foreach (var profile in profiles)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(profile.KeywordsJson);
                if (parsed != null) keywords.AddRange(parsed);
            }
            catch (JsonException)
            {
                // Malformed keyword JSON on a profile shouldn't take down the whole refresh.
            }
        }

        var activeResume = await _resumeRepo.GetActiveAsync(ct);
        if (activeResume is not null)
            keywords.AddRange(ExtractResumeKeywords(activeResume.RawText));

        return keywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToList();
    }

    private static IEnumerable<string> ExtractResumeKeywords(string rawText)
    {
        var text = rawText.ToLowerInvariant();
        var keywords = new List<string>();

        static void Add(List<string> target, params string[] values)
        {
            foreach (var value in values)
            {
                if (!target.Contains(value, StringComparer.OrdinalIgnoreCase))
                    target.Add(value);
            }
        }

        if (text.Contains("software engineer"))
            Add(keywords, "software engineer", "software developer");
        if (text.Contains("full stack") || text.Contains("full-stack"))
            Add(keywords, "full stack", "full stack developer", "full stack engineer");
        if (text.Contains(".net") || text.Contains("dotnet"))
            Add(keywords, ".NET", ".NET developer", ".NET engineer");
        if (text.Contains("c#"))
            Add(keywords, "C#", "C# developer");
        if (text.Contains("asp.net"))
            Add(keywords, "ASP.NET");
        if (text.Contains("angular"))
            Add(keywords, "Angular");
        if (text.Contains("backend") || text.Contains("back-end"))
            Add(keywords, "backend developer", "backend engineer");
        if (text.Contains("architect"))
            Add(keywords, "software architect", "solution architect", "application architect");
        if (text.Contains("lead engineer") || text.Contains("technical lead") || text.Contains("tech lead"))
            Add(keywords, "lead software engineer", "lead developer");

        if (keywords.Count == 0)
            Add(keywords, "software engineer", "software developer");

        return keywords;
    }

    private sealed record ProviderFetchResult(
        IJobFeedProvider Provider,
        IReadOnlyList<WorkLens.Core.Entities.JobListing>? Listings,
        Exception? Error);
}
