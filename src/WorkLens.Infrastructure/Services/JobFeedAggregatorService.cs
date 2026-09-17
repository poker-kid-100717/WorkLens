using System.Text.Json;
using Microsoft.Extensions.Logging;
using WorkLens.Core.Interfaces;

namespace WorkLens.Infrastructure.Services;

/// <summary>
/// Orchestrates a feed refresh cycle. Provider I/O runs concurrently, while persistence
/// is applied sequentially so the scoped EF Core DbContext is never used concurrently.
/// Refresh cycles themselves are serialized to prevent a manual refresh from overlapping
/// the hosted background refresh in the same process.
/// </summary>
public class JobFeedAggregatorService
{
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    private static readonly string[] CareerWatchKeywords =
    {
        "software engineer",
        "senior software engineer",
        "staff software engineer",
        "principal software engineer",
        "lead software engineer",
        ".NET",
        "C#",
        "ASP.NET Core",
        "full stack",
        "backend engineer",
        "software architect",
        "cloud engineer",
        "distributed systems"
    };

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
        await RefreshGate.WaitAsync(ct);
        try
        {
            await RefreshCoreAsync(ct);
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    private async Task RefreshCoreAsync(CancellationToken ct)
    {
        var keywords = await ResolveKeywordsAsync(ct);

        var fetchTasks = _providers.Select(async provider =>
        {
            try
            {
                var listings = await provider.FetchAsync(keywords, ct);
                return new ProviderFetchResult(provider, listings, null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
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
                _state.RecordFailure(result.Provider.Source, "Provider refresh failed");
                continue;
            }

            var listings = result.Listings!;

            try
            {
                foreach (var listing in listings)
                    JobWatchClassifier.ApplyTags(listing);

                await _listingRepo.UpsertRangeAsync(listings, ct);
                await _listingRepo.DeactivateMissingAsync(
                    result.Provider.Source,
                    listings.Select(l => l.ExternalId).ToList(),
                    ct);

                _state.RecordSuccess(result.Provider.Source, listings.Count);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Persisting feed provider {Source} failed", result.Provider.Source);
                _state.RecordFailure(result.Provider.Source, "Provider persistence failed");
            }
        }

        await _listingRepo.SaveChangesAsync(ct);
        _state.LastFullRefreshAt = DateTimeOffset.UtcNow;
    }

    private async Task<List<string>> ResolveKeywordsAsync(CancellationToken ct)
    {
        var keywords = new List<string>(CareerWatchKeywords);
        var profiles = await _profileRepo.GetActiveAsync(ct);

        foreach (var profile in profiles)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(profile.KeywordsJson);
                if (parsed is not null)
                    keywords.AddRange(parsed);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Search profile {ProfileId} contains invalid keyword JSON", profile.Id);
            }
        }

        var activeResume = await _resumeRepo.GetActiveAsync(ct);
        if (activeResume is not null)
            keywords.AddRange(ExtractResumeKeywords(activeResume.RawText));

        return keywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
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
