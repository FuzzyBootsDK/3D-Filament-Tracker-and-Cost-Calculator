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

        var settings = await context.AppSettings.FirstOrDefaultAsync();
        if (settings == null)
            return;

        try
        {
            var settingsChanged = false;

            var envBambuEnabled = Environment.GetEnvironmentVariable("BAMBULAB_ENABLED");
            var envBambuIp = Environment.GetEnvironmentVariable("BAMBULAB_IP");
            var envBambuCode = Environment.GetEnvironmentVariable("BAMBULAB_CODE");
            var envBambuSerial = Environment.GetEnvironmentVariable("BAMBULAB_SERIAL");

            if (!string.IsNullOrEmpty(envBambuEnabled) &&
                (envBambuEnabled == "1" || envBambuEnabled.Equals("true", StringComparison.OrdinalIgnoreCase)) &&
                !settings.BambuLabEnabled)
            {
                settings.BambuLabEnabled = true;
                settingsChanged = true;
            }

            if (!string.IsNullOrEmpty(envBambuIp) && settings.BambuLabIpAddress != envBambuIp)
            {
                settings.BambuLabIpAddress = envBambuIp;
                settingsChanged = true;
            }

            if (!string.IsNullOrEmpty(envBambuCode) && settings.BambuLabAccessCode != envBambuCode)
            {
                settings.BambuLabAccessCode = envBambuCode;
                settingsChanged = true;
            }

            if (!string.IsNullOrEmpty(envBambuSerial) && settings.BambuLabSerialNumber != envBambuSerial)
            {
                settings.BambuLabSerialNumber = envBambuSerial;
                settingsChanged = true;
            }

            var envRelayEnabled = Environment.GetEnvironmentVariable("MQTT_RELAY_ENABLED");
            var envRelayPort = Environment.GetEnvironmentVariable("MQTT_RELAY_PORT");
            var envRelayUsername = Environment.GetEnvironmentVariable("MQTT_RELAY_USERNAME");
            var envRelayPassword = Environment.GetEnvironmentVariable("MQTT_RELAY_PASSWORD");

            if (!string.IsNullOrEmpty(envRelayEnabled) &&
                (envRelayEnabled == "1" || envRelayEnabled.Equals("true", StringComparison.OrdinalIgnoreCase)) &&
                !settings.MqttRelayEnabled)
            {
                settings.MqttRelayEnabled = true;
                settingsChanged = true;
            }

            if (!string.IsNullOrEmpty(envRelayPort) && int.TryParse(envRelayPort, out var relayPort) && settings.MqttRelayPort != relayPort)
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

            if (settingsChanged)
                await context.SaveChangesAsync();
        }
        catch
        {
            // Environment overrides should never block startup.
        }

        var thresholdService = scope.ServiceProvider.GetRequiredService<ThresholdService>();
        thresholdService.SetThresholds(settings.LowThreshold, settings.CriticalThreshold);

        if (settings.BambuLabEnabled &&
            !string.IsNullOrEmpty(settings.BambuLabIpAddress) &&
            !string.IsNullOrEmpty(settings.BambuLabAccessCode) &&
            !string.IsNullOrEmpty(settings.BambuLabSerialNumber))
        {
            var startupPrinter = await context.Printers
                .Where(p => p.Enabled)
                .OrderByDescending(p => p.IsDefault)
                .ThenBy(p => p.Name)
                .FirstOrDefaultAsync();

            var bambuLabService = scope.ServiceProvider.GetRequiredService<BambuLabService>();
            bambuLabService.AmsAutoUpdateWeight = settings.AmsAutoUpdateWeight;
            bambuLabService.AmsAutoUpdateOnlyDecrease = settings.AmsAutoUpdateOnlyDecrease;
            var startupLogger = loggerFactory.CreateLogger("BambuLabInit");

            if (startupPrinter == null)
            {
                startupLogger.LogWarning("BambuLab is enabled but no enabled printer record exists. Skipping startup connect.");
            }
            else
            {
            _ = Task.Run(async () =>
            {
                try
                {
                    await bambuLabService.ConnectToPrinterAsync(
                        startupPrinter.Id,
                        startupPrinter.IpAddress,
                        startupPrinter.AccessCode,
                        startupPrinter.SerialNumber,
                        startupPrinter.Name
                    );
                }
                catch (Exception ex)
                {
                    startupLogger.LogError(ex, "Failed to connect to BambuLab printer on startup");
                }
            });
            }
        }

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
                        settings.MqttRelayPassword
                    );
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

