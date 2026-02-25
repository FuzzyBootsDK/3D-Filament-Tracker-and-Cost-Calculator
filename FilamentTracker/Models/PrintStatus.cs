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
    public string AMSId { get; set; } = string.Empty;
    public string ChipId { get; set; } = string.Empty;
    public int? Humidity { get; set; }
    public double? Temperature { get; set; }
    public List<AMSSlot> Slots { get; set; } = new();
}

public class AMSSlot
{
    public int Id { get; set; }
    public int? State { get; set; }
    public int? Remain { get; set; }
    public string? TrayType { get; set; }
    public string? TrayColor { get; set; }
    public string? TrayIdName { get; set; }
    public string? TraySubBrands { get; set; }
    // Added properties for AMS integration
    public int? Index { get; set; }
    public string? ColorHex { get; set; }
}
