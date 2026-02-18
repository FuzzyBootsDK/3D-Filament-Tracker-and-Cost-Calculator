using System.ComponentModel.DataAnnotations;

namespace FilamentTracker.Models;

public class Filament
{
    public int Id { get; set; }
    
    [Required]
    public string Brand { get; set; } = string.Empty;
    
    [Required]
    public string Type { get; set; } = string.Empty;
    
    public string? Finish { get; set; }
    
    [Required]
    public string ColorName { get; set; } = string.Empty;
    
    [Required]
    public string ColorCode { get; set; } = "#000000";
    
    public decimal Diameter { get; set; } = 1.75m;
    
    public decimal? PricePerKg { get; set; }
    
    public string? Location { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime DateAdded { get; set; } = DateTime.Now;
    
    // Navigation property
    public ICollection<Spool> Spools { get; set; } = new List<Spool>();
    
    // Computed properties
    public decimal TotalWeight => Spools.Sum(s => s.TotalWeight);
    public decimal WeightRemaining => Spools.Sum(s => s.WeightRemaining);
    public decimal PercentRemaining => TotalWeight > 0 ? (WeightRemaining / TotalWeight) * 100 : 0;
    public int SpoolCount => Spools.Count;
    
    // Calculated average price from all spools with prices, or manual override
    public decimal CalculatedPricePerKg
    {
        get
        {
            // If manual price is set, use it
            if (PricePerKg.HasValue)
                return PricePerKg.Value;
            
            // Otherwise, calculate weighted average from spools
            var spoolsWithPrices = Spools.Where(s => s.PurchasePricePerKg.HasValue).ToList();
            if (spoolsWithPrices.Any())
            {
                return spoolsWithPrices.Average(s => s.PurchasePricePerKg!.Value);
            }
            
            // Default fallback
            return 149m;
        }
    }
    
    public string Status
    {
        get
        {
            if (WeightRemaining < 250) return "critical";
            if (WeightRemaining < 500) return "low";
            return "ok";
        }
    }
}
