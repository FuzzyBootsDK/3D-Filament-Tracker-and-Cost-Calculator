using System.ComponentModel.DataAnnotations;

namespace FilamentTracker.Models;

public class AppSettings
{
    public int Id { get; set; }
    
    [Required]
    public decimal LowThreshold { get; set; } = 500;
    
    [Required]
    public decimal CriticalThreshold { get; set; } = 250;
}
