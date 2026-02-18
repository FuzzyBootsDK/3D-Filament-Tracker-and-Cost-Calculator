using System.ComponentModel.DataAnnotations;

namespace FilamentTracker.Models;

public class ReusableSpool
{
    public int Id { get; set; }
    
    [Required]
    public string Material { get; set; } = "plastic"; // plastic, reusable, etc.
    
    public bool InUse { get; set; }
    
    public int? CurrentSpoolId { get; set; }
    
    public DateTime DateAdded { get; set; } = DateTime.Now;
}
