using FilamentTracker.Data;
using FilamentTracker.Models;
using FilamentTracker.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
    {
        // How long a disconnected circuit is kept alive on the server (default: 3 min)
        // Set to 60 min so the page recovers after network blips on the NAS
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(60);
        // Give the client longer to reconnect before the circuit is torn down
        options.DisconnectedCircuitMaxRetained = 100;
        // Detailed errors in production — turn off if you prefer (false hides stack traces)
        options.DetailedErrors = false;
    })
    .AddHubOptions(options =>
    {
        // Keep-alive: ping every 15 s, wait 60 s before declaring the connection dead
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        // Allow larger messages (useful for CSV imports)
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB
    });

// Determine database path (Docker-friendly)
var dbPath = builder.Configuration.GetConnectionString("DefaultConnection")
             ?? (Directory.Exists("/app/data") ? "Data Source=/app/data/filaments.db" : "Data Source=filaments.db");

// Add SQLite database
builder.Services.AddDbContextFactory<FilamentContext>(options =>
    options.UseSqlite(dbPath, sqliteOptions =>
    {
        // Enable foreign key enforcement (required for cascade delete)
        sqliteOptions.CommandTimeout(30);
    }));

// Add custom services
builder.Services.AddScoped<FilamentService>();
builder.Services.AddScoped<CsvService>();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<ThresholdService>();
builder.Services.AddSingleton<BambuLabService>();
builder.Services.AddSingleton<EditStateService>();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FilamentContext>>();
    await using var context = await contextFactory.CreateDbContextAsync();
    await context.Database.EnsureCreatedAsync();

    // Manually create missing tables if needed (for existing databases)
    try
    {
        // Check if Brands table exists, if not create it
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS Brands (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                DateAdded TEXT NOT NULL
            )");

        // Check if the AppSettings table exists, if not create it
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS AppSettings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LowThreshold REAL NOT NULL,
                CriticalThreshold REAL NOT NULL,
                Currency TEXT NOT NULL DEFAULT 'DKK'
            )");

        // Add missing columns only when they do not already exist to avoid noisy errors
        // Capture the connection string up-front so the local function doesn't close over `context`
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
                Console.Error.WriteLine($"[ColumnExistsAsync] {tableName}.{columnName}: {ex.Message}");
                return true;
            }

            return false;
        }

        if (!await ColumnExistsAsync("AppSettings", "Currency"))
            // Adding without NOT NULL constraint here keeps the operation simple across SQLite versions
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE AppSettings ADD COLUMN Currency TEXT DEFAULT 'DKK'
            ");

        if (!await ColumnExistsAsync("Filaments", "PricePerKg"))
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE Filaments ADD COLUMN PricePerKg REAL
            ");

        // Ensure Filaments.ColorHex exists (added in recent migration); add defensively for older DBs
        if (!await ColumnExistsAsync("Filaments", "ColorHex"))
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE Filaments ADD COLUMN ColorHex TEXT DEFAULT ''
            ");

        if (!await ColumnExistsAsync("Spools", "PurchasePricePerKg"))
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE Spools ADD COLUMN PurchasePricePerKg REAL
            ");

        if (!await ColumnExistsAsync("Spools", "AmsTrayUuid"))
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE Spools ADD COLUMN AmsTrayUuid TEXT
            ");

        if (!await ColumnExistsAsync("Spools", "AmsTagUid"))
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE Spools ADD COLUMN AmsTagUid TEXT
            ");

        // Add BambuLab MQTT columns
        if (!await ColumnExistsAsync("AppSettings", "BambuLabIpAddress"))
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE AppSettings ADD COLUMN BambuLabIpAddress TEXT
            ");

        if (!await ColumnExistsAsync("AppSettings", "BambuLabAccessCode"))
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE AppSettings ADD COLUMN BambuLabAccessCode TEXT
            ");

        if (!await ColumnExistsAsync("AppSettings", "BambuLabSerialNumber"))
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE AppSettings ADD COLUMN BambuLabSerialNumber TEXT
            ");

        if (!await ColumnExistsAsync("AppSettings", "BambuLabEnabled"))
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE AppSettings ADD COLUMN BambuLabEnabled INTEGER NOT NULL DEFAULT 0
            ");

        if (!await ColumnExistsAsync("AppSettings", "AmsAutoUpdateWeight"))
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE AppSettings ADD COLUMN AmsAutoUpdateWeight INTEGER NOT NULL DEFAULT 0
            ");

        if (!await ColumnExistsAsync("AppSettings", "AmsAutoUpdateOnlyDecrease"))
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE AppSettings ADD COLUMN AmsAutoUpdateOnlyDecrease INTEGER NOT NULL DEFAULT 1
            ");

        if (!await ColumnExistsAsync("AppSettings", "TimeZoneId"))
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE AppSettings ADD COLUMN TimeZoneId TEXT NOT NULL DEFAULT 'Europe/Copenhagen'
            ");

        // Backfill: existing rows that had the column added will have NULL — set them to sensible defaults
        await context.Database.ExecuteSqlRawAsync(@"
            UPDATE AppSettings SET AmsAutoUpdateOnlyDecrease = 1 WHERE AmsAutoUpdateOnlyDecrease IS NULL
        ");
        await context.Database.ExecuteSqlRawAsync(@"
            UPDATE AppSettings SET AmsAutoUpdateWeight = 0 WHERE AmsAutoUpdateWeight IS NULL
        ");
        await context.Database.ExecuteSqlRawAsync(@"
            UPDATE AppSettings SET BambuLabEnabled = 0 WHERE BambuLabEnabled IS NULL
        ");
        await context.Database.ExecuteSqlRawAsync(@"
            UPDATE AppSettings SET TimeZoneId = 'Europe/Copenhagen' WHERE TimeZoneId IS NULL OR TimeZoneId = ''
        ");

        // Enable foreign key enforcement (critical for cascade delete)
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");

        // Clean up orphaned ReusableSpool records (where CurrentSpoolId references a deleted spool)
        await context.Database.ExecuteSqlRawAsync(@"
            UPDATE ReusableSpools 
            SET CurrentSpoolId = NULL, InUse = 0 
            WHERE CurrentSpoolId IS NOT NULL 
            AND CurrentSpoolId NOT IN (SELECT Id FROM Spools)
        ");

        // Ensure default settings exist
        if (!await context.AppSettings.AnyAsync())
        {
            context.AppSettings.Add(new AppSettings
            {
                LowThreshold = 500,
                CriticalThreshold = 250
            });
            await context.SaveChangesAsync();
        }

        // Load settings into ThresholdService
        var settings = await context.AppSettings.FirstOrDefaultAsync();
        if (settings != null)
        {
            // Environment-variable overrides (container-friendly)
            // These override database values and persist them back to the database
            try
            {
                bool settingsChanged = false;

                var envBambuEnabled = Environment.GetEnvironmentVariable("BAMBULAB_ENABLED");
                var envBambuIp = Environment.GetEnvironmentVariable("BAMBULAB_IP");
                var envBambuCode = Environment.GetEnvironmentVariable("BAMBULAB_CODE");
                var envBambuSerial = Environment.GetEnvironmentVariable("BAMBULAB_SERIAL");

                if (!string.IsNullOrEmpty(envBambuEnabled) &&
                    (envBambuEnabled == "1" || envBambuEnabled.Equals("true", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!settings.BambuLabEnabled)
                    {
                        settings.BambuLabEnabled = true;
                        settingsChanged = true;
                    }
                }

                if (!string.IsNullOrEmpty(envBambuIp) && settings.BambuLabIpAddress != envBambuIp)
                {
                    settings.BambuLabIpAddress = envBambuIp;
                    settingsChanged = true;
                }

                if (!string.IsNullOrEmpty(envBambuCode) && settings.BambuLabAccessCode != envBambuCode)
                {
                    settings.BambuLabAccessCode = envBambuCode;
                    settingsChanged = true;
                }

                if (!string.IsNullOrEmpty(envBambuSerial) && settings.BambuLabSerialNumber != envBambuSerial)
                {
                    settings.BambuLabSerialNumber = envBambuSerial;
                    settingsChanged = true;
                }

                // Save changes back to database if any environment overrides were applied
                if (settingsChanged)
                {
                    await context.SaveChangesAsync();
                }
            }
            catch
            {
                // Swallow errors here — environment lookups should not break startup
            }

            var thresholdService = scope.ServiceProvider.GetRequiredService<ThresholdService>();
            thresholdService.SetThresholds(settings.LowThreshold, settings.CriticalThreshold);

            // Initialize BambuLab connection if enabled
            if (settings.BambuLabEnabled &&
                !string.IsNullOrEmpty(settings.BambuLabIpAddress) &&
                !string.IsNullOrEmpty(settings.BambuLabAccessCode) &&
                !string.IsNullOrEmpty(settings.BambuLabSerialNumber))
            {
                var bambuLabService = scope.ServiceProvider.GetRequiredService<BambuLabService>();
                bambuLabService.AmsAutoUpdateWeight = settings.AmsAutoUpdateWeight;
                bambuLabService.AmsAutoUpdateOnlyDecrease = settings.AmsAutoUpdateOnlyDecrease;
                // Capture logger before Task.Run so the lambda doesn't close over the disposable scope
                var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("BambuLabInit");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await bambuLabService.ConnectAsync(
                            settings.BambuLabIpAddress,
                            settings.BambuLabAccessCode,
                            settings.BambuLabSerialNumber
                        );
                    }
                    catch (Exception ex)
                    {
                        startupLogger.LogError(ex, "Failed to connect to BambuLab printer on startup");
                    }
                });
            }
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[Startup] Database migration error: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();