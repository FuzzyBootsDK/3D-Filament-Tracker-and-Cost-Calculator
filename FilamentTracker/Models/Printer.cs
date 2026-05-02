using System.ComponentModel.DataAnnotations;

namespace FilamentTracker.Models;

public class Printer
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = "My Printer";

    [Required]
    [MaxLength(50)]
    public string PrinterType { get; set; } = "BambuLab"; // Future: Creality, Prusa, etc.

    [Required]
    [MaxLength(200)]
    public string IpAddress { get; set; } = "";

    [Required]
    [MaxLength(20)]
    public string AccessCode { get; set; } = "";

    [Required]
    [MaxLength(100)]
    public string SerialNumber { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public bool IsDefault { get; set; } = false; // Primary printer for live view

    public DateTime DateAdded { get; set; } = DateTime.UtcNow;

    // Optional: Printer-specific settings
    [MaxLength(50)]
    public string? Location { get; set; } // "Office", "Garage", etc.

    [MaxLength(7)]
    public string? ColorHex { get; set; } = "#3b82f6"; // Visual identifier in UI
}
