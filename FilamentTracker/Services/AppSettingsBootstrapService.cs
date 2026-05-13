using FilamentTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace FilamentTracker.Services;

public sealed class AppSettingsBootstrapService(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
{
    public async Task InitializeAsync()
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FilamentContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        var filamentService = scope.ServiceProvider.GetRequiredService<FilamentService>();
        var settings = await filamentService.GetSettingsAsync();

        // ── Apply MQTT relay environment-variable overrides ──────────────────
        try
        {
            var settingsChanged = false;

            var envRelayEnabled  = Environment.GetEnvironmentVariable("MQTT_RELAY_ENABLED");
            var envRelayPort     = Environment.GetEnvironmentVariable("MQTT_RELAY_PORT");
            var envRelayUsername = Environment.GetEnvironmentVariable("MQTT_RELAY_USERNAME");
            var envRelayPassword = Environment.GetEnvironmentVariable("MQTT_RELAY_PASSWORD");
            var envAzureEnabled  = Environment.GetEnvironmentVariable("AZURE_SYNC_ENABLED");
            var envAzureAutoPush = Environment.GetEnvironmentVariable("AZURE_SYNC_AUTO_PUSH_ENABLED");
            var envAzureEndpoint = Environment.GetEnvironmentVariable("AZURE_SYNC_ENDPOINT");
            var envAzureUsername = Environment.GetEnvironmentVariable("AZURE_SYNC_USERNAME");
            var envAzurePassword = Environment.GetEnvironmentVariable("AZURE_SYNC_PASSWORD");

            if (!string.IsNullOrEmpty(envRelayEnabled) &&
                (envRelayEnabled == "1" || envRelayEnabled.Equals("true", StringComparison.OrdinalIgnoreCase)) &&
                !settings.MqttRelayEnabled)
            {
                settings.MqttRelayEnabled = true;
                settingsChanged = true;
            }

            if (!string.IsNullOrEmpty(envRelayPort) &&
                int.TryParse(envRelayPort, out var relayPort) &&
                settings.MqttRelayPort != relayPort)
            {
                settings.MqttRelayPort = relayPort;
                settingsChanged = true;
            }

            if (!string.IsNullOrEmpty(envRelayUsername) && settings.MqttRelayUsername != envRelayUsername)
            {
                settings.MqttRelayUsername = envRelayUsername;
                settingsChanged = true;
            }

            if (!string.IsNullOrEmpty(envRelayPassword) && settings.MqttRelayPassword != envRelayPassword)
            {
                settings.MqttRelayPassword = envRelayPassword;
                settingsChanged = true;
            }

            if (!string.IsNullOrEmpty(envAzureEnabled))
            {
                var azureEnabled = envAzureEnabled == "1" || envAzureEnabled.Equals("true", StringComparison.OrdinalIgnoreCase);
                if (settings.AzureSyncEnabled != azureEnabled)
                {
                    settings.AzureSyncEnabled = azureEnabled;
                    settingsChanged = true;
                }
            }

            if (!string.IsNullOrEmpty(envAzureAutoPush))
            {
                var azureAuto = envAzureAutoPush == "1" || envAzureAutoPush.Equals("true", StringComparison.OrdinalIgnoreCase);
                if (settings.AzureAutoPushEnabled != azureAuto)
                {
                    settings.AzureAutoPushEnabled = azureAuto;
                    settingsChanged = true;
                }
            }

            if (!string.IsNullOrEmpty(envAzureEndpoint) && settings.AzureEndpoint != envAzureEndpoint)
            {
                settings.AzureEndpoint = envAzureEndpoint;
                settingsChanged = true;
            }

            if (!string.IsNullOrEmpty(envAzureUsername) && settings.AzureUsername != envAzureUsername)
            {
                settings.AzureUsername = envAzureUsername;
                settingsChanged = true;
            }

            if (!string.IsNullOrEmpty(envAzurePassword) && settings.AzurePassword != envAzurePassword)
            {
                settings.AzurePassword = envAzurePassword;
                settingsChanged = true;
            }

            if (settingsChanged)
                await filamentService.UpdateSettingsAsync(settings);
        }
        catch
        {
            // Environment overrides should never block startup.
        }

        // ── Restore singleton service state from persisted settings ──────────
        var thresholdService = scope.ServiceProvider.GetRequiredService<ThresholdService>();
        thresholdService.SetThresholds(settings.LowThreshold, settings.CriticalThreshold);

        var themeService = scope.ServiceProvider.GetRequiredService<ThemeService>();
        themeService.SetTheme(settings.Theme);

        // ── Connect all enabled printers ─────────────────────────────────────
        var enabledPrinters = await context.Printers
            .Where(p => p.Enabled)
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .ToListAsync();

        if (enabledPrinters.Count > 0)
        {
            var bambuLabService = scope.ServiceProvider.GetRequiredService<BambuLabService>();
            bambuLabService.AmsAutoUpdateWeight       = settings.AmsAutoUpdateWeight;
            bambuLabService.AmsAutoUpdateOnlyDecrease = settings.AmsAutoUpdateOnlyDecrease;
            var startupLogger = loggerFactory.CreateLogger("BambuLabInit");

            foreach (var printer in enabledPrinters)
            {
                var captured = printer;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await bambuLabService.ConnectToPrinterAsync(
                            captured.Id,
                            captured.IpAddress,
                            captured.AccessCode,
                            captured.SerialNumber,
                            captured.Name);
                    }
                    catch (Exception ex)
                    {
                        startupLogger.LogError(ex, "Failed to connect to printer {Name} on startup", captured.Name);
                    }
                });
            }
        }

        // ── Start MQTT relay if enabled ───────────────────────────────────────
        if (settings.MqttRelayEnabled)
        {
            var mqttRelayService = scope.ServiceProvider.GetRequiredService<MqttRelayService>();
            var relayLogger = loggerFactory.CreateLogger("MqttRelayInit");
            _ = Task.Run(async () =>
            {
                try
                {
                    await mqttRelayService.StartRelayServerAsync(
                        settings.MqttRelayPort,
                        settings.MqttRelayUsername,
                        settings.MqttRelayPassword);
                    relayLogger.LogInformation("MQTT Relay started on port {Port}", settings.MqttRelayPort);
                }
                catch (Exception ex)
                {
                    relayLogger.LogError(ex, "Failed to start MQTT Relay on startup");
                }
            });
        }
    }
}
