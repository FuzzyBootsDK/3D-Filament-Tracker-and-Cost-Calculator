# Filament Tracker v2

A Blazor Server web app for tracking 3D printer filament inventory, managing spools, and calculating print costs.

---

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
| Framework | Blazor Server (.NET 8)         |
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
