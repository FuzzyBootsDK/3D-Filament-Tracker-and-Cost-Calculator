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

    // Multi-connection state
    private readonly Dictionary<int, IMqttClient> _mqttClients = new();
    private readonly Dictionary<int, PrintStatus> _printerStatuses = new();
    private readonly Dictionary<int, CancellationTokenSource> _cancellationTokens = new();
    private readonly Dictionary<int, string> _printerNames = new();
    private readonly Dictionary<int, string> _printerSerialNumbers = new();
    private readonly Queue<MqttLogEntry> _mqttLog = new();
    private readonly object _lock = new();

    /// When true, automatically update spool WeightRemaining from AMS remain%
    /// (only for spools that have been tagged/linked in the AMS page).
    public bool AmsAutoUpdateWeight { get; set; }

    /// When true, the auto-update only ever decreases weight (never adds it back).
    /// When false, it syncs in both directions — AMS can also restore weight.
    public bool AmsAutoUpdateOnlyDecrease { get; set; } = true;

    // Multi-connection API
    public bool IsAnyConnected
    {
        get
        {
            lock (_lock)
            {
                return _mqttClients.Any(kvp => kvp.Value.IsConnected);
            }
        }
    }

    public bool IsPrinterConnected(int printerId)
    {
        lock (_lock)
        {
            return _mqttClients.TryGetValue(printerId, out var client) && client.IsConnected;
        }
    }

    public PrintStatus? GetPrinterStatus(int printerId)
    {
        lock (_lock)
        {
            return _printerStatuses.GetValueOrDefault(printerId);
        }
    }

    public List<int> GetConnectedPrinterIds()
    {
        lock (_lock)
        {
            return _mqttClients
                .Where(kvp => kvp.Value.IsConnected)
                .Select(kvp => kvp.Key)
                .ToList();
        }
    }

    public Dictionary<int, PrintStatus> GetAllPrinterStatuses()
    {
        lock (_lock)
        {
            return new Dictionary<int, PrintStatus>(_printerStatuses);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAllAsync();
        lock (_lock)
        {
            foreach (var client in _mqttClients.Values)
            {
                client?.Dispose();
            }
            _mqttClients.Clear();
        }
    }

    // Events now include printer ID
    public event Action<int, PrintStatus>? OnStatusUpdated;
    public event Action<MqttLogEntry>? OnMqttMessageLogged;

    /// Fired after AMS auto-weight updates have been saved to the DB.
    /// Includes printer ID and affected spool IDs
    public event Action<int>? OnAmsWeightUpdated;

    // Backward compatibility methods (deprecated)
    [Obsolete("Use IsPrinterConnected(int printerId) for multi-printer support")]
    public bool IsConnected => IsAnyConnected;

    [Obsolete("Use GetPrinterStatus(int printerId) for multi-printer support")]
    public PrintStatus GetCurrentStatus()
    {
        lock (_lock)
        {
            // Return first connected printer status or empty
            var firstConnectedId = _mqttClients
                .FirstOrDefault(kvp => kvp.Value.IsConnected).Key;
            return _printerStatuses.GetValueOrDefault(firstConnectedId) ?? new PrintStatus();
        }
    }

    [Obsolete("Use GetConnectedPrinterIds() for multi-printer support")]
    public int? GetConnectedPrinterId()
    {
        lock (_lock)
        {
            return _mqttClients
                .FirstOrDefault(kvp => kvp.Value.IsConnected).Key;
        }
    }

    [Obsolete("Use GetPrinterStatus(printerId).PrinterName for multi-printer support")]
    public string GetConnectedPrinterName()
    {
        lock (_lock)
        {
            var firstId = _mqttClients.FirstOrDefault(kvp => kvp.Value.IsConnected).Key;
            return _printerNames.GetValueOrDefault(firstId) ?? "";
        }
    }

    // Backward compatibility: ConnectAsync (wraps new method)
    [Obsolete("Use ConnectToPrinterAsync for explicit multi-printer support")]
    public async Task ConnectAsync(string ipAddress, string accessCode, string serialNumber, int? printerId = null, string printerName = "")
    {
        if (printerId.HasValue)
        {
            await ConnectToPrinterAsync(printerId.Value, ipAddress, accessCode, serialNumber, printerName);
        }
        else
        {
            throw new ArgumentException("printerId is required for multi-connection mode");
        }
    }

    public async Task ConnectToPrinterAsync(int printerId, string ipAddress, string accessCode, string serialNumber, string printerName)
    {
        try
        {
            // Disconnect existing connection to this printer if any
            if (IsPrinterConnected(printerId))
            {
                logger.LogInformation("Printer {PrinterId} already connected, disconnecting first", printerId);
                await DisconnectFromPrinterAsync(printerId);
            }

            logger.LogInformation("Connecting to printer {PrinterId} ({Name}) at {IpAddress}:8883 with serial {Serial}", 
                printerId, printerName, ipAddress, serialNumber);

            var factory = new MqttClientFactory();
            var client = factory.CreateMqttClient();
            var cts = new CancellationTokenSource();

            lock (_lock)
            {
                _mqttClients[printerId] = client;
                _cancellationTokens[printerId] = cts;
                _printerNames[printerId] = printerName;
                _printerSerialNumbers[printerId] = serialNumber;
                _printerStatuses[printerId] = new PrintStatus 
                { 
                    PrinterName = $"{printerName} ({serialNumber})",
                    IsConnected = false
                };
            }

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(ipAddress, 8883)
                .WithCredentials("bblp", accessCode)
                .WithClientId($"FilamentTracker_{printerId}_{Guid.NewGuid():N}")
                .WithTlsOptions(o =>
                {
                    o.UseTls();
                    o.WithAllowUntrustedCertificates();
                    o.WithIgnoreCertificateChainErrors();
                    o.WithIgnoreCertificateRevocationErrors();
                    o.WithCertificateValidationHandler(_ => true);
                })
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(15))
                .WithCleanSession()
                .WithProtocolVersion(MqttProtocolVersion.V311)
                .Build();

            // Setup message handler for this specific printer
            client.ApplicationMessageReceivedAsync += args => OnMessageReceived(printerId, args);

            client.ConnectedAsync += _ =>
            {
                logger.LogInformation("MQTT connected successfully to printer {PrinterId} at {IpAddress}", printerId, ipAddress);
                PrintStatus? status = null;
                lock (_lock)
                {
                    if (_printerStatuses.TryGetValue(printerId, out var foundStatus))
                    {
                        foundStatus.IsConnected = true;
                        foundStatus.PrinterName = $"{printerName} ({serialNumber})";
                        status = foundStatus;
                    }
                }
                if (status != null)
                {
                    try { OnStatusUpdated?.Invoke(printerId, status); }
                    catch (Exception ex) { logger.LogWarning(ex, "A subscriber threw in OnStatusUpdated (connected) for printer {PrinterId}", printerId); }
                }
                return Task.CompletedTask;
            };

            client.DisconnectedAsync += async e =>
            {
                logger.LogWarning("MQTT disconnected from printer {PrinterId}: {Reason}", printerId, e.Reason);
                PrintStatus? status = null;
                lock (_lock)
                {
                    if (_printerStatuses.TryGetValue(printerId, out var foundStatus))
                    {
                        foundStatus.IsConnected = false;
                        status = foundStatus;
                    }
                }
                if (status != null)
                {
                    try { OnStatusUpdated?.Invoke(printerId, status); }
                    catch (Exception ex) { logger.LogWarning(ex, "A subscriber threw in OnStatusUpdated (disconnected) for printer {PrinterId}", printerId); }
                }

                if (e.Reason != MqttClientDisconnectReason.NormalDisconnection)
                {
                    // Auto-reconnect logic
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    try
                    {
                        bool shouldReconnect;
                        lock (_lock)
                        {
                            shouldReconnect = _mqttClients.ContainsKey(printerId) && 
                                            _mqttClients[printerId] is { IsConnected: false };
                        }

                        if (shouldReconnect)
                        {
                            logger.LogInformation("Attempting to reconnect printer {PrinterId}...", printerId);
                            await client.ConnectAsync(options);
                        }
                    }
                    catch (Exception ex) { logger.LogError(ex, "Failed to reconnect printer {PrinterId} to MQTT", printerId); }
                }
            };

            var connectResult = await client.ConnectAsync(options);

            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                logger.LogError("Failed to connect printer {PrinterId}: {ResultCode}", printerId, connectResult.ResultCode);

                // Clean up failed connection
                await DisconnectFromPrinterAsync(printerId);

                throw new Exception($"MQTT connection failed: {connectResult.ResultCode} - {connectResult.ReasonString}");
            }

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic($"device/{serialNumber}/report"))
                .Build();

            var subscribeResult = await client.SubscribeAsync(subscribeOptions);
            logger.LogInformation("Printer {PrinterId} subscribed to topic: device/{Serial}/report. Result: {ResultCode}",
                printerId, serialNumber, subscribeResult.Items.FirstOrDefault()?.ResultCode);

            PrintStatus? finalStatus = null;
            lock (_lock)
            {
                if (_printerStatuses.TryGetValue(printerId, out var foundStatus))
                {
                    foundStatus.IsConnected = true;
                    foundStatus.PrinterName = $"{printerName} ({serialNumber})";
                    finalStatus = foundStatus;
                }
            }
            if (finalStatus != null)
            {
                OnStatusUpdated?.Invoke(printerId, finalStatus);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect printer {PrinterId} to BambuLab MQTT", printerId);

            PrintStatus? errorStatus = null;
            lock (_lock)
            {
                if (_printerStatuses.TryGetValue(printerId, out var foundStatus))
                {
                    foundStatus.IsConnected = false;
                    errorStatus = foundStatus;
                }
            }
            if (errorStatus != null)
            {
                OnStatusUpdated?.Invoke(printerId, errorStatus);
            }
            throw;
        }
    }

    // Backward compatibility
    [Obsolete("Use DisconnectFromPrinterAsync(int printerId) or DisconnectAllAsync()")]
    public async Task DisconnectAsync()
    {
        // Disconnect first connected printer for backward compatibility
        var firstId = GetConnectedPrinterIds().FirstOrDefault();
        if (firstId != 0)
        {
            await DisconnectFromPrinterAsync(firstId);
        }
    }

    public async Task DisconnectFromPrinterAsync(int printerId)
    {
        try
        {
            IMqttClient? client;
            CancellationTokenSource? cts;
            PrintStatus? status;

            lock (_lock)
            {
                if (!_mqttClients.TryGetValue(printerId, out client))
                {
                    logger.LogDebug("Printer {PrinterId} not found in connected clients", printerId);
                    return; // Not connected
                }

                _cancellationTokens.TryGetValue(printerId, out cts);
                _printerStatuses.TryGetValue(printerId, out status);
            }

            try
            {
                cts?.Cancel();
                if (client?.IsConnected == true)
                {
                    await client.DisconnectAsync();
                }
                client?.Dispose();
            }
            finally
            {
                lock (_lock)
                {
                    _mqttClients.Remove(printerId);
                    _cancellationTokens.Remove(printerId);
                    _printerNames.Remove(printerId);
                    _printerSerialNumbers.Remove(printerId);
                    _printerStatuses.Remove(printerId);
                }

                if (status != null)
                {
                    status.IsConnected = false;
                    status.IsPrinting = false;
                    OnStatusUpdated?.Invoke(printerId, status);
                }

                logger.LogInformation("Disconnected from printer {PrinterId}", printerId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disconnecting from printer {PrinterId}", printerId);
        }
    }

    public async Task DisconnectAllAsync()
    {
        List<int> printerIds;
        lock (_lock)
        {
            printerIds = _mqttClients.Keys.ToList();
        }

        logger.LogInformation("Disconnecting from {Count} printer(s)", printerIds.Count);

        foreach (var printerId in printerIds)
        {
            await DisconnectFromPrinterAsync(printerId);
        }
    }

    private Task OnMessageReceived(int printerId, MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload.ToArray());
            logger.LogDebug("Received MQTT message from printer {PrinterId}: {Payload}", printerId, payload);

            string printerName;
            lock (_lock)
            {
                printerName = _printerNames.GetValueOrDefault(printerId) ?? $"Printer {printerId}";
            }

            var entry = new MqttLogEntry 
            { 
                Timestamp = DateTime.Now, 
                Payload = payload,
                PrinterId = printerId,
                PrinterName = printerName
            };
            lock (_mqttLog)
            {
                _mqttLog.Enqueue(entry);
                while (_mqttLog.Count > MaxLogEntries)
                    _mqttLog.Dequeue();
            }

            // Append to server-side log file (best-effort)
            var logLine = $"[{entry.Timestamp:O}] [Printer {printerId}] {payload.Replace("\r\n", " ").Replace("\n", " ")}{Environment.NewLine}";
            _ = File.AppendAllTextAsync("bambulab-mqtt.log", logLine)
                    .ContinueWith(t => logger.LogDebug(t.Exception, "Log write failed"), TaskContinuationOptions.OnlyOnFaulted);

            try { OnMqttMessageLogged?.Invoke(entry); }
            catch (Exception ex) { logger.LogWarning(ex, "A subscriber threw in OnMqttMessageLogged for printer {PrinterId}", printerId); }

            PrintStatus status;
            lock (_lock)
            {
                if (!_printerStatuses.TryGetValue(printerId, out status!))
                {
                    logger.LogWarning("Received message for disconnected printer {PrinterId}", printerId);
                    return Task.CompletedTask;
                }
            }

            var root = JsonDocument.Parse(payload).RootElement;

            if (root.TryGetProperty("print", out var print))
            {
                if (print.TryGetProperty("ams", out var ams) &&
                    ams.TryGetProperty("ams", out var amsArray))
                {
                    var amsUnits = ParseAmsUnits(amsArray);
                    status.AMSUnits = amsUnits;
                    logger.LogInformation("Printer {PrinterId}: AMS data parsed: {Count} unit(s)", printerId, amsUnits.Count);

                    if (AmsAutoUpdateWeight)
                        _ = Task.Run(() => ApplyAmsWeightUpdatesAsync(printerId, amsUnits));
                }

                if (print.TryGetProperty("gcode_state", out var gcodeState))
                {
                    var state = GetStringSafe(gcodeState).ToLower();
                    if (string.IsNullOrEmpty(state)) state = "idle";
                    status.Status = state;
                    status.IsPrinting = state is "running" or "printing";
                }

                if (print.TryGetProperty("mc_percent", out var mcPercent))
                    status.Progress = mcPercent.GetInt32();

                if (print.TryGetProperty("layer_num", out var layerNum))
                    status.CurrentLayer = layerNum.GetInt32();

                if (print.TryGetProperty("total_layer_num", out var totalLayerNum))
                    status.TotalLayers = totalLayerNum.GetInt32();

                if (print.TryGetProperty("mc_remaining_time", out var remainingTime))
                    status.TimeRemaining = FormatTime(remainingTime.GetInt32());

                if (print.TryGetProperty("mc_remaining_time", out var remaining) && status.Progress is > 0 and < 100)
                {
                    var remainingMin = remaining.GetInt32();
                    var totalMin = remainingMin * 100 / (100 - status.Progress);
                    status.TimeElapsed = FormatTime(totalMin - remainingMin);
                }
                else if (status.Progress == 0)
                    status.TimeElapsed = "0m";

                if (print.TryGetProperty("gcode_file", out var gcodeFile))
                    status.CurrentFile = gcodeFile.GetString();

                if (print.TryGetProperty("bed_temper", out var bedTemp))
                    status.BedTemperature = (int)bedTemp.GetDouble();

                if (print.TryGetProperty("nozzle_temper", out var nozzleTemp))
                    status.NozzleTemperature = (int)nozzleTemp.GetDouble();

                if (print.TryGetProperty("wifi_signal", out var wifiSignal))
                {
                    var sig = GetStringSafe(wifiSignal);
                    if (!string.IsNullOrEmpty(sig))
                        status.WifiSignal = sig;
                }
            }

            try { OnStatusUpdated?.Invoke(printerId, status); }
            catch (Exception ex) { logger.LogWarning(ex, "A subscriber threw in OnStatusUpdated for printer {PrinterId}", printerId); }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing MQTT message from printer {PrinterId}", printerId);
        }

        return Task.CompletedTask;
    }

    private static List<AMSUnit> ParseAmsUnits(JsonElement amsArray)
    {
        var units = new List<AMSUnit>();
        foreach (var amsObj in amsArray.EnumerateArray())
        {
            double.TryParse(
                amsObj.TryGetProperty("temp", out var tempProp) ? GetStringSafe(tempProp) : "0",
                NumberStyles.Float, CultureInfo.InvariantCulture, out var tempVal);

            int.TryParse(amsObj.TryGetProperty("humidity", out var humProp) ? GetStringSafe(humProp) : "0", out var humVal);
            int.TryParse(amsObj.TryGetProperty("humidity_raw", out var humRawProp) ? GetStringSafe(humRawProp) : "0", out var humRawVal);

            var unit = new AMSUnit
            {
                AMSId      = amsObj.TryGetProperty("ams_id", out var amsIdProp) ? GetStringSafe(amsIdProp) : "",
                Temperature = tempVal > 0 ? tempVal : null,
                Humidity    = humRawVal > 0 ? humRawVal : humVal > 0 ? humVal : null
            };

            if (amsObj.TryGetProperty("tray", out var trayArray))
                foreach (var trayObj in trayArray.EnumerateArray())
                {
                    int.TryParse(trayObj.TryGetProperty("id", out var idProp) ? GetStringSafe(idProp) : "0", out var trayId);
                    int.TryParse(trayObj.TryGetProperty("remain", out var remainProp) ? GetStringSafe(remainProp) : "0", out var remainInt);
                    var rawColor = trayObj.TryGetProperty("tray_color", out var colorProp) ? GetStringSafe(colorProp) : "";
                    var colorHex = rawColor.Length >= 6 ? "#" + rawColor[..6] : null;
                    var trayUuid = trayObj.TryGetProperty("tray_uuid", out var uuidProp) ? GetStringSafe(uuidProp) : "";
                    var tagUid   = trayObj.TryGetProperty("tag_uid",   out var tagProp)  ? GetStringSafe(tagProp)  : "";

                    unit.Slots.Add(new AMSSlot
                    {
                        Id            = trayId,
                        Index         = trayId + 1,
                        Remain        = remainInt > 0 ? remainInt : null,
                        TrayType      = trayObj.TryGetProperty("tray_type",      out var typeProp) ? GetStringSafe(typeProp).NullIfEmpty()  : null,
                        TrayColor     = rawColor,
                        TraySubBrands = trayObj.TryGetProperty("tray_sub_brands", out var subProp) ? GetStringSafe(subProp).NullIfEmpty() : null,
                        ColorHex      = colorHex,
                        TrayUuid      = trayUuid.NullIfEmpty(),
                        TagUid        = tagUid.NullIfEmpty(),
                    });
                }

            units.Add(unit);
        }
        return units;
    }

    private static string FormatTime(int minutes)
    {
        if (minutes < 0) minutes = 0;
        var hours = minutes / 60;
        var mins  = minutes % 60;
        return hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
    }

    public IReadOnlyList<MqttLogEntry> GetMqttLog()
    {
        lock (_mqttLog)
            return _mqttLog.ToList();
    }

    public async Task<string> GetServerMqttLogAsync()
    {
        try
        {
            const string path = "bambulab-mqtt.log";
            return File.Exists(path) ? await File.ReadAllTextAsync(path) : string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read server MQTT log");
            return string.Empty;
        }
    }

    private async Task ApplyAmsWeightUpdatesAsync(int printerId, List<AMSUnit> amsUnits)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FilamentContext>>();
            await using var context = await dbContextFactory.CreateDbContextAsync();

            var anyChanged = false;

            foreach (var unit in amsUnits)
            foreach (var slot in unit.Slots)
            {
                if (slot.Remain == null) continue;

                // Only auto-update when the slot reports a real RFID tag UID.
                // Do not auto-match based on tray UUID alone (weak/location id).
                var hasTag = !string.IsNullOrEmpty(slot.TagUid) && slot.TagUid.Replace("0", "").Length > 0;
                if (!hasTag) continue;

                var spool = await context.Spools
                    .Include(s => s.Filament)
                    .Where(s => s.DateEmptied == null && s.WeightRemaining > 0)
                    .Where(s => s.AmsTagUid == slot.TagUid)
                    .FirstOrDefaultAsync();

                if (spool == null) continue;

                var newWeight = Math.Round(spool.TotalWeight * slot.Remain.Value / 100m, 1);

                if (Math.Abs(newWeight - spool.WeightRemaining) < 0.5m) continue;

                if (AmsAutoUpdateOnlyDecrease && newWeight > spool.WeightRemaining)
                {
                    logger.LogDebug(
                        "Printer {PrinterId}: AMS auto-update skipped (OnlyDecrease): spool {SpoolId} AMS says {New}g but inventory has {Old}g",
                        printerId, spool.Id, newWeight, spool.WeightRemaining);
                    continue;
                }

                var direction = newWeight < spool.WeightRemaining ? "▼ decreased" : "▲ increased";
                logger.LogInformation(
                    "Printer {PrinterId}: AMS auto-update: spool {SpoolId} ({Brand} {Color}) {Direction} {Old}g → {New}g (AMS {Pct}%)",
                    printerId, spool.Id, spool.Filament?.Brand, spool.Filament?.ColorName,
                    direction, spool.WeightRemaining, newWeight, slot.Remain.Value);

                spool.WeightRemaining = newWeight;
                anyChanged = true;
            }

            await context.SaveChangesAsync();

            if (anyChanged)
            {
                try { OnAmsWeightUpdated?.Invoke(printerId); }
                catch (Exception ex) { logger.LogWarning(ex, "A subscriber threw in OnAmsWeightUpdated for printer {PrinterId}", printerId); }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply AMS weight updates for printer {PrinterId}", printerId);
        }
    }

    private static string GetStringSafe(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.Null   => "",
            _                    => element.ToString()
        };
}

// DTOs — snake_case property names match the BambuLab MQTT JSON directly
public class AmsInfoDto
{
    public List<AmsUnitDto> Units { get; set; } = new();
}

public class AmsUnitDto
{
    public string AmsId       { get; init; } = "";
    public string Temp        { get; init; } = "";
    public string Humidity    { get; init; } = "";
    public string HumidityRaw { get; init; } = "";
    public List<AmsTrayDto> Trays { get; set; } = [];
}

public class AmsTrayDto
{
    public string Id            { get; init; } = "";
    public string TrayColor     { get; init; } = "";
    public string TrayType      { get; init; } = "";
    public string TraySubBrands { get; init; } = "";
    public string Remain        { get; init; } = "";
    public string TrayUuid      { get; init; } = "";
    public string TagUid        { get; init; } = "";
}

public class MqttLogEntry
{
    public DateTime Timestamp { get; init; }
    public string   Payload   { get; init; } = "";
    public string   Topic     { get; set; }  = "";
    public int?     PrinterId { get; set; }
    public string   PrinterName { get; set; } = "";
}

internal static class StringExtensions
{
    internal static string? NullIfEmpty(this string? s) =>
        string.IsNullOrEmpty(s) ? null : s;
}
