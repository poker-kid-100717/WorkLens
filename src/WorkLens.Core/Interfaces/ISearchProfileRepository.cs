using WorkLens.Core.Entities;

namespace WorkLens.Core.Interfaces;

public interface ISearchProfileRepository
{
    Task<IReadOnlyList<SearchProfile>> GetActiveAsync(CancellationToken ct);
    Task<IReadOnlyList<SearchProfile>> GetAllAsync(CancellationToken ct);
    Task<SearchProfile?> GetByIdAsync(int id, CancellationToken ct);
    Task AddAsync(SearchProfile profile, CancellationToken ct);
    void Update(SearchProfile profile);
    void Remove(SearchProfile profile);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
