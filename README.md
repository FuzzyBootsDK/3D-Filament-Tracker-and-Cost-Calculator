# 🎨 Filament Tracker v2.0

A comprehensive **Blazor Server** web application for tracking 3D printer filament inventory, managing spools, monitoring **BambuLab printers via MQTT**, and calculating print costs with professional accuracy.

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![Blazor Server](https://img.shields.io/badge/Blazor-Server-512BD4)](https://blazor.net/)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED)](https://www.docker.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

---

## ✨ Key Features

### 📦 Inventory Management
- **Multi-spool tracking** — Track multiple spools per filament color/type
- **Smart stock warnings** — Configurable low/critical thresholds with visual indicators
- **Reusable spool system** — Track and reuse cardboard/plastic spools automatically
- **Advanced filtering** — Search by brand, type, finish, color, location
- **Color brightness sorting** — Organize filaments from dark to light
- **Usage tracking** — Record per-spool consumption with date/time stamps
- **Bulk operations** — Manage multiple spools efficiently

### 🖨️ BambuLab Integration (MQTT)
- **Real-time printer monitoring** — Live print status in navigation bar
- **AMS spool linking** — Connect AMS slots to your inventory via RFID/NFC tags
- **Automatic weight updates** — Sync remaining filament weight from AMS reports
- **Print progress tracking** — Monitor completion percentage, time remaining, temperatures
- **WiFi signal monitoring** — Visual signal strength indicator
- **Smart ETA calculation** — Accurate completion times with **timezone support** (automatic DST)
- **Connection health** — Real-time connection status and diagnostic logging

### 🧮 Print Cost Calculator
- **Full cost breakdown** — Material, labor, machine time, electricity, depreciation
- **Printer profiles** — Pre-configured for X1C, P1S, A1, A1 Mini, H2S, H2D, H2C
- **Multi-material support** — Calculate costs for prints using multiple filaments
- **Batch optimization** — Pricing tables for volume discounts
- **Flexible pricing presets** — Competitive (25%), Standard (40%), Premium (60%), Luxury (80%), Custom
- **Professional quotes** — PDF export with detailed breakdowns

### 🌍 Customization & Settings
- **Theme support** — Dark and light modes
- **Currency support** — 24 international currencies (DKK, USD, EUR, GBP, etc.)
- **Timezone configuration** — Accurate ETA display with automatic daylight saving time
- **Threshold customization** — Set your own low/critical stock levels
- **Data portability** — Full CSV import/export for backup and migration
- **Brand management** — Add and manage custom filament brands

---

## 🛠️ Tech Stack

| Component | Technology |
|-----------|-----------|
| **Framework** | Blazor Server (.NET 10) |
| **Database** | SQLite + Entity Framework Core |
| **MQTT Client** | MQTTnet (BambuLab integration) |
| **CSV Processing** | CsvHelper |
| **Container** | Docker + Docker Compose |
| **UI** | Custom CSS with dark/light themes |

---

## 🚀 Quick Start

### Option 1: Docker (Recommended) 🐳

**Prerequisites:** [Docker Desktop](https://www.docker.com/products/docker-desktop) installed and running

#### Windows PowerShell:
```powershell
cd FilamentTracker
.\deploy.ps1
```
*Opens browser automatically to http://localhost:5500*

#### Linux/Mac:
```bash
cd FilamentTracker
docker-compose up -d
```

Access the app at **http://localhost:5500**

### Option 2: Local .NET Development

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```bash
cd FilamentTracker
dotnet restore
dotnet run
```

Access the app at **https://localhost:5001**

The SQLite database is created automatically on first run.

---

## 🖨️ BambuLab MQTT Setup

### Initial Configuration

1. Go to **Settings** in the app
2. Enable **🖨️ BambuLab MQTT Live Tracking**
3. Enter your printer details:
   - **IP Address** — Found in printer's network settings
   - **Access Code** — Settings → Network → Access Code (8 digits)
   - **Serial Number** — Settings → Device
4. Click **💾 Save & Connect**

### Features Enabled
- ✅ Real-time print status in navigation bar
- ✅ Live progress, ETA, and temperature monitoring  
- ✅ AMS spool tracking and automatic weight updates
- ✅ WiFi signal strength indicator

### AMS Auto-Update Weight

When enabled, the app automatically updates spool weight based on AMS remaining percentage:

- **Only decrease mode** *(recommended)* — Protects manual measurements, only reduces weight
- **Two-way sync mode** — AMS value always wins, can increase weight if corrected

Configure this in **Settings** under **BambuLab MQTT Live Tracking**.

---

## 🐳 Docker Deployment

### Quick Commands

```bash
# Start the application
docker-compose up -d

# View logs in real-time
docker logs -f filament-tracker

# Stop the application
docker-compose down

# Restart the application
docker restart filament-tracker

# Update to latest version
docker-compose pull
docker-compose up -d
```

### Accessing from Other Devices

1. Find your server's IP address:
   - **Windows:** `ipconfig`
   - **Mac/Linux:** `ifconfig` or `ip addr`
2. Access from any device on your network: `http://SERVER-IP:5500`

**Example:** `http://192.168.1.100:5500`

### NAS Deployment (Synology, QNAP, Unraid)

#### Synology NAS

1. Install **Docker** package from Package Center
2. Create folder: `/docker/filament-tracker`
3. Upload `docker-compose.yml` to the folder
4. SSH into your NAS:
   ```bash
   cd /volume1/docker/filament-tracker
   sudo docker-compose up -d
   ```
5. Access via: `http://NAS-IP:5500`

#### QNAP NAS

1. Install **Container Station**
2. Upload `docker-compose.yml`
3. Create container from Container Station UI
4. Map port **5000** to external port **5500**
5. Add volume mount: `/app/data` → persistent storage

#### Unraid

1. Go to **Docker** tab
2. Add new container:
   - **Repository:** `filament-tracker:latest`
   - **Port:** `5500:5000`
   - **Path:** `/app/data` → `/mnt/user/appdata/filament-tracker`

---

## 💾 Data Management

### CSV Import/Export

**Export your data:**
1. Go to **Settings**
2. Click **📥 Export to CSV**
3. Save the file to your backup location

**Import data:**
1. Go to **Settings**  
2. Click **Choose File** under Import CSV
3. Select your CSV file
4. Click **📤 Import CSV**

---

### CSV Format Reference

```csv
Brand,Type,Finish,Color Name,Color Code,Total Weight (g),Weight Remaining (g),Quantity,Spool Type,Spool Material,Reusable Spool,Diameter (mm),Location,Notes,Date Added,Purchase Price Per Kg
Bambu Lab,PLA,Matte,Charcoal,#000000,1000,950,1,spool,plastic,Yes,1.75,Shelf A,Example notes,2026-02-16,149
```

**Field Reference:**

| Field | Valid Values | Notes |
|-------|-------------|-------|
| Type | `PLA`, `PETG`, `ABS`, `ASA`, `TPU`, `NYLON`, `PC`, etc. | Filament type |
| Finish | `Matte`, `Silk`, `Glossy`, `Metallic`, `Carbon`, etc. | Surface finish |
| Color Code | Hex code (e.g., `#000000`) | Must start with `#` |
| Spool Type | `spool` or `refill` | Whether it has a physical spool |
| Spool Material | `plastic`, `cardboard`, `none` | For reusable tracking |
| Reusable Spool | `Yes` or `No` | Track for refills |
| Diameter | `1.75` or `2.85` | Filament diameter in mm |
| Purchase Price Per Kg | Decimal number | Optional, defaults to 149 |

💡 **Tip:** Download a pre-filled template from **Settings → Download CSV Template**.

### Docker Volume Backup

**Create backup:**
```bash
docker run --rm \
  -v filament-tracker-data:/data \
  -v $(pwd):/backup \
  alpine tar czf /backup/filament-backup-$(date +%Y%m%d).tar.gz -C /data .
```

**Restore from backup:**
```bash
docker run --rm \
  -v filament-tracker-data:/data \
  -v $(pwd):/backup \
  alpine sh -c "cd /data && tar xzf /backup/filament-backup-YYYYMMDD.tar.gz"
```

---

## 📁 Project Structure

```
FilamentTracker/
├── Components/              # Blazor Razor components
│   ├── InventoryPage.razor     # Main inventory view with tiles
│   ├── AddFilamentPage.razor   # Add new filament form
│   ├── FilamentDetailModal.razor  # Edit filament/spools modal
│   ├── PrintCalculatorPage.razor  # Cost calculator
│   ├── AMSPage.razor           # BambuLab AMS integration
│   ├── SpoolsPage.razor        # Reusable spool management
│   ├── SettingsPage.razor      # App configuration
│   └── HelpPage.razor          # User documentation
├── Data/
│   └── FilamentContext.cs      # EF Core DbContext
├── Models/                   # Data models
│   ├── Filament.cs            # Main filament entity
│   ├── Spool.cs               # Individual spool tracking
│   ├── ReusableSpool.cs       # Reusable spool tracking
│   ├── Brand.cs               # Filament brands
│   ├── AppSettings.cs         # Application settings
│   └── PrintStatus.cs         # MQTT print status
├── Services/                 # Business logic
│   ├── FilamentService.cs      # Filament/spool CRUD
│   ├── BambuLabService.cs      # MQTT client for BambuLab
│   ├── CsvService.cs           # Import/export
│   ├── ThresholdService.cs     # Stock warnings
│   ├── ThemeService.cs         # Dark/light mode
│   └── EditStateService.cs     # UI state management
├── Pages/
│   └── Index.razor            # Main page with navigation
├── Shared/
│   ├── MainLayout.razor       # App layout
│   └── LoggingErrorBoundary.razor  # Error handling
├── wwwroot/                  # Static assets
│   ├── css/site.css           # Main stylesheet (dark/light themes)
│   └── js/                    # JavaScript files
│       ├── site.js            # General utilities
│       ├── calculator-app.js  # Calculator logic
│       └── calculator-pdf.js  # PDF generation
├── Dockerfile               # Container image definition
├── docker-compose.yml       # Container orchestration
├── deploy.ps1               # Windows PowerShell deployment
├── deploy.bat               # Windows batch deployment
├── .dockerignore            # Docker build exclusions
├── .gitignore               # Git exclusions
└── Program.cs               # Application entry point
```

---

## 🔧 Troubleshooting

### Common Issues

| Problem | Solution |
|---------|----------|
| **Port 5500 already in use** | Change port in `docker-compose.yml`: `"YOUR_PORT:5000"` |
| **Can't access from phone** | Use your PC's IP: `http://192.168.x.x:5500` |
| **Container won't start** | Check logs: `docker logs filament-tracker` |
| **Database errors** | Reset database: `docker volume rm filament-tracker-data` |
| **Build fails (.NET)** | Clean and rebuild: `dotnet clean && dotnet restore && dotnet build` |
| **MQTT not connecting** | Verify printer IP, access code, and serial number in Settings |
| **AMS not updating weight** | Enable "Auto-update spool weight from AMS" in Settings |

### Docker Desktop Not Running

**Error:** `Cannot connect to the Docker daemon`

**Fix:** 
1. Start Docker Desktop
2. Wait for it to fully initialize (green icon in system tray)
3. Run deployment script again

### Port Conflicts

If port 5500 is already in use by another application:

1. Edit `docker-compose.yml`
2. Change the port mapping:
   ```yaml
   ports:
     - "8080:5000"  # Use port 8080 instead
   ```
3. Restart: `docker-compose up -d`

---

## 🔄 Updating the Application

### Docker

```bash
cd FilamentTracker
docker-compose pull
docker-compose up -d
```

Your data is preserved in the Docker volume.

### Local .NET

```bash
git pull
cd FilamentTracker
dotnet restore
dotnet run
```

---

## 📜 License

**MIT License** — Free to use, modify, and distribute.

---

## 📅 Changelog

### v2.0 (2026-02-16)
- ✨ Updated to **.NET 10**
- ✨ Added **timezone support** with automatic DST handling (40+ timezones)
- ✨ **AMS auto-matching** now uses NFC/RFID `tag_uid` only (prevents false matches)
- ✨ **Warning badge** on AMS page when slot reports metadata but no UID
- ✨ Linking now **clears conflicting mappings** (each RFID/tag is unique)
- ✨ **Weight discrepancy warning** only shows when auto-update enabled
- 🐛 Fixed cascade delete for filaments and spools
- 🐛 Fixed UI refresh timing after deleting spools
- 🐛 Fixed live tracker compression for better mobile experience
- 📝 Consolidated documentation structure

### v1.0
- Initial release with core inventory tracking
- Print cost calculator
- CSV import/export
- Dark/light theme support

---

*Made with ❤️ for the 3D printing community* 🖨️
