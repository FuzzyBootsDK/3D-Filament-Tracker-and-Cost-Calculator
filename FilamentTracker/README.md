# Filament Tracker v2.0

> **📖 For complete documentation, see the [main README](../README.md) in the repository root.**

## Quick Start

### Docker (Recommended)
```bash
docker-compose up -d
```
Access at **http://localhost:5500**

### Local .NET
```bash
dotnet restore
dotnet run
```
Access at **https://localhost:5001**

---

## What's Included in This Directory

This is the main application directory containing:

- **Components/** — Blazor Razor components
- **Models/** — Data models (Filament, Spool, AppSettings, etc.)
- **Services/** — Business logic (FilamentService, BambuLabService, etc.)
- **wwwroot/** — Static assets (CSS, JavaScript)
- **Dockerfile** & **docker-compose.yml** — Container configuration
- **deploy.ps1** & **deploy.bat** — Deployment scripts

---

## Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | Application entry point, database initialization |
| `docker-compose.yml` | Docker container orchestration |
| `deploy.ps1` | Windows PowerShell deployment script |
| `Dockerfile` | Container image definition |

---

## Configuration

All configuration is done through the **Settings** page in the web app:

- **Thresholds** — Low/critical stock levels
- **Currency** — 24 international currencies
- **Timezone** — ETA calculation with automatic DST
- **BambuLab MQTT** — Printer integration settings
- **MQTT Relay** — Rebroadcast MQTT for ESP32 and other clients
- **Theme** — Dark or light mode

---

## Data Storage

- **Development:** `filaments.db` in this directory
- **Docker:** Persistent volume `filament-tracker-data`

---

For full documentation including:
- Detailed feature descriptions
- BambuLab MQTT setup
- NAS deployment guides
- CSV format reference
- Troubleshooting

**👉 See [../README.md](../README.md)**

---

## Changelog

- 2026-02-16 — Updated to .NET 10; base/SDK images and docs updated.
- 2026-02-16 — AMS auto-matching now uses NFC/RFID `tag_uid` only (prevents false matches when an
  untagged spool is placed into a slot that previously held a different spool).
- 2026-02-16 — UI: warning badge added on AMS page when a slot reports BambuLab metadata but no UID
  was read (prompts manual linking). The app still allows manual linking by tray when explicitly saved.
- 2026-02-16 — Linking now clears conflicting mappings so each RFID/tag is unique in the DB.


## ✨ Features

### Inventory

- 📦 Multi-spool tracking — multiple spools per filament colour
- ⚠️ Configurable low/critical stock warnings
- 🔄 Reusable spool management with automatic in-use tracking
- 🎨 Sort by colour brightness (dark → light)
- 📥 CSV import/export (backup & restore)
- 🌓 Dark/Light theme
- 🔍 Search by brand, colour, type, finish
- 📝 Per-spool usage recording

### Print Cost Calculator

- 🧮 Full cost breakdown — material, labour, machine, electricity, depreciation
- 🖨️ Bambu Lab printer profiles (X1C, P1S, A1, A1 mini, H2S, H2D, H2C)
- 🎨 Multi-material support
- 📊 Batch optimisation table
- 💰 Pricing presets — Competitive (25%), Standard (40%), Premium (60%), Luxury (80%), Custom
- 📑 PDF quote export

---

## 🛠️ Tech Stack

| Layer     | Technology                     |
|-----------|--------------------------------|
| Framework | Blazor Server (.NET 10)        |
| Database  | SQLite + Entity Framework Core |
| CSV       | CsvHelper                      |
| Container | Docker                         |

---

## 🚀 Quick Start

### Docker (recommended)

```bash
docker-compose up -d
```

App available at **http://localhost:5500**

### Local .NET

```bash
dotnet restore
dotnet run
```

App available at **https://localhost:5001**

The SQLite database is created automatically on first run.

---

## 📋 CSV Format

Both import and export use this format:

```
Brand,Type,Finish,Color Name,Color Code,Total Weight (g),Weight Remaining (g),Quantity,Spool Type,Spool Material,Reusable Spool,Diameter (mm),Location,Notes,Date Added,Purchase Price Per Kg
Bambu Lab,PLA,Matte,Charcoal,#000000,1000,1000,1,spool,plastic,Yes,1.75,Shelf A,Example notes,02/16/2026,149
```

### Field notes

| Field                 | Values                                  |
|-----------------------|-----------------------------------------|
| Spool Type            | `spool` or `refill`                     |
| Spool Material        | `plastic`, `cardboard`, `none`          |
| Reusable Spool        | `Yes` or `No`                           |
| Purchase Price Per Kg | Optional — defaults to `149` if missing |

Download a pre-filled template from **Settings → Download CSV Template**.

---

## 📁 Project Structure

```
FilamentTracker/
├── Components/             # All Razor page components
│   ├── InventoryPage.razor
│   ├── AddFilamentPage.razor
│   ├── FilamentDetailModal.razor
│   ├── PrintCalculatorPage.razor
│   ├── SpoolsPage.razor
│   ├── SettingsPage.razor
│   └── HelpPage.razor
├── Data/
│   └── FilamentContext.cs
├── Models/
│   ├── Filament.cs
│   ├── Spool.cs
│   ├── ReusableSpool.cs
│   ├── Brand.cs
│   └── AppSettings.cs
├── Services/
│   ├── FilamentService.cs
│   ├── CsvService.cs
│   ├── ThemeService.cs
│   └── ThresholdService.cs
├── wwwroot/
│   ├── css/site.css
│   └── js/
│       ├── site.js
│       ├── calculator-app.js
│       └── calculator-pdf.js
├── Dockerfile
├── docker-compose.yml
├── deploy.ps1
├── deploy.bat
└── DOCKER-README.md
```

---

## 🐳 Docker Management

```bash
# Start
docker-compose up -d

# Stop
docker-compose down

# View logs
docker logs -f filament-tracker

# Restart
docker restart filament-tracker
```

See [DOCKER-README.md](DOCKER-README.md) for the full deployment guide including NAS setup.

---

## 💾 Backup & Restore

**CSV (recommended):**

- Export: Settings → Export to CSV
- Import: Settings → Import CSV

**Docker volume backup:**

```bash
# Backup
docker run --rm \
  -v filament-tracker-data:/data \
  -v $(pwd):/backup \
  alpine tar czf /backup/filament-backup.tar.gz -C /data .

# Restore
docker run --rm \
  -v filament-tracker-data:/data \
  -v $(pwd):/backup \
  alpine sh -c "cd /data && tar xzf /backup/filament-backup.tar.gz"
```

---

## 🔧 Troubleshooting

| Problem                 | Fix                                                                |
|-------------------------|--------------------------------------------------------------------|
| Port 5500 in use        | Change `5500:5000` in `docker-compose.yml`                         |
| Can't access from phone | Use your PC's IP — `http://192.168.x.x:5500`                       |
| Container won't start   | `docker logs filament-tracker`                                     |
| Reset database          | Delete the Docker volume: `docker volume rm filament-tracker-data` |
| Local build fails       | `dotnet clean && dotnet restore && dotnet build`                   |

---

## License

MIT — free to use and modify.

---

*Made for the 3D printing community* 🖨️
