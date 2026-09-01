using WorkLens.Core.Entities;
using WorkLens.Core.Interfaces;
using WorkLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace WorkLens.Infrastructure.Repositories;

public class ResumeRepository : IResumeRepository
{
    private readonly WorkLensDbContext _db;

    public ResumeRepository(WorkLensDbContext db) => _db = db;

    public Task<Resume?> GetActiveAsync(CancellationToken ct) =>
        _db.Resumes.Where(r => r.IsActive).OrderByDescending(r => r.UploadedAt).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Resume>> GetAllAsync(CancellationToken ct) =>
        await _db.Resumes.OrderByDescending(r => r.UploadedAt).ToListAsync(ct);

    public Task<Resume?> GetByIdAsync(int id, CancellationToken ct) =>
        _db.Resumes.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AddAsync(Resume resume, CancellationToken ct)
    {
        // Only one active resume drives matching at a time — deactivate the others.
        var existing = await _db.Resumes.Where(r => r.IsActive).ToListAsync(ct);
        foreach (var r in existing) r.IsActive = false;

        await _db.Resumes.AddAsync(resume, ct);
    }

    public void Update(Resume resume) => _db.Resumes.Update(resume);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
