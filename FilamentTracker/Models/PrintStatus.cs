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
}
