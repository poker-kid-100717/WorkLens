using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WorkLens.Infrastructure.Services;

public class FeedRefreshOptions
{
    /// <summary>
    /// How often the backend polls upstream job feeds, in seconds.
    /// Kept separate from the frontend's UI poll interval (which hits our own
    /// cached API every 5-10s) — upstream sources should not be hit that fast.
    /// Default is a conservative 120s; configure via JobFeeds:RefreshIntervalSeconds.
    /// </summary>
    public int RefreshIntervalSeconds { get; set; } = 120;
}

/// <summary>
/// Runs <see cref="JobFeedAggregatorService"/> on a fixed interval for the lifetime
/// of the API host. Uses a scoped service provider per tick since repositories/DbContext
/// are scoped, but this service itself is registered as a singleton hosted service.
/// </summary>
public class FeedRefreshBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FeedRefreshOptions _options;
    private readonly ILogger<FeedRefreshBackgroundService> _logger;

    public FeedRefreshBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<FeedRefreshOptions> options,
        ILogger<FeedRefreshBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run once immediately on startup so the feed isn't empty while waiting for the first tick.
        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(30, _options.RefreshIntervalSeconds)));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var aggregator = scope.ServiceProvider.GetRequiredService<JobFeedAggregatorService>();
            await aggregator.RefreshAllAsync(ct);
            _logger.LogInformation("Job feed refresh cycle completed at {Time}", DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job feed refresh cycle failed unexpectedly");
        }
    }
}
