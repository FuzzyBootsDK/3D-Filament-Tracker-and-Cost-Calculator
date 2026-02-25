# Filament Tracker v2.0

A comprehensive Blazor web application for tracking 3D printer filament inventory, managing spools, monitoring usage, and live-monitoring your BambuLab printer.

## 🚀 Quick Start (Docker - Recommended)

**One-click deployment!**

### Windows:
Double-click `deploy.bat` **OR** run in PowerShell:
```powershell
.\deploy.ps1
```

### Mac/Linux:
```bash
docker-compose up -d
```

**That's it!** Browser opens to `http://localhost:5000`

> 📚 **Detailed Docker guide:** See [DOCKER-README.md](DOCKER-README.md)

---

## ✨ Features

### Inventory Management:
- 📦 **Multi-Spool Tracking** - Track multiple spools of the same filament color
- ⚠️ **Smart Warnings** - Customizable low/critical stock alerts (default: 500g/250g)
- 🔄 **Reusable Spool Management** - Automatic tracking when spools empty
- 🎨 **Color Brightness Sorting** - Sort filaments from dark to light
- 🏷️ **Brand Management** - Organized dropdown with custom brands
- ⚖️ **Configurable Thresholds** - Set your own warning levels
- 📥 **CSV Import/Export** - Backup and restore your data
- 💱 **Multi-Currency Support** - 24 currencies including DKK, USD, EUR, GBP, SEK
- 🌓 **Dark/Light Themes** - Choose your preferred theme
- 🔍 **Advanced Search** - Find filaments by brand, color, type, finish
- 📝 **Usage Tracking** - Record filament usage per spool

### Print Cost Calculator:
- 🧮 **Professional Cost Estimation** - Calculate accurate 3D print costs
- 🖨️ **Bambu Lab Printer Profiles** - Pre-configured settings for X1C, P1S, A1, H2 series
- 🎨 **Multi-Material Support** - Track costs for multi-color prints with full **Brand · Type · Sub-Brand - Color** dropdown
- 📄 **G-code Import** - Auto-extract print time and filament weight
- 📊 **Batch Optimization** - Calculate costs for different quantities
- 💰 **Pricing Presets** - Competitive, Standard, Premium, Luxury, or Custom margins
- 📑 **PDF Export** - Generate professional quotes for clients

### BambuLab Live Integration:
- 🖨️ **Real-Time Print Monitoring** - Live progress, layer count, time remaining via direct MQTT
- 🎞️ **AMS Slot Tracking** - All AMS slots shown with color swatch, filament **type**, and **sub-brand** (e.g. "PLA · Matte") — no more duplicate "PLA PLA" display
- 📡 **MQTT Message Log** - Built-in terminal in Settings showing the last 50 raw incoming MQTT messages for diagnostics, with auto-scroll and Refresh / Clear controls
- 🔒 **Local & Private** - Direct LAN connection, no cloud required

---

## 🛠️ Technology Stack

- **Framework**: Blazor Server (.NET 10)
- **Database**: SQLite with Entity Framework Core
- **UI**: Custom CSS with responsive design
- **CSV Processing**: CsvHelper library
- **MQTT**: MQTTnet (direct BambuLab LAN connection)
- **Containerization**: Docker support

---

## 📋 Prerequisites

### Docker Method (Easiest):
- [Docker Desktop](https://www.docker.com/products/docker-desktop) installed and running

### Local .NET Method:
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

---

## 🐳 Docker Deployment (Recommended)

### Quick Deploy:

**Windows (PowerShell):**
```powershell
.\deploy.ps1
```

**Windows (Batch):**
```bash
deploy.bat
```

**Linux/Mac/NAS:**
```bash
docker-compose up -d
```

### What the Script Does:
1. ✅ Checks Docker installation
2. ✅ Builds optimized image (~210MB)
3. ✅ Creates persistent data volume
4. ✅ Starts container
5. ✅ Opens browser automatically

### Access:
- **Local:** `http://localhost:5000`
- **Network:** `http://YOUR-IP:5000`

### Management Commands:
```bash
# View logs
docker logs filament-tracker
docker logs -f filament-tracker  # Follow in real-time

# Stop/Start/Restart
docker stop filament-tracker
docker start filament-tracker
docker restart filament-tracker

# Update to latest version
docker stop filament-tracker
docker rm filament-tracker
.\deploy.ps1  # Or docker-compose up -d --build
```

### Deploy to NAS:
**Synology:**
```bash
cd /volume1/docker/FilamentTracker
docker-compose up -d
```

**QNAP:** Use Container Station UI or CLI

📚 **Full Docker guide:** [DOCKER-README.md](DOCKER-README.md)

---

## 💻 Local .NET Deployment

### Installation

1. Navigate to the FilamentTracker directory:
   ```bash
   cd "FilamentTracker"
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Run the application:
   ```bash
   dotnet run
   ```

4. Open your browser to `https://localhost:5001` (or the URL shown in the console)

### First Run

The database will be automatically created on first run. You can:
- Start adding filaments manually via the "Add Filament" page
- Import existing data from the provided CSV file via Settings > Import Data

## Usage Guide

### Adding Filaments

1. Navigate to **Add Filament** page
2. Fill in required fields:
   - Brand (select from dropdown or add custom)
   - Type (PLA, PETG, ASA, ABS, TPU, NYLON)
   - Color Name
   - Color Code (hex value like #FF0000)
   - Total Weight and Remaining Weight
3. Optional fields:
   - Finish (Matte, Silk, CF, etc.)
   - Spool Type (On Spool or Refill)
   - Quantity (add multiple identical spools at once)
   - Storage Location
   - Notes
   - Purchase Price Per Kg (used for weighted average cost in calculator)

### Managing Inventory

1. Click any filament card on the **Inventory** page
2. In the detail modal you can:
   - View all spools for that filament
   - Record usage (subtract grams)
   - Edit spool details
   - Update storage location and notes
   - Delete individual spools or entire filament

### Tracking Reusable Spools

1. Go to **Spools** page
2. Add reusable spools you own
3. Track which are available vs in use
4. Helps determine how many refills you can accommodate

### Print Cost Calculator

1. Navigate to **Calculator** page
2. Fill in project details:
   - Part name and printer profile
   - Print time and handling time
   - Hardware and packaging costs
   - Batch size and VAT rate
3. Add materials — the dropdown shows **Brand · Type · Sub-Brand - Color** for each inventory filament
4. Optional: Import G-code file for automatic data extraction
5. Configure advanced settings (hourly rate, depreciation, electricity, etc.)
6. Select pricing option (Competitive, Standard, Premium, Luxury, or Custom)
7. Review cost breakdown and batch optimization table
8. Export professional quote as PDF

**Key Benefits:**
- Accurate machine depreciation calculations
- Electricity cost tracking
- Labor and handling time costs
- Hardware and packaging expenses
- Configurable profit margins
- Batch pricing optimization
- Professional PDF quotes

### BambuLab Live Tracking

1. Go to **Settings → BambuLab MQTT Live Tracking**
2. Enable tracking and enter your printer's IP, Access Code, and Serial Number
3. Click **🧪 Test Connection** then **💾 Save & Connect**
4. The live widget in the navigation bar shows print progress, layers, time, and AMS slots in real time

#### AMS Slot Display
Each AMS slot now shows:
- **Color swatch** (live from printer)
- **Type** (e.g., PLA) on the first line
- **Sub-brand / Variant** (e.g., Matte) on the second line

This replaces the old "PLA PLA" display where type and sub-brand were incorrectly merged.

### MQTT Message Log

Found in **Settings → 📡 MQTT Message Log**, this built-in terminal shows:
- The last **50 raw JSON messages** from your printer
- Timestamp, topic, and full payload per message
- Auto-scrolls to newest message
- **🔄 Refresh** and **🗑️ Clear Display** controls

Use it to verify AMS data, confirm connectivity, or debug unexpected behavior.

### Settings

- **Theme Toggle**: Switch between dark and light modes
- **Currency**: Choose from 24 currencies (DKK, USD, EUR, GBP, SEK, NOK, and more)
- **Stock Thresholds**: Configure Low and Critical warning levels
- **Brand Management**: Add, view, and delete filament brands
- **BambuLab MQTT**: Configure live printer tracking
- **MQTT Message Log**: Diagnostic terminal for raw printer messages
- **Export to CSV**: Download all your filament data
- **Import from CSV**: Bulk import from a CSV file
- **Purge Database**: Delete all data (use with caution!)

## CSV Format

For importing data, use this format:

```csv
Brand,Type,Finish,Color Name,Color Code,Total Weight (g),Weight Remaining (g),Quantity,Spool Type,Spool Material,Reusable Spool,Diameter (mm),Location,Notes,Date Added,Purchase Price Per Kg
Bambu Lab,PLA,Matte,Charcoal,#000000,1000,1000,1,spool,plastic,Yes,1.75,Shelf A,Color : Matte Charcoal,02/16/2026,149
```

## Project Structure

```
FilamentTracker/
├── Components/          # Razor components for each page
│   ├── InventoryPage.razor
│   ├── AddFilamentPage.razor
│   ├── SpoolsPage.razor
│   ├── HelpPage.razor
│   ├── SettingsPage.razor
│   └── FilamentDetailModal.razor
├── Data/               # Database context
│   └── FilamentContext.cs
├── Models/             # Data models
│   ├── Filament.cs
│   ├── Spool.cs
│   ├── ReusableSpool.cs
│   ├── Brand.cs
│   ├── PrintStatus.cs   # BambuLab live status model
│   └── AppSettings.cs
├── Services/           # Business logic
│   ├── FilamentService.cs
│   ├── CsvService.cs
│   ├── ThemeService.cs
│   ├── ThresholdService.cs
│   └── BambuLabService.cs  # MQTT connection & AMS parsing
├── Pages/              # Main pages
│   ├── Index.razor
│   └── _Host.cshtml
├── Shared/             # Shared components
│   └── MainLayout.razor
├── wwwroot/            # Static files
│   ├── css/
│   │   ├── site.css
│   │   └── calculator.css
│   └── js/
│       ├── site.js              # Shared helpers (scrollMqttTerminalToBottom)
│       ├── calculator-app.js    # Print calculator logic
│       ├── calculator-gcode.js  # G-code parser
│       └── calculator-pdf.js    # PDF export
├── Docker Files        # Deployment files
│   ├── Dockerfile
│   ├── .dockerignore
│   ├── docker-compose.yml
│   ├── deploy.ps1      # PowerShell deployment
│   ├── deploy.bat      # Batch deployment
│   └── DOCKER-README.md
├── App.razor
├── Program.cs
├── _Imports.razor
└── README.md           # This file
```

## Database

The app uses SQLite with a file-based database:
- **Local:** `filaments.db` in application directory
- **Docker:** `/app/data/filaments.db` (persisted in volume)

### Schema

**Filaments Table**
- Id, Brand, Type, Finish, ColorName, ColorCode
- Diameter, Location, Notes, DateAdded

**Spools Table**
- Id, FilamentId (FK), TotalWeight, WeightRemaining
- IsRefill, SpoolMaterial, IsReusable, DateAdded, DateEmptied
- PurchasePricePerKg *(for weighted average costing)*

**ReusableSpools Table**
- Id, Material, InUse, CurrentSpoolId, DateAdded

**Brands Table**
- Id, Name, DateAdded

**AppSettings Table**
- Id, LowThreshold, CriticalThreshold, Currency
- BambuLabEnabled, BambuLabIpAddress, BambuLabAccessCode, BambuLabSerialNumber

## Features in Detail

### Status Indicators (Configurable!)

- **Green (OK)**: Above your Low threshold (default: 500g) - Stock is healthy
- **Yellow (Low)**: Below Low threshold but above Critical (default: 250g-500g) - Consider ordering more
- **Red (Critical)**: Below Critical threshold (default: 250g) - Order soon!

**Configure your own thresholds in Settings → Stock Thresholds!**

### AMS Slot Display

Each AMS slot shows **type** and **sub-brand** on separate lines (e.g., "PLA" / "Matte") rather than the old merged "PLA PLA" format. Empty or untagged slots are shown as "Empty".

The filament selection dropdown throughout the app (Calculator, AMS views) uses the format:
```
Brand · Type · Sub-Brand - Color Name
```
e.g., `Bambu Lab · PLA · Matte - Charcoal`

### MQTT Message Log (Diagnostic Terminal)

Found in **Settings**, this real-time terminal:
- Displays the last **50 raw JSON messages** from your BambuLab printer
- Auto-scrolls to the newest message
- Color-coded: timestamp (blue), topic (green), payload (white)
- Controls: 🔄 Refresh | 🗑️ Clear Display | connection status badge

### Weighted Average Pricing

Each spool stores an optional purchase price per kg. The calculator uses the weighted average across all spools of the same filament — no manual calculation needed.

### Grouping Logic

Filaments are grouped when they have identical:
- Brand
- Type
- Finish
- Color Name
- Color Code

This allows multiple spools (refills or on spools) to be tracked under one filament.

### Color Brightness Sorting

Sort your filaments by perceived brightness:
- **Dark → Light**: Find black and dark colors first
- **Light → Dark**: Find white and light colors first
- Uses standard RGB brightness formula for accurate sorting

### Usage Recording

Track actual filament usage:
- Manual entry of grams used
- Quick buttons for common amounts (-25g, -50g, -100g)
- Automatically updates remaining weight and status
- Reusable spools automatically marked as "Available" when empty

## Tips

1. **Regular Updates**: Update filament usage after each print for accuracy
2. **Color Codes**: Use manufacturer's hex codes for accurate visual representation
3. **Backup Often**: Export to CSV regularly as a backup (Settings → Export to CSV)
4. **Storage Labels**: Match physical storage labels with the app's location field
5. **Notes Field**: Record optimal print settings, temperature, or special characteristics
6. **Customize Thresholds**: Adjust warning levels in Settings to match your printing habits
7. **Use Color Sort**: Find filaments visually by sorting dark to light or light to dark
8. **Brand Management**: Add custom brands on-the-fly using "Other (Add Custom)" option
9. **Purchase Prices**: Enter prices per spool for accurate weighted average costing
10. **MQTT Terminal**: Use the Settings → MQTT Message Log to debug AMS or connection issues

## 📚 Documentation

- **[DOCKER-README.md](DOCKER-README.md)** - Comprehensive Docker deployment guide
- **Help Page** - In-app documentation (click Help in navigation)
- **This README** - Quick reference and overview

## 🔧 Troubleshooting

### Docker Issues

**Docker not running:**
```bash
# Start Docker Desktop and wait for it to fully start
```

**Port 5000 already in use:**
```powershell
.\deploy.ps1 -Port 8080  # Use different port
```

**Can't access from phone/tablet:**
- Check firewall settings
- Use computer's IP address: `http://192.168.x.x:5000`
- Ensure devices are on same network

**Container won't start:**
```bash
docker logs filament-tracker  # Check logs for errors
docker restart filament-tracker  # Try restarting
```

**Need to reset database:**
```powershell
.\deploy.ps1 -Clean  # Removes container, image, and optionally volume
```

### Local .NET Issues

**Build Issues:**
```bash
dotnet clean
dotnet restore
dotnet build
```

**Database Issues:**
Delete `filaments.db` file to recreate database (export data first!)

**Port Already in Use:**
Edit `Properties/launchSettings.json` to change ports

### BambuLab MQTT Issues

**"BadUserNameOrPassword":** Double-check the 8-character Access Code on the printer.

**Connection timeout:** Verify IP address, same network, port 8883 not blocked.

**AMS showing wrong data:** Check the MQTT Message Log in Settings to see what the printer is actually reporting.

**Connection drops:** Set a static IP on the printer via your router. The app auto-reconnects every 5 seconds.

## 💾 Backup & Restore

### CSV Method (Recommended):
1. Open app → Settings
2. Click "Export to CSV"
3. Save file to backup location

To restore: Settings → Import CSV

### Docker Volume Backup:
```bash
# Backup
docker run --rm -v filament-tracker-data:/data -v ${PWD}:/backup alpine tar czf /backup/filament-backup.tar.gz -C /data .

# Restore
docker run --rm -v filament-tracker-data:/data -v ${PWD}:/backup alpine sh -c "cd /data && tar xzf /backup/filament-backup.tar.gz"
```

## Contributing

This is a personal project, but feel free to fork and customize for your needs!

## License

MIT License - Feel free to use and modify as needed.

## Support

For issues or questions, check the Help page within the app for detailed usage instructions.

---

**Made with ❤️ for the 3D printing community**