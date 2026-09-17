using Microsoft.EntityFrameworkCore;
using WorkLens.Core.Entities;
using WorkLens.Core.Enums;
using WorkLens.Core.Interfaces;
using WorkLens.Infrastructure.Persistence;

namespace WorkLens.Infrastructure.Repositories;

public class JobListingRepository : IJobListingRepository
{
    private readonly WorkLensDbContext _db;

    public JobListingRepository(WorkLensDbContext db) => _db = db;

    public async Task<(IReadOnlyList<JobListing> Items, int TotalCount)> GetPagedAsync(
        string? search, bool? remoteOnly, bool? trackedOnly, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.JobListings
            .AsNoTracking()
            .Include(j => j.Application)
            .Where(j => j.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(j =>
                EF.Functions.Like(j.Title, $"%{term}%") ||
                EF.Functions.Like(j.Company, $"%{term}%") ||
                EF.Functions.Like(j.TagsJson, $"%{term}%"));
        }

        if (remoteOnly == true)
            query = query.Where(j => j.IsRemote);

        if (trackedOnly == true)
            query = query.Where(j => j.Application != null);
        else if (trackedOnly == false)
            query = query.Where(j => j.Application == null);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(j => j.TagsJson.Contains("$160k+ watch"))
            .ThenByDescending(j => j.TagsJson.Contains("$140k+ watch"))
            .ThenByDescending(j => j.TagsJson.Contains("Career Watch"))
            .ThenByDescending(j => j.PostedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task<JobListing?> GetByIdAsync(int id, CancellationToken ct) =>
        _db.JobListings.Include(j => j.Application).FirstOrDefaultAsync(j => j.Id == id, ct);

    public Task<JobListing?> FindByExternalIdAsync(JobSource source, string externalId, CancellationToken ct) =>
        _db.JobListings.FirstOrDefaultAsync(j => j.Source == source && j.ExternalId == externalId, ct);

    public async Task UpsertRangeAsync(IReadOnlyList<JobListing> listings, CancellationToken ct)
    {
        if (listings.Count == 0)
            return;

        foreach (var sourceGroup in listings.GroupBy(x => x.Source))
        {
            var externalIds = sourceGroup.Select(x => x.ExternalId).Distinct().ToArray();
            var existingByExternalId = await _db.JobListings
                .Where(x => x.Source == sourceGroup.Key && externalIds.Contains(x.ExternalId))
                .ToDictionaryAsync(x => x.ExternalId, StringComparer.Ordinal, ct);

            foreach (var incoming in sourceGroup)
            {
                if (!existingByExternalId.TryGetValue(incoming.ExternalId, out var existing))
                {
                    await _db.JobListings.AddAsync(incoming, ct);
                    existingByExternalId[incoming.ExternalId] = incoming;
                    continue;
                }

                existing.Title = incoming.Title;
                existing.Company = incoming.Company;
                existing.Location = incoming.Location;
                existing.IsRemote = incoming.IsRemote;
                existing.SalaryMin = incoming.SalaryMin;
                existing.SalaryMax = incoming.SalaryMax;
                existing.SalaryCurrency = incoming.SalaryCurrency;
                existing.TagsJson = incoming.TagsJson;
                existing.Url = incoming.Url;
                existing.DescriptionHtml = incoming.DescriptionHtml;
                existing.CompanyLogoUrl = incoming.CompanyLogoUrl;
                existing.PostedAt = incoming.PostedAt;
                existing.FetchedAt = incoming.FetchedAt;
                existing.IsActive = true;
            }
        }
    }

    public async Task DeactivateMissingAsync(JobSource source, IReadOnlyList<string> seenExternalIds, CancellationToken ct)
    {
        var query = _db.JobListings.Where(j => j.Source == source && j.IsActive);
        if (seenExternalIds.Count > 0)
            query = query.Where(j => !seenExternalIds.Contains(j.ExternalId));

        await query.ExecuteUpdateAsync(
            setters => setters.SetProperty(j => j.IsActive, false),
            ct);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
