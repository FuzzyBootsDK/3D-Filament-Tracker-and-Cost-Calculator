using System.ComponentModel.DataAnnotations;

namespace FilamentTracker.Models;

public class Spool
{
    public int Id { get; init; }

    [Required] public int FilamentId { get; set; }

    public Filament? Filament { get; init; }

    [Required] public decimal TotalWeight { get; set; } = 1000m;

    [Required] public decimal WeightRemaining { get; set; } = 1000m;

    [Required] public bool IsRefill { get; set; }

    public string? SpoolMaterial { get; set; } // plastic, cardboard, reusable, none

    public bool IsReusable { get; set; }

    public decimal? PurchasePricePerKg { get; set; }

    // BambuLab AMS RFID identifiers — set when the user links this spool to an AMS slot
    public string? AmsTrayUuid { get; set; }
    public string? AmsTagUid { get; set; }

    public DateTime DateAdded { get; init; } = DateTime.Now;

    public DateTime? DateEmptied { get; set; }

    public bool IsEmpty => WeightRemaining <= 0 || DateEmptied.HasValue;

    public decimal PercentRemaining => TotalWeight > 0 ? WeightRemaining / TotalWeight * 100 : 0;
}