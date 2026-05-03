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
builder.Services.AddScoped<InventoryPageState>();
builder.Services.AddScoped<AmsPageState>();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<ThresholdService>();
builder.Services.AddSingleton<BambuLabService>();
builder.Services.AddSingleton<PrinterStatusStore>();
builder.Services.AddSingleton<DatabaseBootstrapService>();
builder.Services.AddSingleton<AppSettingsBootstrapService>();
builder.Services.AddSingleton<MqttRelayService>();
builder.Services.AddSingleton<EditStateService>();

var app = builder.Build();

try
{
    var dbBootstrap = app.Services.GetRequiredService<DatabaseBootstrapService>();
    await dbBootstrap.InitializeAsync();

    var appSettingsBootstrap = app.Services.GetRequiredService<AppSettingsBootstrapService>();
    await appSettingsBootstrap.InitializeAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[Startup] Bootstrap error: {ex.Message}");
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