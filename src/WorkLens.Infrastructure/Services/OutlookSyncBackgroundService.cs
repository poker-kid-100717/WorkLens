using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WorkLens.Infrastructure.Services;

public sealed class OutlookSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutlookOptions _options;
    private readonly ILogger<OutlookSyncBackgroundService> _logger;

    public OutlookSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutlookOptions> options,
        ILogger<OutlookSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromMinutes(Math.Max(1, _options.SyncIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var outlook = scope.ServiceProvider.GetRequiredService<OutlookCommunicationService>();
                var status = await outlook.GetStatusAsync(stoppingToken);
                if (status.IsConfigured && status.IsConnected)
                    await outlook.SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background Outlook synchronization failed.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
