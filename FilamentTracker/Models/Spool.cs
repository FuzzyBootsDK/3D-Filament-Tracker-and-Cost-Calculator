using System.ComponentModel.DataAnnotations;

namespace FilamentTracker.Models;

public class Spool
{
    public int Id { get; set; }
    
    [Required]
    public int FilamentId { get; set; }
    
    public Filament? Filament { get; set; }
    
    [Required]
    public decimal TotalWeight { get; set; } = 1000m;
    
    [Required]
    public decimal WeightRemaining { get; set; } = 1000m;
    
    [Required]
    public bool IsRefill { get; set; }
    
    public string? SpoolMaterial { get; set; } // plastic, cardboard, reusable, none
    
    public bool IsReusable { get; set; }
    
    public DateTime DateAdded { get; set; } = DateTime.Now;
    
    public DateTime? DateEmptied { get; set; }
    
    public bool IsEmpty => WeightRemaining <= 0 || DateEmptied.HasValue;
    
    public decimal PercentRemaining => TotalWeight > 0 ? (WeightRemaining / TotalWeight) * 100 : 0;
}
