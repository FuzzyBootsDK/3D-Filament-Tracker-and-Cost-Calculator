# Filament Tracker — Architecture & How Everything Fits Together

## 1. What The App Is

Filament Tracker is a self-hosted **Blazor Server** web application (.NET 10) for 3D printing enthusiasts to:
- Track filament inventory (spools, brands, weights, costs)
- Calculate print costs
- Monitor BambuLab printers live via MQTT
- Sync AMS (Automatic Material System) slot data back to the inventory
- Relay MQTT messages to other devices (ESP32 etc.)

All data is stored in a local **SQLite** database. There is no cloud sync. It runs as a Docker container on a NAS or any machine.

---

## 2. Technology Stack

| Layer | Technology |
|---|---|
| Framework | Blazor Server (.NET 10) |
| Database | SQLite via EF Core (`IDbContextFactory`) |
| Real-time UI | Blazor SignalR circuit |
| MQTT client | MQTTnet (connects to BambuLab printer) |
| MQTT server | MQTTnet.Server (relay for ESP32 etc.) |
| Styling | CSS custom properties, no framework |
| Deployment | Docker / docker-compose |

---

## 3. Project Structure

```
FilamentTracker/
├── Program.cs                    # App entry point, DI registration, DB init, startup connections
├── Data/
│   └── FilamentContext.cs        # EF Core DbContext — all tables and relationships
├── Models/
│   ├── Filament.cs               # Filament record (brand, color, type, price)
│   ├── Spool.cs                  # Individual spool (weight, AMS IDs, purchase price)
│   ├── ReusableSpool.cs          # Tracks physical reusable spool shells
│   ├── Brand.cs                  # Brand name registry
│   ├── AppSettings.cs            # App-wide config (thresholds, MQTT, timezone, relay)
│   └── PrintStatus.cs            # Live printer state + AMSUnit/AMSSlot models
├── Services/
│   ├── FilamentService.cs        # All CRUD for filaments, spools, brands, settings, AMS linking
│   ├── BambuLabService.cs        # MQTT client — connects to printer(s), fires events
│   ├── MqttRelayService.cs       # MQTT broker — rebroadcasts printer messages
│   ├── ThemeService.cs           # Dark/light mode state (singleton)
│   ├── ThresholdService.cs       # Low/critical weight thresholds (singleton)
│   ├── EditStateService.cs       # Prevents inventory refresh during active modal edits
│   └── CsvService.cs             # CSV import/export logic
├── Pages/
│   └── Index.razor               # Shell page: top nav + live tracking widget + page router
├── Components/
│   ├── InventoryPage.razor       # Filament grid, search/filter, detail modal
│   ├── AddFilamentPage.razor     # Add new filament form
│   ├── SpoolsPage.razor          # Reusable spool management
│   ├── AMSPage.razor             # AMS slot viewer + spool linking
│   ├── PrintCalculatorPage.razor # Cost calculator
│   ├── MqttPage.razor            # Live MQTT message log
│   ├── SettingsPage.razor        # All settings: theme, currency, timezone, MQTT, relay
│   └── HelpPage.razor            # In-app documentation
├── wwwroot/
│   └── css/
│       └── site.css              # All CSS — theme variables, components, responsive rules
└── memory/                       # AI session memory (gitignored)
    ├── preferences.md
    ├── decisions.md
    ├── suggestions.md
    ├── thoughts.md
    ├── design-guide.md
    └── architecture.md           # ← this file
```

---

## 4. Startup Flow (`Program.cs`)

`Program.cs` is the heart of bootstrapping. It runs top to bottom on every app start:

```
1. Register services (DI container)
   ├── AddRazorPages + AddServerSideBlazor (Blazor circuit settings)
   ├── AddDbContextFactory<FilamentContext> (SQLite, path-aware for Docker)
   ├── AddScoped: FilamentService, CsvService
   └── AddSingleton: ThemeService, ThresholdService, BambuLabService,
                     MqttRelayService, EditStateService

2. Database initialisation
   ├── EnsureCreatedAsync() — creates schema if missing
   ├── CREATE TABLE IF NOT EXISTS — defensive table creation for older DBs
   ├── ColumnExistsAsync() helper — adds missing columns without breaking existing data
   │   (Handles schema migrations manually since no EF migrations are used)
   └── PRAGMA foreign_keys = ON — enable cascade delete

3. Load AppSettings from DB
   ├── Apply environment variable overrides (BAMBULAB_IP, BAMBULAB_CODE, etc.)
   │   (saves overrides back to DB so they persist)
   ├── ThresholdService.SetThresholds(low, critical)
   ├── If BambuLabEnabled → BambuLabService.ConnectAsync() (background Task.Run)
   └── If MqttRelayEnabled → MqttRelayService.StartRelayServerAsync() (background Task.Run)

4. app.Run() — start HTTP server
```

**Key design note:** No EF Core Migrations. Schema changes are handled by defensive `ALTER TABLE` statements checked on every startup via `ColumnExistsAsync`. This is intentional for simplicity in a single-user Docker deployment.

---

## 5. Data Layer

### 5.1 Models

| Model | Table | Purpose |
|---|---|---|
| `Filament` | `Filaments` | One record per filament type (brand + color + type combination). Has computed properties: `WeightRemaining`, `TotalWeight`, `PercentRemaining`, `SpoolCount`, `CalculatedPricePerKg`, `Status` |
| `Spool` | `Spools` | One record per physical spool. FK → `Filament`. Tracks `WeightRemaining`, `TotalWeight`, AMS RFID IDs (`AmsTrayUuid`, `AmsTagUid`), `PurchasePricePerKg`, `DateEmptied` |
| `ReusableSpool` | `ReusableSpools` | Tracks the physical spool shell (not the filament). FK → `Spool` (nullable, set null on delete). When a spool empties, `InUse = false`, `CurrentSpoolId = null` |
| `Brand` | `Brands` | Simple name registry. Populated via Settings page, used in dropdowns |
| `AppSettings` | `AppSettings` | Single-row config table. ID always = 1 by convention |
| `PrintStatus` | *(in-memory only)* | Live state from MQTT. Not persisted. Nested: `AMSUnit[]` → `AMSSlot[]` |

### 5.2 Relationships

```
Filament (1) ──has many──> Spool (*)
  └─ ON DELETE CASCADE: deleting a Filament deletes all its Spools

Spool (0..1) <──FK──── ReusableSpool
  └─ ON DELETE SET NULL: deleting a Spool nullifies ReusableSpool.CurrentSpoolId
```

### 5.3 FilamentContext

`FilamentContext` is registered as `IDbContextFactory<FilamentContext>` (not as a scoped `DbContext`). This is **critical**:
- `DbContext` is not thread-safe
- `BambuLabService` runs on MQTT background threads
- The factory pattern lets each operation create and immediately dispose its own `DbContext`
- Always: `await using var context = await contextFactory.CreateDbContextAsync();`

### 5.4 FilamentService

The single data-access facade for all UI components. Key method groups:

| Group | Methods |
|---|---|
| Filaments | `GetAllFilamentsAsync`, `GetFilamentByIdAsync`, `AddFilamentAsync`, `UpdateFilamentAsync`, `DeleteFilamentAsync` |
| Spools | `AddSpoolAsync`, `AddSpoolToFilamentAsync`, `UpdateSpoolAsync`, `DeleteSpoolAsync` |
| Reusable Spools | `GetReusableSpoolsAsync`, `AddReusableSpoolAsync`, `UpdateReusableSpoolAsync`, `DeleteReusableSpoolAsync` |
| Brands | `GetBrandsAsync`, `AddBrandAsync`, `DeleteBrandAsync` |
| Settings | `GetSettingsAsync`, `SaveSettingsAsync` |
| Statistics | `GetStatisticsAsync` (returns dict: Total / Low / Critical / Ok counts) |
| AMS Linking | `FindSpoolByAmsIdAsync`, `LinkSpoolToAmsSlotAsync` |
| Data Management | `PurgeDatabaseAsync`, CSV methods (via `CsvService`) |

**Reusable spool auto-tracking:** When a spool with `IsReusable = true` is added or updated, `FilamentService` automatically creates/updates a `ReusableSpool` record. When weight drops to 0 or `DateEmptied` is set, it marks the shell as available.

---

## 6. Service Layer

### 6.1 ThemeService (Singleton)
Holds `IsDarkMode` (default: `true`). Fires `OnThemeChanged` event. All pages subscribe via `Index.razor` which applies `"dark"` or `"light"` CSS class to the body. No persistence — resets to dark on app restart.

### 6.2 ThresholdService (Singleton)
Holds `LowThreshold` (default: 500g) and `CriticalThreshold` (default: 250g). Loaded from DB at startup in `Program.cs`. `GetStatus(weightRemaining)` returns `"ok"` / `"low"` / `"critical"`. `InventoryPage` subscribes to `OnThresholdsChanged` to refresh tile colors without a page reload.

### 6.3 EditStateService (Singleton)
Tracks whether a filament detail modal is currently open (`IsEditing`, `CurrentFilamentId`). Prevents `InventoryPage`'s MQTT-triggered `StateHasChanged` from refreshing the list mid-edit, which would collapse the open modal.

### 6.4 BambuLabService (Singleton)
The most complex service. Manages **multiple simultaneous MQTT connections** to BambuLab printers.

**Internal state (all guarded by `_lock`):**
```csharp
Dictionary<int, IMqttClient>            _mqttClients        // printerId → MQTT client
Dictionary<int, PrintStatus>            _printerStatuses    // printerId → live status
Dictionary<int, CancellationTokenSource> _cancellationTokens
Dictionary<int, string>                  _printerNames
Dictionary<int, string>                  _printerSerialNumbers
Queue<MqttLogEntry>                      _mqttLog            // last 50 messages
```

**Events:**
```csharp
event Action<int, PrintStatus?>  OnStatusUpdated      // fired on every state change
event Action<MqttLogEntry>       OnMqttMessageLogged  // fired on every raw message
event Action<int>                OnAmsWeightUpdated   // fired after AMS auto-weight save
```

**Connection lifecycle:**
1. `ConnectToPrinterAsync(printerId, ip, accessCode, serial, name)`
   - Creates MQTT client with TLS (BambuLab uses self-signed certs → `WithIgnoreCertificateChainErrors`)
   - Registers `ConnectedAsync`, `DisconnectedAsync`, `ApplicationMessageReceivedAsync` handlers
   - Subscribes to topic: `device/{serialNumber}/report`
   - On `DisconnectedAsync`: if reason ≠ `NormalDisconnection`, waits 5s then auto-reconnects
2. `DisconnectFromPrinterAsync(printerId)` — cancels CTS, disconnects, disposes, removes from all dicts
3. `DisposeAsync()` — disconnects all, disposes all clients

**Message parsing (`OnMessageReceived`):**
Parses JSON payload. Looks for `print` → `gcode_state`, `mc_percent`, `layer_num`, `total_layer_num`, `mc_remaining_time`, `gcode_file`, `bed_temper`, `nozzle_temper`, `wifi_signal`, `ams.ams[]`.

**AMS auto-weight (`ApplyAmsWeightUpdatesAsync`):**
After every AMS update, if `AmsAutoUpdateWeight = true`, runs in a background `Task.Run`. Uses a fresh scoped `DbContext`. Only updates spools with a valid non-zero RFID `TagUid`. Respects `AmsAutoUpdateOnlyDecrease` flag.

**Null-safety rule:** All four `OnStatusUpdated?.Invoke(...)` call sites check the status is non-null before invoking. The event signature is `Action<int, PrintStatus?>` — subscribers must also null-check.

### 6.5 MqttRelayService (Singleton)
Acts as an MQTT **broker** (server), not a client. Subscribes to `BambuLabService.OnMqttMessageLogged` and republishes every message to all connected relay clients (e.g. ESP32 devices). Configurable port (default 1883), optional username/password auth. Runs independently of the main MQTT client.

### 6.6 CsvService (Scoped)
Handles CSV export (filaments → CSV rows) and import (CSV rows → create Filaments + Spools). Injected into `FilamentService`-adjacent operations and called from `SettingsPage`.

---

## 7. UI Layer

### 7.1 The Shell: `Index.razor`

`Index.razor` is the **single page** the browser ever loads. It owns:
- The top navigation bar with 8 `navbtn` buttons
- The live tracking widget (right side of nav)
- A page router (`@if (currentPage == "inventory")`) that conditionally renders child components

**Page routing is manual** — no Blazor router, no URL changes. `SetPage(string page)` sets a string variable and the `@if` chain swaps which component is rendered. This keeps navigation instant and state within the session.

**Live tracking widget** subscribes to `BambuLabService.OnStatusUpdated` on `OnInitializedAsync` and unsubscribes on `Dispose`. Updates `isConnected` and `currentPrint` (the `PrintStatus`). Renders connection state, progress bar, ETA, layer count, temperatures.

**`HandleStatusUpdate` pattern** (same pattern used across all pages that subscribe):
```csharp
private void HandleStatusUpdate(int printerId, PrintStatus? status)
{
    if (status == null) return;                  // null-guard (race condition protection)
    try {
        InvokeAsync(() => {                       // marshal to Blazor's sync context
            try {
                UpdateStatus(status);
                StateHasChanged();               // trigger re-render
            } catch { /* component disposed */ }
        });
    } catch { /* protect MQTT thread */ }
}
```

### 7.2 InventoryPage.razor

The main view. Renders a responsive CSS grid of `tile` cards, one per filament.

**Data flow:**
1. `OnInitializedAsync` → `FilamentService.GetAllFilamentsAsync()` (includes active spools)
2. `ThresholdService.OnThresholdsChanged` → `FilterFilaments()` + `StateHasChanged()`
3. `BambuLabService.OnAmsWeightUpdated` → reload all filaments (weights may have changed)
4. `EditStateService` → checked before MQTT-triggered refreshes to avoid disrupting open modals

**Filtering chain:** Search text + type filter + sort option all feed `FilterFilaments()` which produces `_filteredFilaments` (local list). Runs client-side in memory.

**Tile status:** Each tile gets a CSS class (`ok` / `low` / `critical`) from `ThresholdService.GetStatus(filament.WeightRemaining)`. The left-edge status rail color is controlled by this class.

**Detail modal:** Clicking a tile opens a full edit modal with spool management (add/remove/edit individual spools), usage recording, AMS data preview, and delete.

### 7.3 AMSPage.razor

Shows the AMS units and their slots from the live `PrintStatus`. Each slot can be **linked to an inventory spool** — stored as `AmsTrayUuid` / `AmsTagUid` on the `Spool` record.

**Auto-matching:** On every status update, `AutoMatchSlotsAsync()` calls `FilamentService.FindSpoolByAmsIdAsync()` for each slot. If a spool with a matching `AmsTagUid` is found (RFID match), it auto-populates the dropdown. A `HashSet` prevents the same spool from being auto-matched to multiple slots.

**Slot key:** `SlotKey(unit, slot)` produces a string like `"0-1"` (AMS unit 0, slot 1) used as the dictionary key for `_slotSpoolMap` and `_slotSaveMsg`.

**`HandleAmsWeightUpdated`** uses `Action<int>` (printer ID only) — needs updating to match the current signature.

### 7.4 SettingsPage.razor

Handles:
- Theme toggle (calls `ThemeService.SetDarkMode()`)
- Currency save (persists to `AppSettings` in DB)
- Timezone save (persists to `AppSettings`, used by `Index.razor` ETA calculation)
- Threshold save (persists to DB + calls `ThresholdService.SetThresholds()` to update runtime state immediately)
- BambuLab MQTT config (IP, access code, serial, per-printer management)
- AMS auto-weight settings
- MQTT Relay config (enable/disable, port, credentials)
- CSV export/import
- Database purge

### 7.5 Other Pages

| Page | Purpose |
|---|---|
| `AddFilamentPage.razor` | Form to add new filament entries with one or more spools |
| `SpoolsPage.razor` | Manage reusable spool shells (available/in-use) |
| `PrintCalculatorPage.razor` | Calculate print cost from weight + filament price + electricity |
| `MqttPage.razor` | Live view of last 50 raw MQTT messages from all connected printers |
| `HelpPage.razor` | Fully in-app documentation, no external links required |

---

## 8. Data Flow Diagrams

### 8.1 Inventory Update Cycle

```
User edits spool weight
    └─> FilamentService.UpdateSpoolAsync()
        └─> DB write
            └─> InventoryPage.LoadFilaments()
                └─> StateHasChanged() → re-render
```

### 8.2 Live Print Status Update Cycle

```
BambuLab Printer
    └─> MQTT TLS message → BambuLabService.OnMessageReceived()
        ├─> Parse JSON payload
        ├─> Update _printerStatuses[printerId] (in-memory)
        ├─> OnMqttMessageLogged?.Invoke()  ──> MqttRelayService (rebroadcast)
        │                                  └─> MqttPage (display log)
        └─> OnStatusUpdated?.Invoke(printerId, status)
            ├─> Index.razor.HandleStatusUpdate()     → live widget re-render
            ├─> AMSPage.razor.HandleStatusUpdated()  → AMS slot refresh
            └─> (any other subscriber)
```

### 8.3 AMS Auto-Weight Sync Cycle

```
MQTT message contains AMS data
    └─> BambuLabService.OnMessageReceived()
        └─> ParseAmsUnits() → status.AMSUnits updated
            └─> if AmsAutoUpdateWeight:
                └─> Task.Run(ApplyAmsWeightUpdatesAsync())
                    ├─> For each slot with valid TagUid:
                    │   └─> Find matching spool in DB by AmsTagUid
                    │       └─> Calculate new weight from AMS remain%
                    │           └─> If change > 0.5g (and passes OnlyDecrease check):
                    │               └─> DB write (spool.WeightRemaining)
                    └─> OnAmsWeightUpdated?.Invoke(printerId)
                        ├─> InventoryPage → reload filaments
                        └─> AMSPage → reload inventory
```

### 8.4 Settings Save + Live Effect

```
SettingsPage saves thresholds
    └─> FilamentService.SaveSettingsAsync()     (DB write)
        └─> ThresholdService.SetThresholds()   (in-memory update)
            └─> OnThresholdsChanged event
                └─> InventoryPage subscriber
                    └─> FilterFilaments() + StateHasChanged()
                        └─> Tile colors update instantly without page reload
```

---

## 9. Threading Model

Blazor Server runs on a single synchronization context per circuit (browser tab). However:

- MQTT callbacks arrive on **MQTTnet's thread pool threads** — not the Blazor thread
- `IDbContextFactory` calls from `ApplyAmsWeightUpdatesAsync` run on **background Task threads**

**Rules:**
1. All UI state mutations go through `InvokeAsync(() => { ... StateHasChanged(); })` when called from non-Blazor threads
2. `DbContext` is never shared — always `await using var context = await contextFactory.CreateDbContextAsync()`
3. `_printerStatuses` dictionary is always accessed inside `lock (_lock)`
4. Event handlers swallow exceptions to protect the calling thread (MQTT thread must not crash)

---

## 10. Docker Deployment

```yaml
# docker-compose.yml
services:
  filament-tracker:
    ports:
      - "5500:5000"   # Web UI
      - "1883:1883"   # MQTT Relay (optional)
    volumes:
      - filament-data:/app/data    # SQLite DB persisted here
      - ./logs:/app/logs
    environment:
      - ASPNETCORE_URLS=http://+:5000
      # Optional: configure printer without touching Settings page
      - BAMBULAB_ENABLED=true
      - BAMBULAB_IP=192.168.1.100
      - BAMBULAB_CODE=12345678
      - BAMBULAB_SERIAL=01P00A000000000
```

**DB path logic (`Program.cs`):**
```csharp
var dbPath = config.GetConnectionString("DefaultConnection")
          ?? (Directory.Exists("/app/data") ? "Data Source=/app/data/filaments.db"
                                            : "Data Source=filaments.db");
```
Inside Docker `/app/data` exists (mounted volume) → uses that path. Locally → uses `filaments.db` in the working directory.

**Environment variable overrides:** Any env var set for `BAMBULAB_*` or `MQTT_RELAY_*` is written back to the DB at startup so they persist even after the env var is removed. This makes initial Docker setup easy without navigating to Settings.

---

## 11. Key Patterns & Conventions

| Pattern | Where | Why |
|---|---|---|
| `IDbContextFactory` everywhere | All services | Thread-safety for background MQTT work |
| Manual schema migrations | `Program.cs` | Simpler than EF migrations for a single-user app |
| Singleton services with events | Theme, Threshold, BambuLab | Share state across Blazor components without cascading parameters |
| `InvokeAsync` + `StateHasChanged` | All MQTT handlers | Marshal background thread updates to Blazor's sync context |
| Null-guard before event invoke | `BambuLabService` | Prevent `NullReferenceException` when `TryGetValue` fails (race with disconnect) |
| `EditStateService` flag | `InventoryPage` | Prevent modal collapse during live MQTT refreshes |
| Manual page routing in `Index.razor` | Shell page | No URL changes, instant navigation, session state preserved |
| RFID-only AMS auto-matching | `FilamentService`, `BambuLabService` | `TrayUuid` is position-based and can false-match; `TagUid` is chip-bound |
