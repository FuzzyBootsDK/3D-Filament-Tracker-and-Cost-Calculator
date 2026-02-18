using Microsoft.EntityFrameworkCore;
using FilamentTracker.Data;
using FilamentTracker.Models;

namespace FilamentTracker.Services;

public class FilamentService
{
    private readonly IDbContextFactory<FilamentContext> _contextFactory;
    private readonly ThresholdService _thresholdService;
    
    public FilamentService(IDbContextFactory<FilamentContext> contextFactory, ThresholdService thresholdService)
    {
        _contextFactory = contextFactory;
        _thresholdService = thresholdService;
    }
    
    public async Task<List<Filament>> GetAllFilamentsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Filaments
            .Include(f => f.Spools.Where(s => s.WeightRemaining > 0 && s.DateEmptied == null))
            .OrderByDescending(f => f.DateAdded)
            .ToListAsync();
    }
    
    public async Task<Filament?> GetFilamentByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Filaments
            .Include(f => f.Spools)
            .FirstOrDefaultAsync(f => f.Id == id);
    }
    
    public async Task<Filament> AddFilamentAsync(Filament filament)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.Filaments.Add(filament);
        await context.SaveChangesAsync();
        
        // Track reusable spools
        foreach (var spool in filament.Spools.Where(s => s.IsReusable && s.WeightRemaining > 0))
        {
            var reusableSpool = new ReusableSpool
            {
                Material = spool.SpoolMaterial ?? "plastic",
                InUse = true,
                CurrentSpoolId = spool.Id,
                DateAdded = DateTime.Now
            };
            context.ReusableSpools.Add(reusableSpool);
        }
        await context.SaveChangesAsync();
        
        return filament;
    }
    
    public async Task UpdateFilamentAsync(Filament filament)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.Filaments.Update(filament);
        await context.SaveChangesAsync();
    }
    
    public async Task DeleteFilamentAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var filament = await context.Filaments.FindAsync(id);
        if (filament != null)
        {
            context.Filaments.Remove(filament);
            await context.SaveChangesAsync();
        }
    }
    
    public async Task<Spool> AddSpoolAsync(Spool spool)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.Spools.Add(spool);
        await context.SaveChangesAsync();
        return spool;
    }
    
    public async Task<Spool> AddSpoolToFilamentAsync(int filamentId, Spool spool)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        spool.FilamentId = filamentId;
        context.Spools.Add(spool);
        await context.SaveChangesAsync();
        
        // Track reusable spool if applicable
        if (spool.IsReusable && spool.WeightRemaining > 0)
        {
            var reusableSpool = new ReusableSpool
            {
                Material = spool.SpoolMaterial ?? "plastic",
                InUse = true,
                CurrentSpoolId = spool.Id,
                DateAdded = DateTime.Now
            };
            context.ReusableSpools.Add(reusableSpool);
            await context.SaveChangesAsync();
        }
        
        return spool;
    }
    
    public async Task UpdateSpoolAsync(Spool spool)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.Spools.Update(spool);
        await context.SaveChangesAsync();
        
        // Update reusable spool tracking if this is a reusable spool
        if (spool.IsReusable)
        {
            var reusableSpool = await context.ReusableSpools
                .FirstOrDefaultAsync(rs => rs.CurrentSpoolId == spool.Id);
            
            if (reusableSpool != null)
            {
                // If spool is now empty, mark reusable spool as available
                if (spool.WeightRemaining <= 0 || spool.DateEmptied.HasValue)
                {
                    reusableSpool.InUse = false;
                    reusableSpool.CurrentSpoolId = null;
                    context.ReusableSpools.Update(reusableSpool);
                    await context.SaveChangesAsync();
                }
            }
        }
    }
    
    public async Task DeleteSpoolAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var spool = await context.Spools.FindAsync(id);
        if (spool != null)
        {
            context.Spools.Remove(spool);
            await context.SaveChangesAsync();
        }
    }
    
    public async Task<Dictionary<string, int>> GetStatisticsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var allFilaments = await context.Filaments
            .Include(f => f.Spools.Where(s => s.WeightRemaining > 0 && s.DateEmptied == null))
            .ToListAsync();
            
        var stats = new Dictionary<string, int>
        {
            ["Total"] = allFilaments.Count,
            ["Low"] = 0,
            ["Critical"] = 0,
            ["Ok"] = 0
        };
        
        foreach (var filament in allFilaments)
        {
            var status = _thresholdService.GetStatus(filament.WeightRemaining);
            stats[status == "critical" ? "Critical" : (status == "low" ? "Low" : "Ok")]++;
        }
        
        return stats;
    }
    
    public async Task<List<ReusableSpool>> GetReusableSpoolsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ReusableSpools.ToListAsync();
    }
    
    public async Task<ReusableSpool> AddReusableSpoolAsync(ReusableSpool spool)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.ReusableSpools.Add(spool);
        await context.SaveChangesAsync();
        return spool;
    }
    
    public async Task UpdateReusableSpoolAsync(ReusableSpool spool)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.ReusableSpools.Update(spool);
        await context.SaveChangesAsync();
    }
    
    public async Task DeleteReusableSpoolAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var spool = await context.ReusableSpools.FindAsync(id);
        if (spool != null)
        {
            context.ReusableSpools.Remove(spool);
            await context.SaveChangesAsync();
        }
    }
    
    public async Task PurgeDatabaseAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.Filaments.RemoveRange(context.Filaments);
        context.Spools.RemoveRange(context.Spools);
        await context.SaveChangesAsync();
    }
    
    // Brand management methods
    public async Task<List<Brand>> GetBrandsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Brands.OrderBy(b => b.Name).ToListAsync();
    }
    
    public async Task<Brand> AddBrandAsync(string brandName)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Check if brand already exists
        var existing = await context.Brands.FirstOrDefaultAsync(b => b.Name == brandName);
        if (existing != null)
            return existing;
        
        var brand = new Brand { Name = brandName };
        context.Brands.Add(brand);
        await context.SaveChangesAsync();
        return brand;
    }
    
    public async Task DeleteBrandAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var brand = await context.Brands.FindAsync(id);
        if (brand != null)
        {
            context.Brands.Remove(brand);
            await context.SaveChangesAsync();
        }
    }
    
    public async Task SeedDefaultBrandsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        if (!await context.Brands.AnyAsync())
        {
            var defaultBrands = new[]
            {
                "Bambu Lab", "Prusament", "eSun", "Overture", "Hatchbox",
                "Polymaker", "3D Solutech", "SUNLU", "ERYONE", "Protopasta",
                "ColorFabb", "MatterHackers", "Atomic Filament", "Push Plastic"
            };
            
            foreach (var brandName in defaultBrands)
            {
                context.Brands.Add(new Brand { Name = brandName });
            }
            
            await context.SaveChangesAsync();
        }
    }
    
    // App Settings methods
    public async Task<AppSettings> GetSettingsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var settings = await context.AppSettings.FirstOrDefaultAsync();
        
        if (settings == null)
        {
            settings = new AppSettings
            {
                LowThreshold = 500,
                CriticalThreshold = 250,
                Currency = "DKK"
            };
            context.AppSettings.Add(settings);
            await context.SaveChangesAsync();
        }
        
        return settings;
    }
    
    public async Task UpdateSettingsAsync(AppSettings settings)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.AppSettings.Update(settings);
        await context.SaveChangesAsync();
    }
    
    // Smart usage recording - automatically uses oldest/partially used spools first
    public async Task<UsageResult> RecordFilamentUsageAsync(int filamentId, decimal gramsUsed)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var filament = await context.Filaments
            .Include(f => f.Spools.Where(s => s.WeightRemaining > 0 && !s.DateEmptied.HasValue))
            .FirstOrDefaultAsync(f => f.Id == filamentId);
            
        if (filament == null)
            throw new Exception("Filament not found");
        
        // Get spools ordered by: partially used first, then oldest first
        var availableSpools = filament.Spools
            .Where(s => s.WeightRemaining > 0 && !s.DateEmptied.HasValue)
            .OrderBy(s => s.PercentRemaining == 100 ? 1 : 0) // Partially used first
            .ThenBy(s => s.DateAdded) // Then oldest first
            .ToList();
            
        if (!availableSpools.Any())
            throw new Exception("No spools available with remaining filament");
        
        var result = new UsageResult
        {
            TotalGramsUsed = gramsUsed,
            SpoolsAffected = new List<SpoolUsage>()
        };
        
        decimal remainingToSubtract = gramsUsed;
        
        foreach (var spool in availableSpools)
        {
            if (remainingToSubtract <= 0)
                break;
                
            decimal gramsFromThisSpool = Math.Min(remainingToSubtract, spool.WeightRemaining);
            spool.WeightRemaining -= gramsFromThisSpool;
            
            // Mark as emptied if fully used
            if (spool.WeightRemaining <= 0)
            {
                spool.WeightRemaining = 0;
                spool.DateEmptied = DateTime.Now;
            }
            
            context.Spools.Update(spool);
            
            result.SpoolsAffected.Add(new SpoolUsage
            {
                SpoolId = spool.Id,
                GramsUsed = gramsFromThisSpool,
                WasEmptied = spool.WeightRemaining <= 0,
                RemainingAfter = spool.WeightRemaining
            });
            
            remainingToSubtract -= gramsFromThisSpool;
        }
        
        if (remainingToSubtract > 0)
        {
            result.InsufficientFilament = true;
            result.ShortfallGrams = remainingToSubtract;
        }
        
        await context.SaveChangesAsync();
        return result;
    }
}

// Result class for smart usage recording
public class UsageResult
{
    public decimal TotalGramsUsed { get; set; }
    public List<SpoolUsage> SpoolsAffected { get; set; } = new();
    public bool InsufficientFilament { get; set; }
    public decimal ShortfallGrams { get; set; }
    
    public string GetSummaryMessage()
    {
        if (InsufficientFilament)
        {
            return $"⚠️ Only {TotalGramsUsed - ShortfallGrams:F0}g available. Short by {ShortfallGrams:F0}g!";
        }
        
        if (SpoolsAffected.Count == 1)
        {
            var spool = SpoolsAffected[0];
            if (spool.WasEmptied)
            {
                return $"✅ Used {spool.GramsUsed:F0}g from spool. Spool is now empty!";
            }
            return $"✅ Used {spool.GramsUsed:F0}g from spool. {spool.RemainingAfter:F0}g remaining.";
        }
        
        var emptiedCount = SpoolsAffected.Count(s => s.WasEmptied);
        if (emptiedCount > 0)
        {
            return $"✅ Used {TotalGramsUsed:F0}g across {SpoolsAffected.Count} spool(s). {emptiedCount} spool(s) emptied.";
        }
        
        return $"✅ Used {TotalGramsUsed:F0}g across {SpoolsAffected.Count} spool(s).";
    }
}

public class SpoolUsage
{
    public int SpoolId { get; set; }
    public decimal GramsUsed { get; set; }
    public bool WasEmptied { get; set; }
    public decimal RemainingAfter { get; set; }
}
