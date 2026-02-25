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

    public event Action<PrintStatus>? OnStatusUpdated;
    // AMS updates event
    public event Action<List<FilamentTracker.Models.PrinterAmsDevice>>? OnAmsUpdated;

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
                OnStatusUpdated?.Invoke(_currentStatus);
            };

            _mqttClient.DisconnectedAsync += async e =>
            {
                _logger.LogWarning("MQTT disconnected: {Reason}", e.Reason);
                _currentStatus.IsConnected = false;
                OnStatusUpdated?.Invoke(_currentStatus);

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

            // Parse BambuLab JSON status
            var json = JsonDocument.Parse(payload);
            var root = json.RootElement;

            // Parse AMS information if present
            try
            {
                if (root.TryGetProperty("ams", out var amsProp) && amsProp.ValueKind == JsonValueKind.Array)
                {
                    var devices = new List<FilamentTracker.Models.PrinterAmsDevice>();
                    foreach (var devElem in amsProp.EnumerateArray())
                    {
                        try
                        {
                            var dev = new FilamentTracker.Models.PrinterAmsDevice();
                            if (devElem.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                                dev.Id = idProp.GetInt32();
                            if (devElem.TryGetProperty("active_slot", out var activeProp) && activeProp.ValueKind == JsonValueKind.Number)
                                dev.ActiveSlot = activeProp.GetInt32();

                            if (devElem.TryGetProperty("slots", out var slotsProp) && slotsProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var slotElem in slotsProp.EnumerateArray())
                                {
                                    var slot = new FilamentTracker.Models.PrinterAmsSlot();
                                    if (slotElem.TryGetProperty("slot", out var slotNum) && slotNum.ValueKind == JsonValueKind.Number)
                                        slot.Slot = slotNum.GetInt32();
                                    if (slotElem.TryGetProperty("material", out var mat) && mat.ValueKind == JsonValueKind.String)
                                        slot.Material = mat.GetString();
                                    if (slotElem.TryGetProperty("brand", out var brand) && brand.ValueKind == JsonValueKind.String)
                                        slot.Brand = brand.GetString();
                                    if (slotElem.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String)
                                        slot.Type = type.GetString();
                                    if (slotElem.TryGetProperty("color", out var color) && color.ValueKind == JsonValueKind.String)
                                        slot.Color = color.GetString();
                                    if (slotElem.TryGetProperty("humidity", out var hum) && hum.ValueKind == JsonValueKind.Number)
                                        slot.Humidity = hum.GetInt32();
                                    if (slotElem.TryGetProperty("temp", out var temp) && temp.ValueKind == JsonValueKind.Number)
                                        slot.Temp = temp.GetInt32();
                                    // printer-provided id for the spool
                                    if (slotElem.TryGetProperty("id", out var extId) && extId.ValueKind == JsonValueKind.String)
                                        slot.ExternalId = extId.GetString();

                                    dev.Slots.Add(slot);
                                }
                            }

                            devices.Add(dev);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed parsing one AMS device element");
                        }
                    }

                    if (devices.Count > 0)
                    {
                        try
                        {
                            OnAmsUpdated?.Invoke(devices);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error delivering AMS update event");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error parsing AMS payload");
            }

            if (root.TryGetProperty("print", out var print))
            {
                // Update print status
                if (print.TryGetProperty("gcode_state", out var gcodeState))
                {
                    var state = gcodeState.GetString()?.ToLower() ?? "idle";
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
                if (print.TryGetProperty("mc_remaining_time", out var remaining) && _currentStatus.Progress > 0)
                {
                    var remainingMin = remaining.GetInt32();
                    var totalMin = remainingMin * 100 / (100 - _currentStatus.Progress);
                    var elapsedMin = totalMin - remainingMin;
                    _currentStatus.TimeElapsed = FormatTime(elapsedMin);
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
            }

            // Notify subscribers
            OnStatusUpdated?.Invoke(_currentStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing MQTT message");
        }

        return Task.CompletedTask;
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

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _mqttClient?.Dispose();
    }
}
