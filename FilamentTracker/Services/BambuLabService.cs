using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using FilamentTracker.Data;
using FilamentTracker.Models;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Formatter;

namespace FilamentTracker.Services;

public class BambuLabService(ILogger<BambuLabService> logger, IServiceProvider serviceProvider)
    : IAsyncDisposable
{
    private const int MaxLogEntries = 50;
    private readonly PrintStatus _currentStatus = new();

    // MQTT terminal log — last 50 raw messages
    private readonly Queue<MqttLogEntry> _mqttLog = new();

    private AMSInfoDto? _amsInfo;
    private IMqttClient? _mqttClient;
    private string? _serialNumber;
    private string? _ipAddress;
    private string? _accessCode;

    /// When true, automatically update spool WeightRemaining from AMS remain%
    /// (only for spools that have been tagged/linked in the AMS page).
    public bool AmsAutoUpdateWeight { get; set; }

    /// When true, the auto-update only ever decreases weight (never adds it back).
    /// When false, it syncs in both directions — AMS can also restore weight.
    public bool AmsAutoUpdateOnlyDecrease { get; set; } = true;

    public bool IsConnected => _mqttClient?.IsConnected ?? false;

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _mqttClient?.Dispose();
    }

    public event Action<PrintStatus>? OnStatusUpdated;
    public event Action<MqttLogEntry>? OnMqttMessageLogged;

    /// Fired after AMS auto-weight updates have been saved to the DB.
    /// UI pages can subscribe to reload their filament data.
    public event Action? OnAmsWeightUpdated;

    public PrintStatus GetCurrentStatus()
    {
        return _currentStatus;
    }

    public async Task ConnectAsync(string ipAddress, string accessCode, string serialNumber)
    {
        try
        {
            _ipAddress = ipAddress;
            _accessCode = accessCode;
            _serialNumber = serialNumber;

            // Disconnect existing connection if any
            if (_mqttClient?.IsConnected == true) await DisconnectAsync();

            // Create MQTT client
            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();

            // Configure MQTT options for BambuLab
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(ipAddress, 8883) // BambuLab uses port 8883 for MQTT over TLS
                .WithCredentials("bblp", accessCode)
                .WithClientId($"FilamentTracker_{Guid.NewGuid():N}")
                .WithTlsOptions(o =>
                {
                    o.UseTls();
                    o.WithAllowUntrustedCertificates();
                    o.WithIgnoreCertificateChainErrors();
                    o.WithIgnoreCertificateRevocationErrors();
                    o.WithCertificateValidationHandler(context => true);
                })
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(15))
                .WithCleanSession()
                .WithProtocolVersion(MqttProtocolVersion.V311) // Use MQTT 3.1.1
                .Build();

            // Handle incoming messages
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;

            // Handle connection events
            _mqttClient.ConnectedAsync += e =>
            {
                try
                {
                    logger.LogInformation("MQTT connected successfully to {IpAddress}", ipAddress);
                    _currentStatus.IsConnected = true;
                    _currentStatus.PrinterName = $"BambuLab ({serialNumber})";
                    try { OnStatusUpdated?.Invoke(_currentStatus); }
                    catch (Exception ex) { logger.LogWarning(ex, "A subscriber threw in OnStatusUpdated (connected)"); }
                    return Task.CompletedTask;
                }
                catch (Exception exception) { return Task.FromException(exception); }
            };

            _mqttClient.DisconnectedAsync += async e =>
            {
                logger.LogWarning("MQTT disconnected: {Reason}", e.Reason);
                _currentStatus.IsConnected = false;
                try { OnStatusUpdated?.Invoke(_currentStatus); }
                catch (Exception ex) { logger.LogWarning(ex, "A subscriber threw in OnStatusUpdated (disconnected)"); }

                if (e.Reason != MqttClientDisconnectReason.NormalDisconnection)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    try
                    {
                        if (_mqttClient is { IsConnected: false })
                        {
                            logger.LogInformation("Attempting to reconnect...");
                            await _mqttClient.ConnectAsync(options);
                        }
                    }
                    catch (Exception ex) { logger.LogError(ex, "Failed to reconnect to MQTT"); }
                }
            };

            // Connect
            logger.LogInformation("Connecting to BambuLab printer at {IpAddress}:8883 with serial {Serial}", ipAddress, serialNumber);

            var connectResult = await _mqttClient.ConnectAsync(options);

            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                logger.LogError("Failed to connect: {ResultCode}", connectResult.ResultCode);
                throw new Exception($"MQTT connection failed: {connectResult.ResultCode} - {connectResult.ReasonString}");
            }

            logger.LogInformation("Connected successfully. Result: {ResultCode}", connectResult.ResultCode);

            // Subscribe to a printer status topic
            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic($"device/{serialNumber}/report"))
                .Build();

            var subscribeResult = await _mqttClient.SubscribeAsync(subscribeOptions);

            var firstItem = subscribeResult.Items.FirstOrDefault();
            logger.LogInformation("Subscribed to topic: device/{Serial}/report. Result: {ResultCode}",
                serialNumber, firstItem?.ResultCode);

            _currentStatus.IsConnected = true;
            _currentStatus.PrinterName = $"BambuLab ({serialNumber})";
            OnStatusUpdated?.Invoke(_currentStatus);

            logger.LogInformation("Connected to BambuLab printer at {IpAddress}", ipAddress);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to BambuLab MQTT");
            _currentStatus.IsConnected = false;
            OnStatusUpdated?.Invoke(_currentStatus);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (_mqttClient?.IsConnected == true) await _mqttClient.DisconnectAsync();
            _currentStatus.IsConnected = false;
            _currentStatus.IsPrinting = false;
            OnStatusUpdated?.Invoke(_currentStatus);
            logger.LogInformation("Disconnected from BambuLab printer");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disconnecting from MQTT");
        }
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var payloadBytes = e.ApplicationMessage.Payload.ToArray();
            var payload = Encoding.UTF8.GetString(payloadBytes);
            logger.LogDebug("Received MQTT message: {Payload}", payload);

            // Store in rolling log
            var entry = new MqttLogEntry { Timestamp = DateTime.Now, Payload = payload };
            lock (_mqttLog)
            {
                _mqttLog.Enqueue(entry);
                while (_mqttLog.Count > MaxLogEntries)
                    _mqttLog.Dequeue();
            }

            // Append to a server-side log file (best-effort, fire-and-forget)
            try
            {
                var logLine =
                    $"[{entry.Timestamp:O}] {payload.Replace("\r\n", " ").Replace("\n", " ")}{Environment.NewLine}";
                _ = File.AppendAllTextAsync("bambulab-mqtt.log", logLine);
            }
            catch
            {
            }

            try
            {
                OnMqttMessageLogged?.Invoke(entry);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "A subscriber threw in OnMqttMessageLogged");
            }

            // Parse BambuLab JSON status
            var json = JsonDocument.Parse(payload);
            var root = json.RootElement;

            // AMS info parsing
            if (root.TryGetProperty("print", out var print))
            {
                if (print.TryGetProperty("ams", out var ams))
                    if (ams.TryGetProperty("ams", out var amsArray))
                    {
                        var amsInfoDto = new AMSInfoDto();
                        foreach (var amsObj in amsArray.EnumerateArray())
                        {
                            var amsDto = new AmsDto
                            {
                                ams_id = amsObj.TryGetProperty("ams_id", out var amsIdProp)
                                    ? GetStringSafe(amsIdProp)
                                    : "",
                                temp = amsObj.TryGetProperty("temp", out var tempProp) ? GetStringSafe(tempProp) : "",
                                humidity = amsObj.TryGetProperty("humidity", out var humProp)
                                    ? GetStringSafe(humProp)
                                    : "",
                                humidity_raw = amsObj.TryGetProperty("humidity_raw", out var humRawProp)
                                    ? GetStringSafe(humRawProp)
                                    : ""
                            };
                            if (amsObj.TryGetProperty("tray", out var trayArray))
                                foreach (var trayObj in trayArray.EnumerateArray())
                                {
                                    var trayDto = new AmsTrayDto
                                    {
                                        id = trayObj.TryGetProperty("id", out var idProp) ? GetStringSafe(idProp) : "",
                                        tray_color = trayObj.TryGetProperty("tray_color", out var colorProp)
                                            ? GetStringSafe(colorProp)
                                            : "",
                                        tray_type = trayObj.TryGetProperty("tray_type", out var typeProp)
                                            ? GetStringSafe(typeProp)
                                            : "",
                                        tray_sub_brands = trayObj.TryGetProperty("tray_sub_brands", out var subProp)
                                            ? GetStringSafe(subProp)
                                            : "",
                                        remain = trayObj.TryGetProperty("remain", out var remainProp)
                                            ? GetStringSafe(remainProp)
                                            : "",
                                        tray_uuid = trayObj.TryGetProperty("tray_uuid", out var uuidProp)
                                            ? GetStringSafe(uuidProp)
                                            : "",
                                        tag_uid = trayObj.TryGetProperty("tag_uid", out var tagProp)
                                            ? GetStringSafe(tagProp)
                                            : ""
                                    };
                                    amsDto.tray.Add(trayDto);
                                }

                            amsInfoDto.Ams.Add(amsDto);
                        }

                        _amsInfo = amsInfoDto;

                        // Map parsed AMS info into _currentStatus.AMSUnits so the UI sees it
                        var amsUnits = new List<AMSUnit>();
                        foreach (var amsDto in amsInfoDto.Ams)
                        {
                            double.TryParse(amsDto.temp, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out var tempVal);

                            // Parse both raw and processed humidity if available. Some firmware reports both
                            // a processed "humidity" value and a separate "humidity_raw" sensor value.
                            int.TryParse(amsDto.humidity, out var humVal);
                            int.TryParse(amsDto.humidity_raw, out var humRawVal);

                            // Prefer raw sensor value when present, otherwise fall back to processed humidity
                            var displayHum = humRawVal > 0 ? humRawVal : humVal;

                            var unit = new AMSUnit
                            {
                                AMSId = amsDto.ams_id,
                                Temperature = tempVal > 0 ? tempVal : null,
                                Humidity = displayHum > 0 ? displayHum : null
                            };
                            foreach (var trayDto in amsDto.tray)
                            {
                                int.TryParse(trayDto.id, out var trayIdInt);
                                int.TryParse(trayDto.remain, out var remainInt);
                                var colorHex = !string.IsNullOrEmpty(trayDto.tray_color) &&
                                               trayDto.tray_color.Length >= 6
                                    ? "#" + trayDto.tray_color[..6]
                                    : null;
                                unit.Slots.Add(new AMSSlot
                                {
                                    Id = trayIdInt,
                                    Index = trayIdInt + 1,
                                    Remain = remainInt > 0 ? remainInt : null,
                                    TrayType = string.IsNullOrEmpty(trayDto.tray_type) ? null : trayDto.tray_type,
                                    TrayColor = trayDto.tray_color,
                                    TraySubBrands = string.IsNullOrEmpty(trayDto.tray_sub_brands)
                                        ? null
                                        : trayDto.tray_sub_brands,
                                    ColorHex = colorHex,
                                    TrayUuid = string.IsNullOrEmpty(trayDto.tray_uuid) ? null : trayDto.tray_uuid,
                                    TagUid = string.IsNullOrEmpty(trayDto.tag_uid) ? null : trayDto.tag_uid
                                });
                            }

                            amsUnits.Add(unit);
                        }

                        _currentStatus.AMSUnits = amsUnits;
                        logger.LogInformation("AMS data parsed: {Count} unit(s)", amsUnits.Count);

                        // Auto-update spool weights if the setting is enabled
                        if (AmsAutoUpdateWeight)
                            _ = Task.Run(() => ApplyAmsWeightUpdatesAsync(amsUnits));
                    }

                // Update print status
                if (print.TryGetProperty("gcode_state", out var gcodeState))
                {
                    var state = GetStringSafe(gcodeState).ToLower();
                    if (string.IsNullOrEmpty(state)) state = "idle";
                    _currentStatus.Status = state;
                    _currentStatus.IsPrinting = state == "running" || state == "printing";
                }

                // Progress (mc_percent: 0-100)
                if (print.TryGetProperty("mc_percent", out var mcPercent))
                    _currentStatus.Progress = mcPercent.GetInt32();

                // Layer info
                if (print.TryGetProperty("layer_num", out var layerNum))
                    _currentStatus.CurrentLayer = layerNum.GetInt32();

                if (print.TryGetProperty("total_layer_num", out var totalLayerNum))
                    _currentStatus.TotalLayers = totalLayerNum.GetInt32();

                // Time info (in minutes)
                if (print.TryGetProperty("mc_remaining_time", out var remainingTime))
                {
                    var minutes = remainingTime.GetInt32();
                    _currentStatus.TimeRemaining = FormatTime(minutes);
                }

                // Calculate elapsed time from progress and remaining
                if (print.TryGetProperty("mc_remaining_time", out var remaining) && _currentStatus.Progress is > 0 and < 100)
                {
                    var remainingMin = remaining.GetInt32();
                    var totalMin = remainingMin * 100 / (100 - _currentStatus.Progress);
                    var elapsedMin = totalMin - remainingMin;
                    _currentStatus.TimeElapsed = FormatTime(elapsedMin);
                }
                else if (_currentStatus.Progress == 0)
                {
                    _currentStatus.TimeElapsed = "0m";
                }
                else if (_currentStatus.Progress == 100)
                {
                    // Print finished, elapsed = total
                    if (print.TryGetProperty("mc_remaining_time", out var finishedRemaining))
                    {
                        var finishedMin = finishedRemaining.GetInt32();
                        _currentStatus.TimeElapsed = FormatTime(finishedMin);
                    }
                }

                // Current file
                if (print.TryGetProperty("gcode_file", out var gcodeFile))
                    _currentStatus.CurrentFile = gcodeFile.GetString();

                // Temperatures
                if (print.TryGetProperty("bed_temper", out var bedTemp))
                    _currentStatus.BedTemperature = (int)bedTemp.GetDouble();

                if (print.TryGetProperty("nozzle_temper", out var nozzleTemp))
                    _currentStatus.NozzleTemperature = (int)nozzleTemp.GetDouble();

                // (Fan speeds not parsed — not present in MQTT payloads by default)

                // WiFi signal (e.g. "-59dBm")
                if (print.TryGetProperty("wifi_signal", out var wifiSignal))
                {
                    var sig = GetStringSafe(wifiSignal);
                    if (!string.IsNullOrEmpty(sig))
                        _currentStatus.WifiSignal = sig;
                }
            }

            // Notify subscribers
            try
            {
                OnStatusUpdated?.Invoke(_currentStatus);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "A subscriber threw in OnStatusUpdated");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing MQTT message");
        }

        return Task.CompletedTask;
    }

    private static bool AMSInfoEquals(AMSInfoDto? a, AMSInfoDto? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Ams.Count != b.Ams.Count) return false;
        for (var i = 0; i < a.Ams.Count; i++)
        {
            var amsA = a.Ams[i];
            var amsB = b.Ams[i];
            if (amsA.ams_id != amsB.ams_id) return false;
            if (amsA.tray.Count != amsB.tray.Count) return false;
            for (var j = 0; j < amsA.tray.Count; j++)
            {
                var trayA = amsA.tray[j];
                var trayB = amsB.tray[j];
                if (trayA.id != trayB.id) return false;
            }
        }

        return true;
    }

    private static string FormatTime(int minutes)
    {
        if (minutes < 0) minutes = 0;

        var hours = minutes / 60;
        var mins = minutes % 60;

        if (hours > 0)
            return $"{hours}h {mins}m";

        return $"{mins}m";
    }

    /// <summary>
    ///     Returns all AMS slots across all AMS units from the last received message.
    ///     Returns an empty list (not a hardcoded stub) when no data has been received yet.
    /// </summary>
    public IReadOnlyList<MqttLogEntry> GetMqttLog()
    {
        lock (_mqttLog)
        {
            return _mqttLog.ToList();
        }
    }

    public async Task<string> GetServerMqttLogAsync()
    {
        try
        {
            const string path = "bambulab-mqtt.log";
            if (!File.Exists(path)) return string.Empty;
            return await File.ReadAllTextAsync(path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read server MQTT log");
            return string.Empty;
        }
    }

    /// <summary>
    /// For each AMS slot that is linked to a spool in the inventory, update the spool's
    /// WeightRemaining to match what the AMS RFID reports. Only runs when AmsAutoUpdateWeight is true.
    /// Uses a DI scope so the scoped FilamentService/DbContext is used correctly from the singleton.
    /// </summary>
    private async Task ApplyAmsWeightUpdatesAsync(List<AMSUnit> amsUnits)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var filamentService = scope.ServiceProvider.GetRequiredService<FilamentService>();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FilamentTracker.Data.FilamentContext>>();

            await using var context = await dbContextFactory.CreateDbContextAsync();

            var anyChanged = false;

            foreach (var unit in amsUnits)
            foreach (var slot in unit.Slots)
            {
                if (slot.Remain == null) continue;

                // Only process RFID-tagged slots (skip all-zero UUIDs = non-BambuLab spools)
                var hasUuid = !string.IsNullOrEmpty(slot.TrayUuid) && slot.TrayUuid.Replace("0", "").Length > 0;
                var hasTag  = !string.IsNullOrEmpty(slot.TagUid)  && slot.TagUid.Replace("0", "").Length > 0;
                if (!hasUuid && !hasTag) continue;

                // Find the linked spool
                var spool = await context.Spools
                    .Include(s => s.Filament)
                    .Where(s => s.DateEmptied == null && s.WeightRemaining > 0)
                    .Where(s => (hasUuid && s.AmsTrayUuid == slot.TrayUuid) ||
                                (hasTag  && s.AmsTagUid   == slot.TagUid))
                    .FirstOrDefaultAsync();

                if (spool == null) continue;

                // Calculate new weight: AMS remain% × spool total weight
                var newWeight = Math.Round(spool.TotalWeight * slot.Remain.Value / 100m, 1);

                // Only write if it actually changed (avoid hammering the DB on every MQTT tick)
                if (Math.Abs(newWeight - spool.WeightRemaining) < 0.5m) continue;

                // If OnlyDecrease is enabled, skip updates that would add weight back
                if (AmsAutoUpdateOnlyDecrease && newWeight > spool.WeightRemaining)
                {
                    logger.LogDebug(
                        "AMS auto-update skipped (OnlyDecrease): spool {SpoolId} AMS says {New}g but inventory has {Old}g — not increasing",
                        spool.Id, newWeight, spool.WeightRemaining);
                    continue;
                }

                var direction = newWeight < spool.WeightRemaining ? "▼ decreased" : "▲ increased";
                logger.LogInformation(
                    "AMS auto-update: spool {SpoolId} ({Brand} {Color}) {Direction} {Old}g → {New}g (AMS reports {Pct}%)",
                    spool.Id, spool.Filament?.Brand, spool.Filament?.ColorName,
                    direction, spool.WeightRemaining, newWeight, slot.Remain.Value);

                spool.WeightRemaining = newWeight;
                anyChanged = true;
            }

            await context.SaveChangesAsync();

            // Notify UI that spool weights changed so pages can reload fresh data
            if (anyChanged)
            {
                try { OnAmsWeightUpdated?.Invoke(); }
                catch (Exception ex) { logger.LogWarning(ex, "A subscriber threw in OnAmsWeightUpdated"); }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply AMS weight updates");
        }
    }

    // No helper for fan parsing — fan speeds are not parsed because MQTT payloads don't reliably include them.

    private static string GetStringSafe(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.Null => "",
            _ => element.ToString()
        };
    }
}

// DTOs for AMS info
public class AMSInfoDto
{
    public List<AmsDto> Ams { get; set; } = new();
}

public class AmsDto
{
    public string ams_id { get; init; } = "";
    public string temp { get; init; } = "";
    public string humidity { get; init; } = "";
    public string humidity_raw { get; init; } = "";
    public List<AmsTrayDto> tray { get; set; } = [];
}

public class AmsTrayDto
{
    public string id { get; init; } = "";
    public string tray_color { get; init; } = "";
    public string tray_type { get; init; } = "";
    public string tray_sub_brands { get; init; } = "";
    public string remain { get; init; } = "";
    public string tray_uuid { get; init; } = "";
    public string tag_uid { get; init; } = "";
}

public class MqttLogEntry
{
    public DateTime Timestamp { get; init; }
    public string Payload { get; init; } = "";
    public string Topic { get; set; } = "";
}