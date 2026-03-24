using System.ComponentModel.DataAnnotations;

namespace FilamentTracker.Models;

public class AppSettings
{
    public int Id { get; init; }

    [Required] public decimal LowThreshold { get; set; } = 500;

    [Required] public decimal CriticalThreshold { get; set; } = 250;

    [Required] [MaxLength(10)] public string Currency { get; set; } = "DKK";

    // BambuLab MQTT Settings
    [MaxLength(200)] public string? BambuLabIpAddress { get; set; }

    [MaxLength(20)] public string? BambuLabAccessCode { get; set; }

    [MaxLength(100)] public string? BambuLabSerialNumber { get; set; }

    public bool BambuLabEnabled { get; set; } = false;

    // When true, automatically update spool WeightRemaining from AMS remain% (only for tagged spools)
    public bool AmsAutoUpdateWeight { get; set; } = false;

    // When true, the auto-update only ever decreases weight (protects manually weighed values).
    // When false, AMS can also increase weight back (full two-way sync).
    public bool AmsAutoUpdateOnlyDecrease { get; set; } = true;

    // Timezone ID for ETA calculations (uses IANA timezone database, e.g., "Europe/Copenhagen")
    // Automatically handles daylight saving time transitions
    [MaxLength(100)]
    public string TimeZoneId { get; set; } = "Europe/Copenhagen";

    // MQTT Relay Settings (for ESP32 and other clients)
    public bool MqttRelayEnabled { get; set; } = false;

    public int MqttRelayPort { get; set; } = 1883;

    [MaxLength(50)]
    public string? MqttRelayUsername { get; set; }

    [MaxLength(50)]
    public string? MqttRelayPassword { get; set; }
}