using WorkLens.Core.Entities;
using WorkLens.Core.Enums;

namespace WorkLens.Core.Interfaces;

public interface IJobListingRepository
{
    Task<(IReadOnlyList<JobListing> Items, int TotalCount)> GetPagedAsync(
        string? search, bool? remoteOnly, bool? trackedOnly, int page, int pageSize, CancellationToken ct);

    Task<JobListing?> GetByIdAsync(int id, CancellationToken ct);

    Task<JobListing?> FindByExternalIdAsync(JobSource source, string externalId, CancellationToken ct);

    Task UpsertRangeAsync(IReadOnlyList<JobListing> listings, CancellationToken ct);

    /// <summary>Marks listings from a source as inactive if they weren't seen in the latest fetch batch.</summary>
    Task DeactivateMissingAsync(JobSource source, IReadOnlyList<string> seenExternalIds, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
