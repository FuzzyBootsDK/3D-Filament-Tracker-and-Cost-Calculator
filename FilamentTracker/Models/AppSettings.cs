using System.ComponentModel.DataAnnotations;

namespace FilamentTracker.Models;

public class AppSettings
{
    public int Id { get; set; }
    
    [Required]
    public decimal LowThreshold { get; set; } = 500;
    
    [Required]
    public decimal CriticalThreshold { get; set; } = 250;
    
    [Required]
    [MaxLength(10)]
    public string Currency { get; set; } = "DKK";
    
    // BambuLab MQTT Settings
    [MaxLength(200)]
    public string? BambuLabIpAddress { get; set; }
    
    [MaxLength(20)]
    public string? BambuLabAccessCode { get; set; }
    
    [MaxLength(100)]
    public string? BambuLabSerialNumber { get; set; }
    
    public bool BambuLabEnabled { get; set; } = false;
}
