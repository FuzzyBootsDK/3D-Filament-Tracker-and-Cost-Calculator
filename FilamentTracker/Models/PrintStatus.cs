namespace FilamentTracker.Models;

public class PrintStatus
{
    public bool IsConnected { get; set; }
    public bool IsPrinting { get; set; }
    public int Progress { get; set; } // 0-100
    public string TimeElapsed { get; set; } = "0h 0m";
    public string TimeRemaining { get; set; } = "0h 0m";
    public int CurrentLayer { get; set; }
    public int TotalLayers { get; set; }
    public string PrinterName { get; set; } = "BambuLab Printer";
    public string? CurrentFile { get; set; }
    public int BedTemperature { get; set; }
    public int NozzleTemperature { get; set; }

    public string Status { get; set; } = "idle"; // idle, printing, paused, finished
    public string? WifiSignal { get; set; } // e.g. "-59dBm"

    // AMS info
    public List<AMSUnit>? AMSUnits { get; set; } = new();
}

public class AMSUnit
{
    public string AMSId { get; init; } = string.Empty;
    public string ChipId { get; set; } = string.Empty;
    public int? Humidity { get; init; }
    public double? Temperature { get; init; }
    public List<AMSSlot> Slots { get; set; } = [];
}

public class AMSSlot
{
    public int Id { get; init; }
    public int? State { get; set; }
    public int? Remain { get; init; }
    public string? TrayType { get; init; }
    public string? TrayColor { get; init; }
    public string? TrayIdName { get; set; }

    public string? TraySubBrands { get; init; }

    // Added properties for AMS integration
    public int? Index { get; init; }
    public string? ColorHex { get; init; }
    // RFID identifiers from BambuLab AMS (used for auto-matching to inventory)
    public string? TrayUuid { get; init; }
    public string? TagUid { get; init; }
}