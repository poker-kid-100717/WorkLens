using WorkLens.Core.Entities;
using WorkLens.Core.Enums;

namespace WorkLens.Core.Interfaces;

/// <summary>
/// Adapter contract for a single upstream job feed (RemoteOK, Remotive, Greenhouse, ...).
/// Each provider knows how to call its own API and map results into JobListing rows.
/// New sources are added by implementing this interface and registering it in DI —
/// the aggregator loops over all registered providers.
/// </summary>
public interface IJobFeedProvider
{
    JobSource Source { get; }

    /// <summary>
    /// Fetches the current set of listings from the upstream source, filtered by the
    /// given keywords where the provider's API supports server-side filtering
    /// (otherwise the aggregator filters client-side after this call).
    /// </summary>
    Task<IReadOnlyList<JobListing>> FetchAsync(IReadOnlyList<string> keywords, CancellationToken ct);
}
