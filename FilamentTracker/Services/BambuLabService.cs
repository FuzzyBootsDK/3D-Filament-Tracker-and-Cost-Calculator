using MQTTnet;
using System.Text;
using System.Text.Json;
using System.Buffers;
using System.Security.Cryptography.X509Certificates;
using FilamentTracker.Models;

namespace FilamentTracker.Services;

public class BambuLabService : IAsyncDisposable
{
    private readonly ILogger<BambuLabService> _logger;
    private IMqttClient? _mqttClient;
    private PrintStatus _currentStatus = new();
    private string? _ipAddress;
    private string? _accessCode;
    private string? _serialNumber;

    private AMSInfoDto? _amsInfo;
    private AMSInfoDto? _lastAmsInfo;

    // MQTT terminal log — last 50 raw messages
    private readonly System.Collections.Generic.Queue<MqttLogEntry> _mqttLog = new();
    private const int MaxLogEntries = 50;

    public event Action<PrintStatus>? OnStatusUpdated;
    public event Action<MqttLogEntry>? OnMqttMessageLogged;

    public BambuLabService(ILogger<BambuLabService> logger)
    {
        _logger = logger;
    }

    public PrintStatus GetCurrentStatus() => _currentStatus;

    public bool IsConnected => _mqttClient?.IsConnected ?? false;

    public async Task ConnectAsync(string ipAddress, string accessCode, string serialNumber)
    {
        try
        {
            _ipAddress = ipAddress;
            _accessCode = accessCode;
            _serialNumber = serialNumber;

            // Disconnect existing connection if any
            if (_mqttClient?.IsConnected == true)
            {
                await DisconnectAsync();
            }

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
                    o.WithAllowUntrustedCertificates(true);
                    o.WithIgnoreCertificateChainErrors(true);
                    o.WithIgnoreCertificateRevocationErrors(true);
                    o.WithCertificateValidationHandler(context =>
                    {
                        // Accept all certificates (BambuLab uses self-signed)
                        return true;
                    });
                })
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(15))
                .WithCleanSession(true)
                .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311) // Use MQTT 3.1.1
                .Build();

            // Handle incoming messages
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;

            // Handle connection events
            _mqttClient.ConnectedAsync += async e =>
            {
                _logger.LogInformation("MQTT connected successfully to {IpAddress}", ipAddress);
                _currentStatus.IsConnected = true;
                _currentStatus.PrinterName = $"BambuLab ({serialNumber})";
                try { OnStatusUpdated?.Invoke(_currentStatus); }
                catch (Exception ex) { _logger.LogWarning(ex, "A subscriber threw in OnStatusUpdated (connected)"); }
            };

            _mqttClient.DisconnectedAsync += async e =>
            {
                _logger.LogWarning("MQTT disconnected: {Reason}", e.Reason);
                _currentStatus.IsConnected = false;
                try { OnStatusUpdated?.Invoke(_currentStatus); }
                catch (Exception ex) { _logger.LogWarning(ex, "A subscriber threw in OnStatusUpdated (disconnected)"); }

                // Auto-reconnect after 5 seconds if not manually disconnected
                if (e.Reason != MqttClientDisconnectReason.NormalDisconnection)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    try
                    {
                        if (_mqttClient != null && !_mqttClient.IsConnected)
                        {
                            _logger.LogInformation("Attempting to reconnect...");
                            await _mqttClient.ConnectAsync(options);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to reconnect to MQTT");
                    }
                }
            };

            // Connect
            _logger.LogInformation("Connecting to BambuLab printer at {IpAddress}:8883 with serial {Serial}", 
                ipAddress, serialNumber);
            
            var connectResult = await _mqttClient.ConnectAsync(options);
            
            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                _logger.LogError("Failed to connect: {ResultCode}", connectResult.ResultCode);
                throw new Exception($"MQTT connection failed: {connectResult.ResultCode} - {connectResult.ReasonString}");
            }

            _logger.LogInformation("Connected successfully. Result: {ResultCode}", connectResult.ResultCode);

            // Subscribe to printer status topic
            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic($"device/{serialNumber}/report"))
                .Build();

            var subscribeResult = await _mqttClient.SubscribeAsync(subscribeOptions);
            
            var firstItem = subscribeResult.Items.FirstOrDefault();
            _logger.LogInformation("Subscribed to topic: device/{Serial}/report. Result: {ResultCode}", 
                serialNumber, firstItem?.ResultCode);

            _currentStatus.IsConnected = true;
            _currentStatus.PrinterName = $"BambuLab ({serialNumber})";
            OnStatusUpdated?.Invoke(_currentStatus);

            _logger.LogInformation("Connected to BambuLab printer at {IpAddress}", ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to BambuLab MQTT");
            _currentStatus.IsConnected = false;
            OnStatusUpdated?.Invoke(_currentStatus);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (_mqttClient?.IsConnected == true)
            {
                await _mqttClient.DisconnectAsync();
            }

            _currentStatus.IsConnected = false;
            _currentStatus.IsPrinting = false;
            OnStatusUpdated?.Invoke(_currentStatus);

            _logger.LogInformation("Disconnected from BambuLab printer");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from MQTT");
        }
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            // MQTTnet 5.x uses ReadOnlySequence<byte> for Payload
            var payloadBytes = e.ApplicationMessage.Payload.ToArray();
            var payload = Encoding.UTF8.GetString(payloadBytes);
            _logger.LogDebug("Received MQTT message: {Payload}", payload);

            // Store in rolling log
            var entry = new MqttLogEntry { Timestamp = DateTime.Now, Payload = payload };
            lock (_mqttLog)
            {
                _mqttLog.Enqueue(entry);
                while (_mqttLog.Count > MaxLogEntries)
                    _mqttLog.Dequeue();
            }
            try { OnMqttMessageLogged?.Invoke(entry); }
            catch (Exception ex) { _logger.LogWarning(ex, "A subscriber threw in OnMqttMessageLogged"); }

            // Parse BambuLab JSON status
            var json = JsonDocument.Parse(payload);
            var root = json.RootElement;

            // AMS info parsing
            if (root.TryGetProperty("print", out var print))
            {
                if (print.TryGetProperty("ams", out var ams))
                {
                    if (ams.TryGetProperty("ams", out var amsArray))
                    {
                        var amsInfoDto = new AMSInfoDto();
                        foreach (var amsObj in amsArray.EnumerateArray())
                        {
                            var amsDto = new AMSDto
                            {
                                ams_id = amsObj.TryGetProperty("ams_id", out var amsIdProp) ? GetStringSafe(amsIdProp) : "",
                                temp = amsObj.TryGetProperty("temp", out var tempProp) ? GetStringSafe(tempProp) : "",
                                humidity = amsObj.TryGetProperty("humidity", out var humProp) ? GetStringSafe(humProp) : "",
                            };
                            if (amsObj.TryGetProperty("tray", out var trayArray))
                            {
                                foreach (var trayObj in trayArray.EnumerateArray())
                                {
                                    var trayDto = new AMSTrayDto
                                    {
                                        id = trayObj.TryGetProperty("id", out var idProp) ? GetStringSafe(idProp) : "",
                                        tray_color = trayObj.TryGetProperty("tray_color", out var colorProp) ? GetStringSafe(colorProp) : "",
                                        tray_type = trayObj.TryGetProperty("tray_type", out var typeProp) ? GetStringSafe(typeProp) : "",
                                        tray_sub_brands = trayObj.TryGetProperty("tray_sub_brands", out var subProp) ? GetStringSafe(subProp) : "",
                                        remain = trayObj.TryGetProperty("remain", out var remainProp) ? GetStringSafe(remainProp) : "",
                                    };
                                    amsDto.tray.Add(trayDto);
                                }
                            }
                            amsInfoDto.Ams.Add(amsDto);
                        }
                        _amsInfo = amsInfoDto;

                        // Map parsed AMS info into _currentStatus.AMSUnits so the UI sees it
                        var amsUnits = new List<AMSUnit>();
                        foreach (var amsDto in amsInfoDto.Ams)
                        {
                            double.TryParse(amsDto.temp, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var tempVal);
                            int.TryParse(amsDto.humidity, out var humVal);

                            var unit = new AMSUnit
                            {
                                AMSId = amsDto.ams_id,
                                Temperature = tempVal > 0 ? tempVal : null,
                                Humidity = humVal > 0 ? humVal : null,
                            };
                            foreach (var trayDto in amsDto.tray)
                            {
                                int.TryParse(trayDto.id, out var trayIdInt);
                                int.TryParse(trayDto.remain, out var remainInt);
                                var colorHex = !string.IsNullOrEmpty(trayDto.tray_color) && trayDto.tray_color.Length >= 6
                                    ? "#" + trayDto.tray_color[..6]
                                    : null;
                                unit.Slots.Add(new AMSSlot
                                {
                                    Id = trayIdInt,
                                    Index = trayIdInt + 1,
                                    Remain = remainInt > 0 ? remainInt : null,
                                    TrayType = string.IsNullOrEmpty(trayDto.tray_type) ? null : trayDto.tray_type,
                                    TrayColor = trayDto.tray_color,
                                    TraySubBrands = string.IsNullOrEmpty(trayDto.tray_sub_brands) ? null : trayDto.tray_sub_brands,
                                    ColorHex = colorHex,
                                });
                            }
                            amsUnits.Add(unit);
                        }
                        _currentStatus.AMSUnits = amsUnits;
                        _logger.LogInformation("AMS data parsed: {Count} unit(s)", amsUnits.Count);
                    }
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
                {
                    _currentStatus.Progress = mcPercent.GetInt32();
                }

                // Layer info
                if (print.TryGetProperty("layer_num", out var layerNum))
                {
                    _currentStatus.CurrentLayer = layerNum.GetInt32();
                }

                if (print.TryGetProperty("total_layer_num", out var totalLayerNum))
                {
                    _currentStatus.TotalLayers = totalLayerNum.GetInt32();
                }

                // Time info (in minutes)
                if (print.TryGetProperty("mc_remaining_time", out var remainingTime))
                {
                    var minutes = remainingTime.GetInt32();
                    _currentStatus.TimeRemaining = FormatTime(minutes);
                }

                // Calculate elapsed time from progress and remaining
                if (print.TryGetProperty("mc_remaining_time", out var remaining) && _currentStatus.Progress > 0 && _currentStatus.Progress < 100)
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
                {
                    _currentStatus.CurrentFile = gcodeFile.GetString();
                }

                // Temperatures
                if (print.TryGetProperty("bed_temper", out var bedTemp))
                {
                    _currentStatus.BedTemperature = (int)bedTemp.GetDouble();
                }

                if (print.TryGetProperty("nozzle_temper", out var nozzleTemp))
                {
                    _currentStatus.NozzleTemperature = (int)nozzleTemp.GetDouble();
                }

                // WiFi signal (e.g. "-59dBm")
                if (print.TryGetProperty("wifi_signal", out var wifiSignal))
                {
                    var sig = GetStringSafe(wifiSignal);
                    if (!string.IsNullOrEmpty(sig))
                        _currentStatus.WifiSignal = sig;
                }
            }

            // Notify subscribers
            try { OnStatusUpdated?.Invoke(_currentStatus); }
            catch (Exception ex) { _logger.LogWarning(ex, "A subscriber threw in OnStatusUpdated"); }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing MQTT message");
        }
        return Task.CompletedTask;
    }

    private static bool AMSInfoEquals(AMSInfoDto? a, AMSInfoDto? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Ams.Count != b.Ams.Count) return false;
        for (int i = 0; i < a.Ams.Count; i++)
        {
            var amsA = a.Ams[i];
            var amsB = b.Ams[i];
            if (amsA.ams_id != amsB.ams_id) return false;
            if (amsA.tray.Count != amsB.tray.Count) return false;
            for (int j = 0; j < amsA.tray.Count; j++)
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
    /// Returns all AMS slots across all AMS units from the last received message.
    /// Returns empty list (not a hardcoded stub) when no data has been received yet.
    /// </summary>
    public IReadOnlyList<MqttLogEntry> GetMqttLog()
    {
        lock (_mqttLog)
            return _mqttLog.ToList();
    }

    public List<AMSSlot> GetAMSSlots()
    {
        if (_currentStatus.AMSUnits == null || _currentStatus.AMSUnits.Count == 0)
            return new List<AMSSlot>();

        return _currentStatus.AMSUnits.SelectMany(u => u.Slots).ToList();
    }

    public AMSInfoDto? GetAMSInfo()
    {
        return _amsInfo;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _mqttClient?.Dispose();
    }

    // Utility to safely get string from JsonElement
    private string GetStringSafe(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                return element.GetRawText(); // Handles numbers as string
            case JsonValueKind.Null:
                return "";
            default:
                return element.ToString();
        }
    }

    private string GetStringOrNumber(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            _ => string.Empty
        };
    }

    private int GetIntOrString(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
            return element.GetInt32();
        if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var val))
            return val;
        return 0;
    }
}

// DTOs for AMS info
public class AMSInfoDto
{
    public List<AMSDto> Ams { get; set; } = new();
}
public class AMSDto
{
    public string ams_id { get; set; }
    public string temp { get; set; }
    public string humidity { get; set; }
    public List<AMSTrayDto> tray { get; set; } = new();
}
public class AMSTrayDto
{
    public string id { get; set; }
    public string tray_color { get; set; }
    public string tray_type { get; set; }
    public string tray_sub_brands { get; set; }
    public string remain { get; set; }
    // Add other tray properties as needed
}

public class MqttLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Payload { get; set; } = "";
}

