using System.Net.Http.Json;
using FilamentTracker.Models;

namespace FilamentTracker.Services;

public sealed record AzureSyncResult(bool Success, string Message);

public sealed class AzureInventorySyncService(
    FilamentService filamentService,
    CsvService csvService,
    IHttpClientFactory httpClientFactory,
    ILogger<AzureInventorySyncService> logger)
{
    public async Task<AzureSyncResult> PushInventoryAsync(string source = "manual")
    {
        var settings = await filamentService.GetSettingsAsync();

        if (!settings.AzureSyncEnabled)
            return new AzureSyncResult(false, "Azure sync is disabled in Settings.");

        if (string.IsNullOrWhiteSpace(settings.AzureEndpoint) ||
            string.IsNullOrWhiteSpace(settings.AzureUsername) ||
            string.IsNullOrWhiteSpace(settings.AzurePassword))
        {
            return new AzureSyncResult(false, "Azure endpoint, username, and password are required.");
        }

        try
        {
            var csv = await csvService.ExportToCsvAsync();
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var payload = new
            {
                username = settings.AzureUsername,
                password = settings.AzurePassword,
                source,
                exportedAtUtc = DateTime.UtcNow,
                format = "csv",
                inventoryCsv = csv
            };

            var response = await client.PostAsJsonAsync(settings.AzureEndpoint.Trim(), payload);
            if (response.IsSuccessStatusCode)
                return new AzureSyncResult(true, "Inventory pushed to Azure successfully.");

            var body = await response.Content.ReadAsStringAsync();
            var shortBody = body.Length > 300 ? body[..300] + "..." : body;
            return new AzureSyncResult(false,
                $"Azure push failed: {(int)response.StatusCode} {response.ReasonPhrase}. {shortBody}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Azure inventory push failed");
            return new AzureSyncResult(false, $"Azure push failed: {ex.Message}");
        }
    }

    public async Task<AzureSyncResult> PullInventoryAsync()
    {
        var settings = await filamentService.GetSettingsAsync();

        if (!settings.AzureSyncEnabled)
            return new AzureSyncResult(false, "Azure sync is disabled in Settings.");

        if (string.IsNullOrWhiteSpace(settings.AzureEndpoint) ||
            string.IsNullOrWhiteSpace(settings.AzureUsername) ||
            string.IsNullOrWhiteSpace(settings.AzurePassword))
        {
            return new AzureSyncResult(false, "Azure endpoint, username, and password are required.");
        }

        return new AzureSyncResult(false,
            "Azure pull is not wired yet. Add your Azure API contract, then connect this method to import data.");
    }
}

