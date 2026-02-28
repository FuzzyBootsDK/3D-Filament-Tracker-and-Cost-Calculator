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
    private readonly Queue<MqttLogEntry> _mqttLog = new();
    private IMqttClient? _mqttClient;

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
    public event Action? OnAmsWeightUpdated;

    public PrintStatus GetCurrentStatus() => _currentStatus;

    public async Task ConnectAsync(string ipAddress, string accessCode, string serialNumber)
    {
        try
        {
            if (_mqttClient?.IsConnected == true) await DisconnectAsync();

            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(ipAddress, 8883)
                .WithCredentials("bblp", accessCode)
                .WithClientId($"FilamentTracker_{Guid.NewGuid():N}")
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

            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;

            _mqttClient.ConnectedAsync += _ =>
            {
                logger.LogInformation("MQTT connected successfully to {IpAddress}", ipAddress);
                _currentStatus.IsConnected = true;
                _currentStatus.PrinterName = $"BambuLab ({serialNumber})";
                try { OnStatusUpdated?.Invoke(_currentStatus); }
                catch (Exception ex) { logger.LogWarning(ex, "A subscriber threw in OnStatusUpdated (connected)"); }
                return Task.CompletedTask;
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

            logger.LogInformation("Connecting to BambuLab printer at {IpAddress}:8883 with serial {Serial}", ipAddress, serialNumber);
            var connectResult = await _mqttClient.ConnectAsync(options);

            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                logger.LogError("Failed to connect: {ResultCode}", connectResult.ResultCode);
                throw new Exception($"MQTT connection failed: {connectResult.ResultCode} - {connectResult.ReasonString}");
            }

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic($"device/{serialNumber}/report"))
                .Build();

            var subscribeResult = await _mqttClient.SubscribeAsync(subscribeOptions);
            logger.LogInformation("Subscribed to topic: device/{Serial}/report. Result: {ResultCode}",
                serialNumber, subscribeResult.Items.FirstOrDefault()?.ResultCode);

            _currentStatus.IsConnected = true;
            _currentStatus.PrinterName = $"BambuLab ({serialNumber})";
            OnStatusUpdated?.Invoke(_currentStatus);
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

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload.ToArray());
            logger.LogDebug("Received MQTT message: {Payload}", payload);

            var entry = new MqttLogEntry { Timestamp = DateTime.Now, Payload = payload };
            lock (_mqttLog)
            {
                _mqttLog.Enqueue(entry);
                while (_mqttLog.Count > MaxLogEntries)
                    _mqttLog.Dequeue();
            }

            // Append to server-side log file (best-effort)
            var logLine = $"[{entry.Timestamp:O}] {payload.Replace("\r\n", " ").Replace("\n", " ")}{Environment.NewLine}";
            _ = File.AppendAllTextAsync("bambulab-mqtt.log", logLine)
                    .ContinueWith(t => logger.LogDebug(t.Exception, "Log write failed"), TaskContinuationOptions.OnlyOnFaulted);

            try { OnMqttMessageLogged?.Invoke(entry); }
            catch (Exception ex) { logger.LogWarning(ex, "A subscriber threw in OnMqttMessageLogged"); }

            var root = JsonDocument.Parse(payload).RootElement;

            if (root.TryGetProperty("print", out var print))
            {
                if (print.TryGetProperty("ams", out var ams) &&
                    ams.TryGetProperty("ams", out var amsArray))
                {
                    var amsUnits = ParseAmsUnits(amsArray);
                    _currentStatus.AMSUnits = amsUnits;
                    logger.LogInformation("AMS data parsed: {Count} unit(s)", amsUnits.Count);

                    if (AmsAutoUpdateWeight)
                        _ = Task.Run(() => ApplyAmsWeightUpdatesAsync(amsUnits));
                }

                if (print.TryGetProperty("gcode_state", out var gcodeState))
                {
                    var state = GetStringSafe(gcodeState).ToLower();
                    if (string.IsNullOrEmpty(state)) state = "idle";
                    _currentStatus.Status = state;
                    _currentStatus.IsPrinting = state is "running" or "printing";
                }

                if (print.TryGetProperty("mc_percent", out var mcPercent))
                    _currentStatus.Progress = mcPercent.GetInt32();

                if (print.TryGetProperty("layer_num", out var layerNum))
                    _currentStatus.CurrentLayer = layerNum.GetInt32();

                if (print.TryGetProperty("total_layer_num", out var totalLayerNum))
                    _currentStatus.TotalLayers = totalLayerNum.GetInt32();

                if (print.TryGetProperty("mc_remaining_time", out var remainingTime))
                    _currentStatus.TimeRemaining = FormatTime(remainingTime.GetInt32());

                if (print.TryGetProperty("mc_remaining_time", out var remaining) && _currentStatus.Progress is > 0 and < 100)
                {
                    var remainingMin = remaining.GetInt32();
                    var totalMin = remainingMin * 100 / (100 - _currentStatus.Progress);
                    _currentStatus.TimeElapsed = FormatTime(totalMin - remainingMin);
                }
                else if (_currentStatus.Progress == 0)
                    _currentStatus.TimeElapsed = "0m";

                if (print.TryGetProperty("gcode_file", out var gcodeFile))
                    _currentStatus.CurrentFile = gcodeFile.GetString();

                if (print.TryGetProperty("bed_temper", out var bedTemp))
                    _currentStatus.BedTemperature = (int)bedTemp.GetDouble();

                if (print.TryGetProperty("nozzle_temper", out var nozzleTemp))
                    _currentStatus.NozzleTemperature = (int)nozzleTemp.GetDouble();

                if (print.TryGetProperty("wifi_signal", out var wifiSignal))
                {
                    var sig = GetStringSafe(wifiSignal);
                    if (!string.IsNullOrEmpty(sig))
                        _currentStatus.WifiSignal = sig;
                }
            }

            try { OnStatusUpdated?.Invoke(_currentStatus); }
            catch (Exception ex) { logger.LogWarning(ex, "A subscriber threw in OnStatusUpdated"); }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing MQTT message");
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

    private async Task ApplyAmsWeightUpdatesAsync(List<AMSUnit> amsUnits)
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

                var hasUuid = !string.IsNullOrEmpty(slot.TrayUuid) && slot.TrayUuid.Replace("0", "").Length > 0;
                var hasTag  = !string.IsNullOrEmpty(slot.TagUid)  && slot.TagUid.Replace("0", "").Length > 0;
                if (!hasUuid && !hasTag) continue;

                var spool = await context.Spools
                    .Include(s => s.Filament)
                    .Where(s => s.DateEmptied == null && s.WeightRemaining > 0)
                    .Where(s => (hasUuid && s.AmsTrayUuid == slot.TrayUuid) ||
                                (hasTag  && s.AmsTagUid   == slot.TagUid))
                    .FirstOrDefaultAsync();

                if (spool == null) continue;

                var newWeight = Math.Round(spool.TotalWeight * slot.Remain.Value / 100m, 1);

                if (Math.Abs(newWeight - spool.WeightRemaining) < 0.5m) continue;

                if (AmsAutoUpdateOnlyDecrease && newWeight > spool.WeightRemaining)
                {
                    logger.LogDebug(
                        "AMS auto-update skipped (OnlyDecrease): spool {SpoolId} AMS says {New}g but inventory has {Old}g",
                        spool.Id, newWeight, spool.WeightRemaining);
                    continue;
                }

                var direction = newWeight < spool.WeightRemaining ? "▼ decreased" : "▲ increased";
                logger.LogInformation(
                    "AMS auto-update: spool {SpoolId} ({Brand} {Color}) {Direction} {Old}g → {New}g (AMS {Pct}%)",
                    spool.Id, spool.Filament?.Brand, spool.Filament?.ColorName,
                    direction, spool.WeightRemaining, newWeight, slot.Remain.Value);

                spool.WeightRemaining = newWeight;
                anyChanged = true;
            }

            await context.SaveChangesAsync();

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
}

internal static class StringExtensions
{
    internal static string? NullIfEmpty(this string? s) =>
        string.IsNullOrEmpty(s) ? null : s;
}
