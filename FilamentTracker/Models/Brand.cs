using System.ComponentModel.DataAnnotations;

namespace FilamentTracker.Models;

public class Brand
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public DateTime DateAdded { get; set; } = DateTime.Now;
}
