using FilamentTracker.Data;
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
    options.KeepAliveInterval       = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval   = TimeSpan.FromSeconds(60);
    // Allow larger messages (useful for CSV imports)
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB
});

// Determine database path (Docker-friendly)
var dbPath = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? (Directory.Exists("/app/data") ? "Data Source=/app/data/filaments.db" : "Data Source=filaments.db");

// Add SQLite database
builder.Services.AddDbContextFactory<FilamentContext>(options =>
    options.UseSqlite(dbPath));

// Add custom services
builder.Services.AddScoped<FilamentService>();
builder.Services.AddScoped<CsvService>();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<ThresholdService>();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FilamentContext>>();
    using var context = await contextFactory.CreateDbContextAsync();
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
        
        // Check if AppSettings table exists, if not create it
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS AppSettings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LowThreshold REAL NOT NULL,
                CriticalThreshold REAL NOT NULL,
                Currency TEXT NOT NULL DEFAULT 'DKK'
            )");
        
        // Add Currency column if it doesn't exist (for existing databases)
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE AppSettings ADD COLUMN Currency TEXT NOT NULL DEFAULT 'DKK'
            ");
        }
        catch { /* Column already exists */ }
        
        // Add PricePerKg column to Filaments if it doesn't exist (for existing databases)
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE Filaments ADD COLUMN PricePerKg REAL
            ");
        }
        catch { /* Column already exists */ }
        
        // Add PurchasePricePerKg column to Spools if it doesn't exist
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE Spools ADD COLUMN PurchasePricePerKg REAL
            ");
        }
        catch { /* Column already exists */ }
        
        // Ensure default settings exist
        if (!await context.AppSettings.AnyAsync())
        {
            context.AppSettings.Add(new FilamentTracker.Models.AppSettings
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
            var thresholdService = scope.ServiceProvider.GetRequiredService<ThresholdService>();
            thresholdService.SetThresholds(settings.LowThreshold, settings.CriticalThreshold);
        }
    }
    catch { }
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
