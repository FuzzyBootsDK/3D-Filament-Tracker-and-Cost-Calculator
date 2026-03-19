using FilamentTracker.Data;
using FilamentTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace FilamentTracker.Services;

public class FilamentService(IDbContextFactory<FilamentContext> contextFactory, ThresholdService thresholdService)
{
    public async Task<List<Filament>> GetAllFilamentsAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Filaments
            .Include(f => f.Spools.Where(s => s.WeightRemaining > 0 && s.DateEmptied == null))
            .OrderByDescending(f => f.DateAdded)
            .ToListAsync();
    }

    public List<Filament> GetAllFilaments()
    {
        using var context = contextFactory.CreateDbContext();
        // Return all filaments from the database or in-memory list
        return context.Filaments.ToList();
    }

    public async Task<Filament?> GetFilamentByIdAsync(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Filaments
            .Include(f => f.Spools)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<Filament> AddFilamentAsync(Filament filament)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Filaments.Add(filament);
        await context.SaveChangesAsync();

        // Track reusable spools
        foreach (var spool in filament.Spools.Where(s => s is { IsReusable: true, WeightRemaining: > 0 }))
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
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Filaments.Update(filament);
        await context.SaveChangesAsync();
    }

    public async Task DeleteFilamentAsync(int id)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var filament = await context.Filaments.FindAsync(id);
            if (filament != null)
            {
                context.Filaments.Remove(filament);
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Log the error - in production, use proper logging
            Console.WriteLine($"Error deleting filament {id}: {ex.Message}");
            throw; // Re-throw so the UI can handle it
        }
    }

    public async Task<Spool> AddSpoolAsync(Spool spool)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Spools.Add(spool);
        await context.SaveChangesAsync();
        return spool;
    }

    public async Task<Spool> AddSpoolToFilamentAsync(int filamentId, Spool spool)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        spool.FilamentId = filamentId;
        context.Spools.Add(spool);
        await context.SaveChangesAsync();

        // Track reusable spool if applicable
        if (spool is { IsReusable: true, WeightRemaining: > 0 })
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
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Spools.Update(spool);
        await context.SaveChangesAsync();

        // Update reusable spool tracking if this is a reusable spool
        if (spool.IsReusable)
        {
            var reusableSpool = await context.ReusableSpools
                .FirstOrDefaultAsync(rs => rs.CurrentSpoolId == spool.Id);

            if (reusableSpool != null)
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

    public async Task DeleteSpoolAsync(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var spool = await context.Spools.FindAsync(id);
        if (spool != null)
        {
            context.Spools.Remove(spool);
            await context.SaveChangesAsync();
        }
    }

    public async Task<Dictionary<string, int>> GetStatisticsAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
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
            var status = thresholdService.GetStatus(filament.WeightRemaining);
            stats[status == "critical" ? "Critical" : status == "low" ? "Low" : "Ok"]++;
        }

        return stats;
    }

    public async Task<List<ReusableSpool>> GetReusableSpoolsAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.ReusableSpools.ToListAsync();
    }

    public async Task<ReusableSpool> AddReusableSpoolAsync(ReusableSpool spool)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        context.ReusableSpools.Add(spool);
        await context.SaveChangesAsync();
        return spool;
    }

    public async Task UpdateReusableSpoolAsync(ReusableSpool spool)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        context.ReusableSpools.Update(spool);
        await context.SaveChangesAsync();
    }

    public async Task DeleteReusableSpoolAsync(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var spool = await context.ReusableSpools.FindAsync(id);
        if (spool != null)
        {
            context.ReusableSpools.Remove(spool);
            await context.SaveChangesAsync();
        }
    }

    public async Task PurgeDatabaseAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Filaments.RemoveRange(context.Filaments);
        context.Spools.RemoveRange(context.Spools);
        await context.SaveChangesAsync();
    }

    // Brand management methods
    public async Task<List<Brand>> GetBrandsAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Brands.OrderBy(b => b.Name).ToListAsync();
    }

    public async Task<Brand> AddBrandAsync(string brandName)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

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
        await using var context = await contextFactory.CreateDbContextAsync();
        var brand = await context.Brands.FindAsync(id);
        if (brand != null)
        {
            context.Brands.Remove(brand);
            await context.SaveChangesAsync();
        }
    }

    // ── AMS RFID linking ──────────────────────────────────────────────────────

    /// Find a spool that has been previously linked to an AMS tray_uuid or tag_uid.
    /// Returns null when no match (i.e. first time this spool is seen).
    public async Task<Spool?> FindSpoolByAmsIdAsync(string? trayUuid, string? tagUid)
    {
        // Auto-matching should only occur on the persistent RFID tag (tagUid).
        // Treat trayUuid as a weak/location identifier and do not auto-match on it
        // because it can cause false positives when an untagged spool is placed
        // into a slot that previously held a different spool.
        if (string.IsNullOrEmpty(tagUid))
            return null;

        var isPlaceholderTag = tagUid.Replace("0", "").Length == 0;
        if (isPlaceholderTag) return null;

        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Spools
            .Include(s => s.Filament)
            .Where(s => s.DateEmptied == null && s.WeightRemaining > 0)
            .Where(s => s.AmsTagUid == tagUid)
            .OrderByDescending(s => s.DateAdded)
            .FirstOrDefaultAsync();
    }

    /// Persist the AMS RFID ids onto a spool so it auto-matches next time.
    public async Task LinkSpoolToAmsSlotAsync(int spoolId, string? trayUuid, string? tagUid)
    {
        // Normalize placeholders: treat all-zero UUIDs as null so we don't persist meaningless IDs
        static string? Normalize(string? s) => string.IsNullOrEmpty(s) || s.Replace("0", "").Length == 0 ? null : s;
        var nTray = Normalize(trayUuid);
        var nTag = Normalize(tagUid);

        await using var context = await contextFactory.CreateDbContextAsync();

        // Clear same identifiers from any other spool to keep mapping unique.
        if (!string.IsNullOrEmpty(trayUuid) || !string.IsNullOrEmpty(tagUid))
        {
            var conflicts = await context.Spools
                .Where(s => s.Id != spoolId &&
                            ((trayUuid != null && s.AmsTrayUuid == trayUuid) ||
                             (tagUid  != null && s.AmsTagUid   == tagUid)))
                .ToListAsync();
            foreach (var other in conflicts)
            {
                other.AmsTrayUuid = null;
                other.AmsTagUid = null;
                context.Spools.Update(other);
            }
        }

        var spool = await context.Spools.FindAsync(spoolId);
        if (spool == null) return;
        spool.AmsTrayUuid = nTray;
        spool.AmsTagUid = nTag;
        context.Spools.Update(spool);
        await context.SaveChangesAsync();
    }

    /// Find spool by exact AMS identifiers (does not treat all-zero UUID as special).
    /// Used for explicit unlink operations where the slot may contain a placeholder UUID.
    public async Task<Spool?> FindSpoolByExactAmsIdAsync(string? trayUuid, string? tagUid)
    {
        if (string.IsNullOrEmpty(trayUuid) && string.IsNullOrEmpty(tagUid))
            return null;

        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Spools
            .Include(s => s.Filament)
            .Where(s => s.DateEmptied == null && s.WeightRemaining > 0)
            .Where(s => (trayUuid != null && s.AmsTrayUuid == trayUuid) || (tagUid != null && s.AmsTagUid == tagUid))
            .OrderByDescending(s => s.DateAdded)
            .FirstOrDefaultAsync();
    }

    public async Task SeedDefaultBrandsAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        if (!await context.Brands.AnyAsync())
        {
            var defaultBrands = new[]
            {
                "Bambu Lab", "Prusament", "eSun", "Overture", "Hatchbox",
                "Polymaker", "3D Solutech", "SUNLU", "ERYONE", "Protopasta",
                "ColorFabb", "MatterHackers", "Atomic Filament", "Push Plastic"
            };

            foreach (var brandName in defaultBrands) context.Brands.Add(new Brand { Name = brandName });

            await context.SaveChangesAsync();
        }
    }

    // App Settings methods
    public async Task<AppSettings> GetSettingsAsync()
    {
        // Use raw ADO.NET for the entire read so EF change tracking and connection
        // state never interfere with reading bool columns (NULL / 0 / 1).
        await using var context = await contextFactory.CreateDbContextAsync();
        var connStr = context.Database.GetDbConnection().ConnectionString;
        await using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connStr);
        await conn.OpenAsync();

        // Check if a row exists
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM AppSettings";
        var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        if (count == 0)
        {
            // Insert defaults
            await using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = """

                                                    INSERT INTO AppSettings
                                                        (LowThreshold, CriticalThreshold, Currency, BambuLabEnabled,
                                                         AmsAutoUpdateWeight, AmsAutoUpdateOnlyDecrease)
                                                    VALUES (500, 250, 'DKK', 0, 0, 1)
                                    """;
            await insertCmd.ExecuteNonQueryAsync();
        }

        // Read all columns we care about
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, LowThreshold, CriticalThreshold, Currency,
                   BambuLabIpAddress, BambuLabAccessCode, BambuLabSerialNumber,
                   BambuLabEnabled, AmsAutoUpdateWeight, AmsAutoUpdateOnlyDecrease
            FROM AppSettings
            LIMIT 1";
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return new AppSettings { AmsAutoUpdateOnlyDecrease = true };

        return new AppSettings
        {
            Id                        = reader.GetInt32(0),
            LowThreshold              = reader.GetDecimal(1),
            CriticalThreshold         = reader.GetDecimal(2),
            Currency                  = reader.GetString(3),
            BambuLabIpAddress         = reader.IsDBNull(4)  ? null : reader.GetString(4),
            BambuLabAccessCode        = reader.IsDBNull(5)  ? null : reader.GetString(5),
            BambuLabSerialNumber      = reader.IsDBNull(6)  ? null : reader.GetString(6),
            BambuLabEnabled           = !reader.IsDBNull(7) && reader.GetInt32(7) == 1,
            AmsAutoUpdateWeight       = !reader.IsDBNull(8) && reader.GetInt32(8) == 1,
            AmsAutoUpdateOnlyDecrease = reader.IsDBNull(9)  || reader.GetInt32(9) == 1,
        };
    }

    public async Task UpdateSettingsAsync(AppSettings settings)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var connStr = context.Database.GetDbConnection().ConnectionString;
            await using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE AppSettings SET
                    LowThreshold               = $low,
                    CriticalThreshold          = $crit,
                    Currency                   = $curr,
                    BambuLabIpAddress          = $ip,
                    BambuLabAccessCode         = $code,
                    BambuLabSerialNumber       = $serial,
                    BambuLabEnabled            = $enabled,
                    AmsAutoUpdateWeight        = $autoWeight,
                    AmsAutoUpdateOnlyDecrease  = $onlyDecrease
                WHERE Id = $id";
            cmd.Parameters.AddWithValue("$low",          (double)settings.LowThreshold);
            cmd.Parameters.AddWithValue("$crit",         (double)settings.CriticalThreshold);
            cmd.Parameters.AddWithValue("$curr",         settings.Currency);
            cmd.Parameters.AddWithValue("$ip",           (object?)settings.BambuLabIpAddress    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$code",         (object?)settings.BambuLabAccessCode   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$serial",       (object?)settings.BambuLabSerialNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$enabled",      settings.BambuLabEnabled          ? 1 : 0);
            cmd.Parameters.AddWithValue("$autoWeight",   settings.AmsAutoUpdateWeight       ? 1 : 0);
            cmd.Parameters.AddWithValue("$onlyDecrease", settings.AmsAutoUpdateOnlyDecrease ? 1 : 0);
            cmd.Parameters.AddWithValue("$id",           settings.Id);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[UpdateSettingsAsync] SQL update failed: {ex.Message}");
            // Fallback to EF
            await using var ctx = await contextFactory.CreateDbContextAsync();
            ctx.AppSettings.Update(settings);
            await ctx.SaveChangesAsync();
        }
    }

    // Smart usage recording - automatically uses oldest/partially used spools first
    public async Task<UsageResult> RecordFilamentUsageAsync(int filamentId, decimal gramsUsed)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var filament = await context.Filaments
            .Include(f => f.Spools.Where(s => s.WeightRemaining > 0 && !s.DateEmptied.HasValue))
            .FirstOrDefaultAsync(f => f.Id == filamentId);

        if (filament == null)
            throw new Exception("Filament not found");

        // Get spools ordered by: partially used first, then oldest first
        var availableSpools = filament.Spools
            .Where(s => s is { WeightRemaining: > 0, DateEmptied: null })
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

        var remainingToSubtract = gramsUsed;

        foreach (var spool in availableSpools)
        {
            if (remainingToSubtract <= 0)
                break;

            var gramsFromThisSpool = Math.Min(remainingToSubtract, spool.WeightRemaining);
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
    public decimal TotalGramsUsed { get; init; }
    public List<SpoolUsage> SpoolsAffected { get; init; } = new();
    public bool InsufficientFilament { get; set; }
    public decimal ShortfallGrams { get; set; }

    public string GetSummaryMessage()
    {
        if (InsufficientFilament)
            return $"⚠️ Only {TotalGramsUsed - ShortfallGrams:F0}g available. Short by {ShortfallGrams:F0}g!";

        if (SpoolsAffected.Count == 1)
        {
            var spool = SpoolsAffected[0];
            if (spool.WasEmptied) return $"✅ Used {spool.GramsUsed:F0}g from spool. Spool is now empty!";
            return $"✅ Used {spool.GramsUsed:F0}g from spool. {spool.RemainingAfter:F0}g remaining.";
        }

        var emptiedCount = SpoolsAffected.Count(s => s.WasEmptied);
        if (emptiedCount > 0)
            return
                $"✅ Used {TotalGramsUsed:F0}g across {SpoolsAffected.Count} spool(s). {emptiedCount} spool(s) emptied.";

        return $"✅ Used {TotalGramsUsed:F0}g across {SpoolsAffected.Count} spool(s).";
    }
}

public class SpoolUsage
{
    public decimal GramsUsed { get; init; }
    public bool WasEmptied { get; init; }
    public decimal RemainingAfter { get; init; }
}