using WorkLens.Core.Entities;

namespace WorkLens.Core.Interfaces;

public interface IJobMatchRepository
{
    Task<JobMatch?> FindAsync(int resumeId, int jobListingId, CancellationToken ct);
    Task<Dictionary<int, JobMatch>> GetForListingsAsync(int resumeId, IReadOnlyList<int> jobListingIds, CancellationToken ct);
    Task UpsertAsync(JobMatch match, CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
