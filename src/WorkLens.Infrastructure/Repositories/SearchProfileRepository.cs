using WorkLens.Core.Entities;
using WorkLens.Core.Interfaces;
using WorkLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace WorkLens.Infrastructure.Repositories;

public class SearchProfileRepository : ISearchProfileRepository
{
    private readonly WorkLensDbContext _db;

    public SearchProfileRepository(WorkLensDbContext db) => _db = db;

    public async Task<IReadOnlyList<SearchProfile>> GetActiveAsync(CancellationToken ct) =>
        await _db.SearchProfiles.Where(p => p.IsActive).ToListAsync(ct);

    public async Task<IReadOnlyList<SearchProfile>> GetAllAsync(CancellationToken ct) =>
        await _db.SearchProfiles.OrderBy(p => p.Name).ToListAsync(ct);

    public Task<SearchProfile?> GetByIdAsync(int id, CancellationToken ct) =>
        _db.SearchProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(SearchProfile profile, CancellationToken ct) =>
        await _db.SearchProfiles.AddAsync(profile, ct);

    public void Update(SearchProfile profile) => _db.SearchProfiles.Update(profile);

    public void Remove(SearchProfile profile) => _db.SearchProfiles.Remove(profile);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
