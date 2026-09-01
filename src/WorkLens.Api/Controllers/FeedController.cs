using WorkLens.Api.Mapping;
using WorkLens.Core.DTOs;
using WorkLens.Core.Interfaces;
using WorkLens.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace WorkLens.Api.Controllers;

/// <summary>
/// Serves the aggregated, already-fetched job feed from local storage.
/// This is what the Angular frontend polls every 5-10 seconds — it never calls
/// upstream job boards directly, it just reads whatever the background refresh
/// service last cached in SQL Server.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FeedController : ControllerBase
{
    private readonly IJobListingRepository _listingRepo;
    private readonly FeedRefreshState _refreshState;
    private readonly JobFeedAggregatorService _aggregator;
    private readonly FeedRefreshOptions _options;

    public FeedController(
        IJobListingRepository listingRepo,
        FeedRefreshState refreshState,
        JobFeedAggregatorService aggregator,
        IOptions<FeedRefreshOptions> options)
    {
        _listingRepo = listingRepo;
        _refreshState = refreshState;
        _aggregator = aggregator;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<ActionResult<FeedResponseDto>> Get(
        [FromQuery] string? search,
        [FromQuery] bool? remoteOnly,
        [FromQuery] bool? trackedOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var (items, total) = await _listingRepo.GetPagedAsync(search, remoteOnly, trackedOnly, page, pageSize, ct);
        var snapshot = _refreshState.Snapshot();

        return Ok(new FeedResponseDto
        {
            Items = items.Select(i => i.ToDto()).ToList(),
            TotalCount = total,
            LastRefreshedAt = _refreshState.LastFullRefreshAt ?? DateTimeOffset.MinValue,
            RefreshIntervalSeconds = _options.RefreshIntervalSeconds,
            SourceStatuses = snapshot.Select(kv => new FeedSourceStatusDto
            {
                Source = kv.Key.ToString(),
                LastFetchSucceeded = kv.Value.LastFetchSucceeded,
                LastFetchedAt = kv.Value.LastFetchedAt,
                ListingCount = kv.Value.ListingCount,
                LastError = kv.Value.LastError
            }).ToList()
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<JobListingDto>> GetById(int id, CancellationToken ct)
    {
        var job = await _listingRepo.GetByIdAsync(id, ct);
        if (job is null) return NotFound();
        return Ok(job.ToDto());
    }

    /// <summary>Triggers an immediate out-of-cycle refresh instead of waiting for the next timer tick.</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshNow(CancellationToken ct)
    {
        await _aggregator.RefreshAllAsync(ct);
        return Accepted();
    }
}
