using System.ComponentModel.DataAnnotations;

namespace FilamentTracker.Models;

public class ReusableSpool
{
    public int Id { get; init; }

    [Required] public string Material { get; init; } = "plastic"; // plastic, reusable, etc.

    public bool InUse { get; set; }

    public int? CurrentSpoolId { get; set; }

    public DateTime DateAdded { get; init; } = DateTime.Now;
}