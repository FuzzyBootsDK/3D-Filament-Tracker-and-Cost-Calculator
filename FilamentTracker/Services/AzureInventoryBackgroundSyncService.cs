namespace FilamentTracker.Services;

public sealed class AzureInventoryBackgroundSyncService(
    IServiceScopeFactory scopeFactory,
    ILogger<AzureInventoryBackgroundSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var filamentService = scope.ServiceProvider.GetRequiredService<FilamentService>();
                var syncService = scope.ServiceProvider.GetRequiredService<AzureInventorySyncService>();

                var settings = await filamentService.GetSettingsAsync();
                if (!settings.AzureSyncEnabled || !settings.AzureAutoPushEnabled)
                    continue;

                var result = await syncService.PushInventoryAsync("hourly");
                if (result.Success)
                    logger.LogInformation("Hourly Azure sync completed.");
                else
                    logger.LogWarning("Hourly Azure sync skipped/failed: {Message}", result.Message);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error during hourly Azure sync");
            }
        }
    }
}

