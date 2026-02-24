using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using FilamentTracker.Data;
using FilamentTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace FilamentTracker.Services;

public class CsvService
{
    private readonly IDbContextFactory<FilamentContext> _contextFactory;
    
    public CsvService(IDbContextFactory<FilamentContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }
    
    public async Task<string> ExportToCsvAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var filaments = await context.Filaments
            .Include(f => f.Spools)
            .ToListAsync();
            
        var records = new List<CsvFilamentRecord>();
        
        foreach (var filament in filaments)
        {
            foreach (var spool in filament.Spools)
            {
                records.Add(new CsvFilamentRecord
                {
                    Brand = filament.Brand,
                    Type = filament.Type,
                    Finish = filament.Finish ?? "",
                    ColorName = filament.ColorName,
                    ColorCode = filament.ColorCode,
                    TotalWeight = spool.TotalWeight,
                    WeightRemaining = spool.WeightRemaining,
                    Quantity = 1,
                    SpoolType = spool.IsRefill ? "refill" : "spool",
                    SpoolMaterial = spool.SpoolMaterial ?? "none",
                    ReusableSpool = spool.IsReusable ? "Yes" : "No",
                    Diameter = filament.Diameter,
                    Location = filament.Location ?? "",
                    Notes = filament.Notes ?? "",
                    DateAdded = filament.DateAdded.ToString("MM/dd/yyyy"),
                    PurchasePricePerKg = spool.PurchasePricePerKg.HasValue
                        ? spool.PurchasePricePerKg.Value.ToString(CultureInfo.InvariantCulture)
                        : (filament.PricePerKg.HasValue ? filament.PricePerKg.Value.ToString(CultureInfo.InvariantCulture) : "")
                });
            }
        }
        
        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
        await csv.WriteRecordsAsync(records);
        return writer.ToString();
    }
    
    public async Task ImportFromCsvAsync(string csvContent)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var reader = new StringReader(csvContent);
        
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            // Don't throw if a column in the class is not in the CSV (e.g. old exports without price)
            HeaderValidated = null,
            MissingFieldFound = null,
        };
        
        using var csv = new CsvReader(reader, config);
        
        var records = csv.GetRecords<CsvFilamentRecord>().ToList();
        
        // Group by filament properties to combine spools
        var grouped = records.GroupBy(r => new
        {
            r.Brand,
            r.Type,
            r.Finish,
            r.ColorName,
            r.ColorCode,
            r.Diameter
        });
        
        foreach (var group in grouped)
        {
            var firstRecord = group.First();
            
            var filament = new Filament
            {
                Brand = group.Key.Brand,
                Type = group.Key.Type,
                Finish = string.IsNullOrWhiteSpace(group.Key.Finish) ? null : group.Key.Finish,
                ColorName = group.Key.ColorName,
                ColorCode = group.Key.ColorCode,
                Diameter = group.Key.Diameter,
                Location = firstRecord.Location,
                Notes = firstRecord.Notes,
                DateAdded = DateTime.TryParse(firstRecord.DateAdded, out var date) ? date : DateTime.Now
            };
            
            foreach (var record in group)
            {
                filament.Spools.Add(new Spool
                {
                    TotalWeight = record.TotalWeight,
                    WeightRemaining = record.WeightRemaining,
                    IsRefill = record.SpoolType.ToLower() == "refill",
                    SpoolMaterial = string.IsNullOrWhiteSpace(record.SpoolMaterial) ? null : record.SpoolMaterial,
                    IsReusable = record.ReusableSpool.ToLower() == "yes",
                    PurchasePricePerKg = decimal.TryParse(record.PurchasePricePerKg, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) && price > 0
                        ? price
                        : 149m,
                    DateAdded = filament.DateAdded
                });
            }
            
            context.Filaments.Add(filament);
        }
        
        await context.SaveChangesAsync();
        
        // Track reusable spools from import
        var allSpools = await context.Spools
            .Where(s => s.IsReusable && s.WeightRemaining > 0)
            .ToListAsync();
        
        foreach (var spool in allSpools)
        {
            // Check if this spool already has a ReusableSpool entry
            var exists = await context.ReusableSpools
                .AnyAsync(rs => rs.CurrentSpoolId == spool.Id);
            
            if (!exists)
            {
                var reusableSpool = new ReusableSpool
                {
                    Material = spool.SpoolMaterial ?? "plastic",
                    InUse = true,
                    CurrentSpoolId = spool.Id,
                    DateAdded = spool.DateAdded
                };
                context.ReusableSpools.Add(reusableSpool);
            }
        }
        
        await context.SaveChangesAsync();
    }
}

public class CsvFilamentRecord
{
    [CsvHelper.Configuration.Attributes.Name("Brand")]
    public string Brand { get; set; } = "";
    
    [CsvHelper.Configuration.Attributes.Name("Type")]
    public string Type { get; set; } = "";
    
    [CsvHelper.Configuration.Attributes.Name("Finish")]
    public string Finish { get; set; } = "";
    
    [CsvHelper.Configuration.Attributes.Name("Color Name")]
    public string ColorName { get; set; } = "";
    
    [CsvHelper.Configuration.Attributes.Name("Color Code")]
    public string ColorCode { get; set; } = "";
    
    [CsvHelper.Configuration.Attributes.Name("Total Weight (g)")]
    public decimal TotalWeight { get; set; }
    
    [CsvHelper.Configuration.Attributes.Name("Weight Remaining (g)")]
    public decimal WeightRemaining { get; set; }
    
    [CsvHelper.Configuration.Attributes.Name("Quantity")]
    public int Quantity { get; set; }
    
    [CsvHelper.Configuration.Attributes.Name("Spool Type")]
    public string SpoolType { get; set; } = "";
    
    [CsvHelper.Configuration.Attributes.Name("Spool Material")]
    public string SpoolMaterial { get; set; } = "";
    
    [CsvHelper.Configuration.Attributes.Name("Reusable Spool")]
    public string ReusableSpool { get; set; } = "";
    
    [CsvHelper.Configuration.Attributes.Name("Diameter (mm)")]
    public decimal Diameter { get; set; }
    
    [CsvHelper.Configuration.Attributes.Name("Location")]
    public string Location { get; set; } = "";
    
    [CsvHelper.Configuration.Attributes.Name("Notes")]
    public string Notes { get; set; } = "";
    
    [CsvHelper.Configuration.Attributes.Name("Date Added")]
    public string DateAdded { get; set; } = "";
    
    [CsvHelper.Configuration.Attributes.Name("Purchase Price Per Kg")]
    public string PurchasePricePerKg { get; set; } = "";
}
