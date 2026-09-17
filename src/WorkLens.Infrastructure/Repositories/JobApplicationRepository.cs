using Microsoft.EntityFrameworkCore;
using WorkLens.Core.Entities;
using WorkLens.Core.Interfaces;
using WorkLens.Infrastructure.Persistence;

namespace WorkLens.Infrastructure.Repositories;

public class JobApplicationRepository : IJobApplicationRepository
{
    private readonly WorkLensDbContext _db;

    public JobApplicationRepository(WorkLensDbContext db) => _db = db;

    public async Task<IReadOnlyList<JobApplication>> GetAllAsync(CancellationToken ct) =>
        await _db.JobApplications
            .AsNoTracking()
            .Include(a => a.JobListing)
            .Include(a => a.StatusHistory)
            .OrderByDescending(a => a.SavedAt)
            .ToListAsync(ct);

    public Task<JobApplication?> GetByIdAsync(int id, CancellationToken ct) =>
        _db.JobApplications
            .Include(a => a.JobListing)
            .Include(a => a.StatusHistory)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<JobApplication?> GetByJobListingIdAsync(int jobListingId, CancellationToken ct) =>
        _db.JobApplications.FirstOrDefaultAsync(a => a.JobListingId == jobListingId, ct);

    public async Task AddAsync(JobApplication application, CancellationToken ct) =>
        await _db.JobApplications.AddAsync(application, ct);

    public void Update(JobApplication application) => _db.JobApplications.Update(application);

    public void Remove(JobApplication application) => _db.JobApplications.Remove(application);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
