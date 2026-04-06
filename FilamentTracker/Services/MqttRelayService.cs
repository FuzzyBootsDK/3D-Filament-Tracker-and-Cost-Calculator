using System.Text;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace FilamentTracker.Services;

/// <summary>
/// MQTT Relay Service that rebroadcasts messages from BambuLab printer
/// to other clients (like ESP32) to reduce direct connections to the printer.
/// </summary>
public class MqttRelayService : IAsyncDisposable
{
    private readonly ILogger<MqttRelayService> _logger;
    private readonly BambuLabService _bambuLabService;
    private MqttServer? _mqttServer;
    private bool _isServerRunning;
    private string? _relayUsername;
    private string? _relayPassword;

    public MqttRelayService(ILogger<MqttRelayService> logger, BambuLabService bambuLabService)
    {
        _logger = logger;
        _bambuLabService = bambuLabService;
        
        // Subscribe to BambuLab messages to relay them
        _bambuLabService.OnMqttMessageLogged += OnBambuLabMessage;
    }

    public bool IsServerRunning => _isServerRunning;
    public int RelayPort { get; private set; } = 1883; // Default MQTT port

    /// <summary>
    /// Starts the MQTT relay server that rebroadcasts BambuLab messages
    /// </summary>
    public async Task StartRelayServerAsync(int port = 1883, string? username = null, string? password = null)
    {
        try
        {
            if (_isServerRunning)
            {
                _logger.LogWarning("MQTT Relay Server is already running");
                return;
            }

            RelayPort = port;
            _relayUsername = username;
            _relayPassword = password;

            var factory = new MqttServerFactory();
            var options = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointPort(port)
                .Build();

            _mqttServer = factory.CreateMqttServer(options);

            // Set up authentication handler
            _mqttServer.ValidatingConnectionAsync += ValidateConnection;
            _mqttServer.ClientConnectedAsync += OnClientConnected;
            _mqttServer.ClientDisconnectedAsync += OnClientDisconnected;
            _mqttServer.ClientSubscribedTopicAsync += OnClientSubscribed;

            await _mqttServer.StartAsync();
            _isServerRunning = true;
            
            _logger.LogInformation("MQTT Relay Server started on port {Port}", port);
            _logger.LogInformation("ESP32 and other clients can now connect to this server instead of the printer");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MQTT Relay Server on port {Port}", port);
            throw;
        }
    }

    /// <summary>
    /// Stops the MQTT relay server
    /// </summary>
    public async Task StopRelayServerAsync()
    {
        try
        {
            if (_mqttServer != null && _isServerRunning)
            {
                await _mqttServer.StopAsync();
                _isServerRunning = false;
                _logger.LogInformation("MQTT Relay Server stopped");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping MQTT Relay Server");
        }
    }

    /// <summary>
    /// Called when BambuLab service receives a message - rebroadcast it to all connected clients
    /// </summary>
    private void OnBambuLabMessage(MqttLogEntry entry)
    {
        if (!_isServerRunning || _mqttServer == null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                // Get the serial number from the current BambuLab connection to maintain topic structure
                var status = _bambuLabService.GetCurrentStatus();
                var serialNumber = ExtractSerialNumber(status.PrinterName);
                
                var topic = !string.IsNullOrEmpty(serialNumber) 
                    ? $"device/{serialNumber}/report" 
                    : "bambulab/report";

                // Use the raw bytes directly from the original MQTT message
                // This ensures byte-for-byte identical relay with no encoding round-trip
                var payloadBytes = entry.PayloadBytes.Length > 0 
                    ? entry.PayloadBytes 
                    : Encoding.UTF8.GetBytes(entry.Payload);  // Fallback for old entries

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payloadBytes)  // Exact bytes from printer, no re-encoding
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                    .WithRetainFlag(false)
                    .Build();

                await _mqttServer.InjectApplicationMessage(
                    new InjectedMqttApplicationMessage(message)
                    {
                        SenderClientId = "FilamentTracker_Relay"
                    });

                _logger.LogDebug("Relayed message to {Clients} client(s) on topic {Topic}", 
                    _mqttServer.GetClientsAsync().Result.Count, topic);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to relay MQTT message");
            }
        });
    }

    private string? ExtractSerialNumber(string printerName)
    {
        // Extract serial from format "BambuLab (SERIALNUMBER)"
        if (string.IsNullOrEmpty(printerName)) return null;
        
        var start = printerName.IndexOf('(');
        var end = printerName.IndexOf(')');
        
        if (start >= 0 && end > start)
        {
            return printerName.Substring(start + 1, end - start - 1);
        }
        
        return null;
    }

    private Task ValidateConnection(ValidatingConnectionEventArgs args)
    {
        // Optional authentication
        if (!string.IsNullOrEmpty(_relayUsername) && !string.IsNullOrEmpty(_relayPassword))
        {
            if (args.UserName != _relayUsername || args.Password != _relayPassword)
            {
                args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                _logger.LogWarning("MQTT Relay: Client authentication failed for user {User}", args.UserName);
            }
            else
            {
                args.ReasonCode = MqttConnectReasonCode.Success;
                _logger.LogInformation("MQTT Relay: Client {ClientId} authenticated successfully", args.ClientId);
            }
        }
        else
        {
            // No authentication required
            args.ReasonCode = MqttConnectReasonCode.Success;
        }

        return Task.CompletedTask;
    }

    private Task OnClientConnected(ClientConnectedEventArgs args)
    {
        _logger.LogInformation("MQTT Relay: Client {ClientId} connected from {Endpoint}", 
            args.ClientId, args.Endpoint);
        return Task.CompletedTask;
    }

    private Task OnClientDisconnected(ClientDisconnectedEventArgs args)
    {
        _logger.LogInformation("MQTT Relay: Client {ClientId} disconnected: {Type}", 
            args.ClientId, args.DisconnectType);
        return Task.CompletedTask;
    }

    private Task OnClientSubscribed(ClientSubscribedTopicEventArgs args)
    {
        _logger.LogInformation("MQTT Relay: Client {ClientId} subscribed to topic {Topic}", 
            args.ClientId, args.TopicFilter.Topic);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _bambuLabService.OnMqttMessageLogged -= OnBambuLabMessage;

        if (_mqttServer != null)
        {
            _mqttServer.ValidatingConnectionAsync -= ValidateConnection;
            _mqttServer.ClientConnectedAsync -= OnClientConnected;
            _mqttServer.ClientDisconnectedAsync -= OnClientDisconnected;
            _mqttServer.ClientSubscribedTopicAsync -= OnClientSubscribed;

            await StopRelayServerAsync();
            _mqttServer.Dispose();
        }
    }
}
