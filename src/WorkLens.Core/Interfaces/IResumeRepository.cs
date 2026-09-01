using WorkLens.Core.Entities;

namespace WorkLens.Core.Interfaces;

public interface IResumeRepository
{
    Task<Resume?> GetActiveAsync(CancellationToken ct);
    Task<IReadOnlyList<Resume>> GetAllAsync(CancellationToken ct);
    Task<Resume?> GetByIdAsync(int id, CancellationToken ct);
    Task AddAsync(Resume resume, CancellationToken ct);
    void Update(Resume resume);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
