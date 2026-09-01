using WorkLens.Core.Entities;

namespace WorkLens.Core.Interfaces;

public interface IJobApplicationRepository
{
    Task<IReadOnlyList<JobApplication>> GetAllAsync(CancellationToken ct);
    Task<JobApplication?> GetByIdAsync(int id, CancellationToken ct);
    Task<JobApplication?> GetByJobListingIdAsync(int jobListingId, CancellationToken ct);
    Task AddAsync(JobApplication application, CancellationToken ct);
    void Update(JobApplication application);
    void Remove(JobApplication application);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
