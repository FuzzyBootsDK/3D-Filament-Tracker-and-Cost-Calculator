using FilamentTracker.Data;
using FilamentTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace FilamentTracker.Services;

public sealed class DatabaseBootstrapService(IServiceProvider serviceProvider, ILogger<DatabaseBootstrapService> logger)
{
    public async Task InitializeAsync()
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FilamentContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();

        // Add missing tables/columns for existing DB files.
        var dbConnStr = context.Database.GetDbConnection().ConnectionString;

        async Task<bool> ColumnExistsAsync(string tableName, string columnName)
        {
            await using var conn = new Microsoft.Data.Sqlite.SqliteConnection(dbConnStr);
            try
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"PRAGMA table_info('{tableName}');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    if (reader["name"].ToString() == columnName)
                        return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Column existence check failed for {Table}.{Column}", tableName, columnName);
                return true;
            }

            return false;
        }

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS Brands (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                DateAdded TEXT NOT NULL
            )");

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS AppSettings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LowThreshold REAL NOT NULL,
                CriticalThreshold REAL NOT NULL,
                Currency TEXT NOT NULL DEFAULT 'DKK'
            )");

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS Printers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                PrinterType TEXT NOT NULL DEFAULT 'BambuLab',
                IpAddress TEXT NOT NULL,
                AccessCode TEXT NOT NULL,
                SerialNumber TEXT NOT NULL,
                Enabled INTEGER NOT NULL DEFAULT 1,
                IsDefault INTEGER NOT NULL DEFAULT 0,
                DateAdded TEXT NOT NULL,
                Location TEXT,
                ColorHex TEXT DEFAULT '#3b82f6'
            )");

        if (!await ColumnExistsAsync("AppSettings", "Currency"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE AppSettings ADD COLUMN Currency TEXT DEFAULT 'DKK'");

        if (!await ColumnExistsAsync("Filaments", "PricePerKg"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE Filaments ADD COLUMN PricePerKg REAL");

        if (!await ColumnExistsAsync("Filaments", "ColorHex"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE Filaments ADD COLUMN ColorHex TEXT DEFAULT ''");

        if (!await ColumnExistsAsync("Spools", "PurchasePricePerKg"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE Spools ADD COLUMN PurchasePricePerKg REAL");

        if (!await ColumnExistsAsync("Spools", "AmsTrayUuid"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE Spools ADD COLUMN AmsTrayUuid TEXT");

        if (!await ColumnExistsAsync("Spools", "AmsTagUid"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE Spools ADD COLUMN AmsTagUid TEXT");

        if (!await ColumnExistsAsync("AppSettings", "BambuLabIpAddress"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE AppSettings ADD COLUMN BambuLabIpAddress TEXT");

        if (!await ColumnExistsAsync("AppSettings", "BambuLabAccessCode"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE AppSettings ADD COLUMN BambuLabAccessCode TEXT");

        if (!await ColumnExistsAsync("AppSettings", "BambuLabSerialNumber"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE AppSettings ADD COLUMN BambuLabSerialNumber TEXT");

        if (!await ColumnExistsAsync("AppSettings", "BambuLabEnabled"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE AppSettings ADD COLUMN BambuLabEnabled INTEGER NOT NULL DEFAULT 0");

        if (!await ColumnExistsAsync("AppSettings", "AmsAutoUpdateWeight"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE AppSettings ADD COLUMN AmsAutoUpdateWeight INTEGER NOT NULL DEFAULT 0");

        if (!await ColumnExistsAsync("AppSettings", "AmsAutoUpdateOnlyDecrease"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE AppSettings ADD COLUMN AmsAutoUpdateOnlyDecrease INTEGER NOT NULL DEFAULT 1");

        if (!await ColumnExistsAsync("AppSettings", "TimeZoneId"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE AppSettings ADD COLUMN TimeZoneId TEXT NOT NULL DEFAULT 'Europe/Copenhagen'");

        if (!await ColumnExistsAsync("AppSettings", "MqttRelayEnabled"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE AppSettings ADD COLUMN MqttRelayEnabled INTEGER NOT NULL DEFAULT 0");

        if (!await ColumnExistsAsync("AppSettings", "MqttRelayPort"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE AppSettings ADD COLUMN MqttRelayPort INTEGER NOT NULL DEFAULT 1883");

        if (!await ColumnExistsAsync("AppSettings", "MqttRelayUsername"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE AppSettings ADD COLUMN MqttRelayUsername TEXT");

        if (!await ColumnExistsAsync("AppSettings", "MqttRelayPassword"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE AppSettings ADD COLUMN MqttRelayPassword TEXT");

        await context.Database.ExecuteSqlRawAsync("UPDATE AppSettings SET AmsAutoUpdateOnlyDecrease = 1 WHERE AmsAutoUpdateOnlyDecrease IS NULL");
        await context.Database.ExecuteSqlRawAsync("UPDATE AppSettings SET AmsAutoUpdateWeight = 0 WHERE AmsAutoUpdateWeight IS NULL");
        await context.Database.ExecuteSqlRawAsync("UPDATE AppSettings SET BambuLabEnabled = 0 WHERE BambuLabEnabled IS NULL");
        await context.Database.ExecuteSqlRawAsync("UPDATE AppSettings SET TimeZoneId = 'Europe/Copenhagen' WHERE TimeZoneId IS NULL OR TimeZoneId = ''");
        await context.Database.ExecuteSqlRawAsync("UPDATE AppSettings SET MqttRelayEnabled = 0 WHERE MqttRelayEnabled IS NULL");
        await context.Database.ExecuteSqlRawAsync("UPDATE AppSettings SET MqttRelayPort = 1883 WHERE MqttRelayPort IS NULL OR MqttRelayPort = 0");

        if (!await ColumnExistsAsync("AppSettings", "Theme"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE AppSettings ADD COLUMN Theme TEXT NOT NULL DEFAULT 'dark'");

        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");

        await context.Database.ExecuteSqlRawAsync(@"
            UPDATE ReusableSpools
            SET CurrentSpoolId = NULL, InUse = 0
            WHERE CurrentSpoolId IS NOT NULL
            AND CurrentSpoolId NOT IN (SELECT Id FROM Spools)");

        if (!await context.AppSettings.AnyAsync())
        {
            context.AppSettings.Add(new AppSettings
            {
                LowThreshold = 500,
                CriticalThreshold = 250
            });
            await context.SaveChangesAsync();
        }

        var filamentService = scope.ServiceProvider.GetRequiredService<FilamentService>();
        await filamentService.MigrateLegacyPrinterSettingsAsync();
    }
}

