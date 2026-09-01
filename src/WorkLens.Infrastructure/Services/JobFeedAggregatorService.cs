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
    private readonly FeedRefreshState _state;
    private readonly ILogger<JobFeedAggregatorService> _logger;

    public JobFeedAggregatorService(
        IEnumerable<IJobFeedProvider> providers,
        IJobListingRepository listingRepo,
        ISearchProfileRepository profileRepo,
        FeedRefreshState state,
        ILogger<JobFeedAggregatorService> logger)
    {
        _providers = providers;
        _listingRepo = listingRepo;
        _profileRepo = profileRepo;
        _state = state;
        _logger = logger;
    }

    public async Task RefreshAllAsync(CancellationToken ct)
    {
        var keywords = await ResolveKeywordsAsync(ct);

        // Fetch from independent HTTP providers in parallel. No EF Core work happens
        // in these tasks, so the scoped DbContext is not shared concurrently.
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

        // A scoped DbContext is not thread-safe. Apply each provider result one at a
        // time, then commit the complete refresh as one unit of work.
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

        return keywords.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private sealed record ProviderFetchResult(
        IJobFeedProvider Provider,
        IReadOnlyList<WorkLens.Core.Entities.JobListing>? Listings,
        Exception? Error);
}
