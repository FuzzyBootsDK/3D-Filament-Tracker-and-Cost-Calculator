# 3D Filament Tracker Refactor Plan

## Goal

Refactor and simplify the project **without changing the user-visible behavior**: same routes, same screens, same workflows, same calculations, same CSV format, same database compatibility, and same BambuLab/MQTT functionality. The current application is a Blazor Server app on .NET 10 using SQLite/EF Core, CsvHelper, Docker, and MQTT integration. The repo is organized around `Components`, `Data`, `Models`, `Services`, `Pages`, `Shared`, and `wwwroot`, with the main complexity concentrated in `Program.cs`, `FilamentService.cs`, `BambuLabService.cs`, and several page components. citeturn110929view0turn678208view0turn954246view0turn188810view0turn188810view1turn188810view2turn652173view0

---

## 1. What must not change

These are the **compatibility boundaries**. Every refactor step must preserve them.

### 1.1 User-facing behavior to preserve

- Inventory management behavior, including multi-spool tracking, stock warnings, reusable spools, filtering, color sorting, per-spool usage, and bulk workflows. citeturn110929view0
- BambuLab integration behavior, including live status, AMS slot linking, automatic weight updates, ETA/timezone handling, and connection diagnostics. citeturn110929view0turn678208view0turn652173view2
- Print calculator behavior, including printer profiles, multi-material support, pricing presets, batch optimization, and PDF export. citeturn110929view0turn222792view4
- Settings semantics, including thresholds, currency, timezone, BambuLab settings, MQTT relay settings, and theme behavior. citeturn678208view0turn652173view2
- CSV import/export compatibility. `CsvService` exports one record per spool and imports by grouping records into filaments and spools; that file shape must remain compatible. citeturn110929view0turn652173view5

### 1.2 Technical contracts to preserve

- Same route/component surface: `AMSPage`, `AddFilamentPage`, `InventoryPage`, `PrintCalculatorPage`, `SettingsPage`, `SpoolsPage`, and related modal/components should keep working from the user’s perspective. citeturn188810view0
- Same core domain model concepts: `Filament`, `Spool`, `ReusableSpool`, `Brand`, `AppSettings`, `PrintStatus`. citeturn188810view1
- Same database compatibility with older SQLite files. `Program.cs` currently uses `EnsureCreated`, `CREATE TABLE IF NOT EXISTS`, many defensive `ALTER TABLE ... ADD COLUMN` checks, backfills defaults, and cleans orphan reusable spools. That behavior is ugly but currently part of compatibility. Do not remove it until replaced by an equally safe compatibility layer. citeturn222792view0
- Same AMS identity semantics. The repo documentation states AMS auto-matching now uses RFID/NFC `tag_uid` only for automatic matching, while manual linking by tray is still supported when explicitly saved. citeturn678208view0turn222792view1
- Same environment-variable override semantics for container startup for BambuLab and MQTT relay configuration. citeturn222792view0

---

## 2. Diagnosis: where the current complexity lives

### 2.1 `Program.cs` is doing too much

`Program.cs` currently handles:

- Blazor/SignalR configuration
- DB path resolution
- EF Core registration
- service registration
- database creation
- ad hoc schema migration
- schema backfill/default repair
- settings bootstrap
- environment variable overrides
- threshold initialization
- BambuLab startup connection
- MQTT relay startup
- request pipeline configuration. citeturn222792view0

That file is the first thing to simplify, because it is coupling **startup policy**, **database compatibility**, **settings loading**, and **background service bootstrapping** into one long block. citeturn222792view0

### 2.2 `FilamentService.cs` is almost certainly a god service

The repo positions `FilamentService` as the main business-logic service, and the visible snippets show it contains AMS identification logic such as `FindSpoolByAmsIdAsync` and tag/tray matching rules. Given the app features and current directory structure, this service is almost certainly handling several distinct responsibilities: inventory CRUD, spool operations, reusable spool lifecycle, statistics, brand management, AMS linkage, and settings-adjacent logic. That concentration should be broken apart. citeturn678208view0turn954246view0turn222792view1

### 2.3 `BambuLabService.cs` appears to mix transport, parsing, state, persistence, and events

The observable public surface already shows state flags, events, weight sync policy, and async disposal. Combined with the README feature list, this indicates one service is likely managing MQTT connection lifecycle, printer status state, message logging, AMS sync decisions, and DB writes. That should be separated into smaller units. citeturn110929view0turn222792view2

### 2.4 The Blazor pages are too “smart”

`InventoryPage.razor` and `PrintCalculatorPage.razor` are large UI surfaces with nontrivial logic. The inventory page presents dashboard counters, low/critical state, filtering/sorting, and likely AMS discrepancy display logic. The print calculator page injects `IJSRuntime` and `FilamentService`, indicating a page that likely combines rendering, state orchestration, calculator setup, and JS integration. Those pages should become thin shells over page-specific state and application services. citeturn222792view3turn222792view4

### 2.5 Domain models contain behavior that belongs elsewhere

`Filament` contains computed totals, pricing fallback logic, status thresholds, and UI-oriented summary properties. `Spool` contains derived state like `IsEmpty` and `PercentRemaining`. These are not wrong, but `Filament.Status` uses hard-coded threshold values (`250`, `500`) even though thresholds are configurable in `AppSettings` and loaded into `ThresholdService`, which is a signal that threshold evaluation logic should move out of the entity and into a domain/application rule service. citeturn652173view2turn652173view3turn652173view4turn954246view0turn678208view0

---

## 3. Refactor strategy

Do **not** rewrite. Use a staged **strangler refactor**:

1. Freeze behavior.
2. Introduce seams.
3. Move responsibilities behind interfaces.
4. Preserve old routes/components while replacing internals.
5. Only delete old code after parity tests pass.

This must be done in phases so the app remains runnable after every phase.

---

## 4. Target architecture

## 4.1 High-level structure

Recommended target structure:

```text
FilamentTracker/
  Application/
    Inventory/
      Commands/
      Queries/
      Dtos/
      InventoryService.cs
      SpoolService.cs
      BrandService.cs
      ReusableSpoolService.cs
      InventoryStatisticsService.cs
    Ams/
      AmsLinkService.cs
      AmsMatchingService.cs
      AmsWeightSyncService.cs
      AmsSlotSnapshot.cs
    Calculator/
      PrintCostCalculatorService.cs
      PrinterProfileService.cs
      QuoteExportService.cs
      CalculatorPayloadBuilder.cs
      Dtos/
    Settings/
      AppSettingsService.cs
      EnvironmentSettingsOverrideService.cs
      ThresholdPolicy.cs
      CurrencyCatalog.cs
      TimeZoneService.cs
    Common/
      Interfaces/
      Results/
      Validation/
  Domain/
    Entities/
      Filament.cs
      Spool.cs
      ReusableSpool.cs
      Brand.cs
      AppSettings.cs
      PrintStatus.cs
    Rules/
      StockStatusEvaluator.cs
      PriceCalculationPolicy.cs
      ReusableSpoolRules.cs
    ValueObjects/
      StockThresholds.cs
      Money.cs
      FilamentColor.cs
      AmsIdentity.cs
  Infrastructure/
    Data/
      FilamentContext.cs
      Configurations/
      Startup/
        DatabasePathResolver.cs
        DatabaseInitializer.cs
        DatabaseCompatibilityMigrator.cs
        SeedDataService.cs
        StartupSettingsLoader.cs
      Repositories/
        FilamentRepository.cs
        SpoolRepository.cs
        BrandRepository.cs
        SettingsRepository.cs
        ReusableSpoolRepository.cs
    Messaging/
      BambuLab/
        BambuLabClient.cs
        BambuLabConnectionManager.cs
        BambuMessageParser.cs
        BambuStatusStore.cs
        BambuEventDispatcher.cs
        BambuStartupHostedService.cs
      MqttRelay/
        MqttRelayService.cs
        MqttRelayHostedService.cs
    Csv/
      CsvImportService.cs
      CsvExportService.cs
      CsvSchema.cs
    Pdf/
      QuotePdfService.cs
  UI/
    Components/
      Pages/
      Shared/
      Inventory/
      Calculator/
      Ams/
      Settings/
    State/
      InventoryPageState.cs
      PrintCalculatorPageState.cs
      AmsPageState.cs
      SettingsPageState.cs
    Mapping/
      ViewModelMappers.cs
  Program.cs
```

This structure separates **domain**, **application orchestration**, **infrastructure**, and **UI state** while still fitting a Blazor Server app. It also lets you keep the current component routes while replacing the internals behind them. The need for this split is supported by the current concentration of logic in `Program.cs`, `Services/`, and `Components/`. citeturn678208view0turn954246view0turn188810view0

---

## 5. Exact class splits

## 5.1 Split `Program.cs`

### Current responsibilities to extract

From the observable code, extract these units out of `Program.cs`: citeturn222792view0

1. `ServiceCollectionExtensions.AddAppServices()`
2. `DatabasePathResolver.ResolveConnectionString(IConfiguration)`
3. `DatabaseInitializer.EnsureDatabaseCreatedAsync()`
4. `DatabaseCompatibilityMigrator.ApplyCompatibilityUpdatesAsync()`
5. `SeedDataService.EnsureDefaultsAsync()`
6. `StartupSettingsLoader.LoadAsync()`
7. `EnvironmentSettingsOverrideService.ApplyAsync(AppSettings)`
8. `ThresholdBootstrapper.InitializeAsync()`
9. `BambuLabStartupService.TryStartAsync()`
10. `MqttRelayStartupService.TryStartAsync()`
11. `ApplicationBuilderExtensions.ConfigureHttpPipeline()`

### Target `Program.cs`

The final `Program.cs` should become roughly this shape:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddUi()
    .AddData(builder.Configuration)
    .AddInventoryServices()
    .AddAmsServices()
    .AddCalculatorServices()
    .AddSettingsServices()
    .AddCsvServices()
    .AddBambuLabServices()
    .AddMqttRelayServices()
    .AddStartupServices();

var app = builder.Build();

await app.Services.InitializeApplicationAsync();

app.ConfigureHttpPipeline();
app.Run();
```

### Why this matters

This removes startup fragility and makes DB bootstrap, compatibility migration, settings overrides, and background startup independently testable. Right now they are not. citeturn222792view0

---

## 5.2 Split `FilamentService`

### Likely current responsibilities

Based on repo capabilities and the AMS matching snippet, `FilamentService` should be assumed to contain these concerns: inventory CRUD, filament retrieval, spool CRUD, brand operations, reusable spool tracking, dashboard statistics, low stock computation, AMS linking/matching, and probably helper methods for calculator inventory lookup. citeturn110929view0turn954246view0turn222792view1turn222792view4

### Target split

#### `InventoryService`

Owns filament-level operations:

- add filament
- update filament metadata
- delete filament
- get filament detail
- list filaments
- search/filter inventory
- compute inventory projections for UI

Example interface:

```csharp
public interface IInventoryService
{
    Task<IReadOnlyList<FilamentListItemDto>> GetInventoryAsync(InventoryFilter filter, CancellationToken ct = default);
    Task<FilamentDetailDto?> GetFilamentAsync(int filamentId, CancellationToken ct = default);
    Task<int> CreateFilamentAsync(CreateFilamentCommand command, CancellationToken ct = default);
    Task UpdateFilamentAsync(UpdateFilamentCommand command, CancellationToken ct = default);
    Task DeleteFilamentAsync(int filamentId, CancellationToken ct = default);
}
```

#### `SpoolService`

Owns spool-level operations:

- add spool to filament
- edit spool
- mark empty
- record usage/decrement weight
- attach/detach reusable spool
- set purchase price

```csharp
public interface ISpoolService
{
    Task<int> AddSpoolAsync(AddSpoolCommand command, CancellationToken ct = default);
    Task UpdateSpoolAsync(UpdateSpoolCommand command, CancellationToken ct = default);
    Task RecordUsageAsync(RecordSpoolUsageCommand command, CancellationToken ct = default);
    Task MarkEmptyAsync(int spoolId, CancellationToken ct = default);
    Task LinkReusableSpoolAsync(LinkReusableSpoolCommand command, CancellationToken ct = default);
}
```

#### `ReusableSpoolService`

Owns reusable spool inventory and state transitions:

- create reusable spool
- mark in use / available
- repair orphaned state
- list reusable spool stock

This extracts the logic currently partially hinted at by the startup orphan cleanup query. citeturn222792view0

#### `BrandService`

Owns brand list maintenance:

- list brands
- create brand
- rename/delete brand if supported

#### `InventoryStatisticsService`

Owns:

- total count
- low/critical count
- by-type summaries
- by-location summaries if needed

This should supply the values shown on `InventoryPage`. citeturn222792view3

#### `AmsLinkService`

Owns explicit user-driven spool-slot linking and conflict resolution:

- link spool to AMS identifiers
- unlink spool
- clear conflicting mappings so each tag remains unique

The repo changelog explicitly mentions conflicting mapping cleanup for uniqueness. That belongs here, not in a general inventory service. citeturn678208view0

#### `AmsMatchingService`

Owns auto-matching logic:

- `FindSpoolByAmsIdAsync`
- tag-based matching policy
- tray-as-manual-only support
- ambiguous match handling
- “no UID present” behavior

The repo docs and code snippet make this separation necessary. Automatic matching is a distinct policy area. citeturn678208view0turn222792view1

### Migration sequence for `FilamentService`

1. Keep `FilamentService` class alive temporarily.
2. Extract one method group at a time into new services.
3. Let `FilamentService` delegate to them.
4. Update callers page-by-page.
5. Delete `FilamentService` only after no callers remain.

This avoids a big-bang change.

---

## 5.3 Split `BambuLabService`

### Current concern bundle

Observable signals show `BambuLabService` carries settings flags, connection state, events, logging hooks, and AMS update events. Combined with the feature set, it likely also parses incoming MQTT messages and persists spool/status updates. citeturn110929view0turn222792view2

### Target split

#### `IBambuLabClient` / `BambuLabClient`

Low-level MQTT client wrapper:

- connect/disconnect
- subscribe/publish
- raw message event
- reconnect policy hooks

#### `BambuMessageParser`

Pure parser:

- convert raw MQTT payloads into typed domain/application events
- parse status, AMS info, temperatures, progress, ETA, Wi-Fi strength
- isolate JSON schema handling from everything else

#### `BambuStatusStore`

In-memory state holder for UI:

- current `PrintStatus`
- AMS slot snapshots
- connection status
- latest diagnostics/log lines

This should be the single readable source for nav/status displays.

#### `AmsWeightSyncService`

Persistence policy service:

- reads `AmsAutoUpdateWeight`
- reads `AmsAutoUpdateOnlyDecrease`
- updates `Spool.WeightRemaining`
- enforces “decrease only” option
- raises “weights saved” event

The current public properties strongly suggest this logic currently lives inside `BambuLabService`; it should not. citeturn222792view2turn652173view2

#### `BambuEventDispatcher`

Maps parser outputs to:

- UI state updates
- status events
- AMS sync calls
- log notifications

#### `BambuLabStartupHostedService`

Starts connection after app boot using loaded settings instead of `Task.Run` inside `Program.cs`. The current startup logic is explicit in `Program.cs`; a hosted service is cleaner and more observable. citeturn222792view0

### Benefit

After this split, transport errors, parsing bugs, AMS persistence bugs, and UI status bugs become isolated problems instead of all looking like “BambuLabService is broken.”

---

## 5.4 Split `CsvService`

`CsvService` currently both exports and imports, converts DTO shape, applies fallback pricing rules, creates filaments/spools, and post-processes reusable spool records. That is too much for one class. citeturn652173view5

### Target split

- `CsvExportService`
  - fetches export data
  - maps domain entities to CSV records
  - writes CSV
- `CsvImportService`
  - validates schema
  - parses records
  - groups/imports filaments and spools
  - applies import defaults
- `CsvSchema`
  - declares field names and compatibility rules
- `CsvImportResult`
  - number imported
  - warnings
  - duplicates/conflicts

### Important compatibility note

The exported headers and import grouping semantics should stay identical until after a dedicated migration/versioning design is introduced. The current `CsvFilamentRecord` contract is therefore part of the compatibility surface. citeturn652173view5

---

## 5.5 Split calculator responsibilities

The print calculator page currently exposes a complex UI with project details, materials, advanced settings, pricing logic, and PDF export. It should not also construct domain payloads, query inventory, and coordinate JS/PDF concerns directly. citeturn222792view4turn110929view0

### Target split

#### `PrintCostCalculatorService`

Pure calculation service:

- material cost
- labor cost
- machine cost
- depreciation
- electricity
- subtotal
- VAT
- margin presets
- final price
- batch table

#### `PrinterProfileService`

Owns built-in printer profile catalog and defaults.

#### `CalculatorPayloadBuilder`

Builds the UI-facing calculator state from inventory data and defaults.

#### `QuoteExportService`

Owns PDF generation orchestration.

### Why

This makes the calculator independently testable. The current page shape makes it likely that too much calculation setup is happening in Razor/component code. citeturn222792view4

---

## 5.6 Extract settings concerns

`AppSettings` is already a large and important aggregate containing thresholds, currency, BambuLab settings, AMS sync policy, timezone, and MQTT relay settings. Startup also mutates these from environment variables. Those concerns need dedicated services. citeturn652173view2turn222792view0

### Target classes

- `AppSettingsService`
  - load/save app settings
  - validate fields
  - expose current settings cache if needed
- `EnvironmentSettingsOverrideService`
  - apply env overrides safely
  - return a change report
- `ThresholdPolicy`
  - compute low/critical status from current thresholds
- `TimeZoneService`
  - timezone lookup and ETA conversion
- `CurrencyCatalog`
  - supported currency list and labels

### Key correction

Move stock-status evaluation out of `Filament.Status`, because entity-level hardcoded threshold values conflict with configurable thresholds in `AppSettings`. Replace `Filament.Status` with either:

- a computed UI DTO field produced by `ThresholdPolicy`, or
- a `GetStatus(StockThresholds thresholds)` method outside the entity.

This is one of the cleanest correctness improvements you can make while keeping the visible UI unchanged. citeturn652173view2turn652173view3turn954246view0

---

## 6. UI refactor plan

## 6.1 Principle

Each Razor page should become a **thin shell** that does four things only:

1. route declaration
2. dependency injection
3. bind to page state/view model
4. call handlers on user actions

Everything else should move into page state classes, mappers, or application services.

## 6.2 `InventoryPage.razor`

### Current visible concerns

The page renders total/low/critical counters and warning states, which implies it also retrieves and derives statistics. With the app’s feature set, it is very likely also handling filters, sorting, search, badges, and action orchestration. citeturn222792view3turn110929view0

### Split into

#### `InventoryPageState`

Properties:

- `InventoryFilter Filter`
- `IReadOnlyList<FilamentCardViewModel> Items`
- `InventoryStatsViewModel Stats`
- `bool IsLoading`
- `string? Error`
- `int LowStockCount`
- `int CriticalCount`

Methods:

- `LoadAsync()`
- `ApplyFilterAsync()`
- `SortAsync()`
- `RefreshAsync()`
- `DeleteFilamentAsync(id)`
- `RecordUsageAsync(spoolId, grams)`

#### Child components

- `InventoryStatsBar.razor`
- `InventoryFilterBar.razor`
- `InventoryGrid.razor`
- `FilamentCard.razor`
- `StockWarningBanner.razor`

### Outcome

The page becomes readable and future UI changes stop risking inventory business logic.

---

## 6.3 `PrintCalculatorPage.razor`

### Split into

#### `PrintCalculatorPageState`

Properties:

- project details model
- selected printer profile
- materials list
- advanced settings model
- calculated totals
- pricing preset
- batch table
- export state

Methods:

- `InitializeAsync()`
- `AddMaterial()`
- `RemoveMaterial()`
- `UseInventoryFilament()`
- `Recalculate()`
- `ExportPdfAsync()`

#### Child components

- `CalculatorProjectDetails.razor`
- `CalculatorMaterialsEditor.razor`
- `CalculatorAdvancedSettings.razor`
- `CalculatorTotalsPanel.razor`
- `BatchPricingTable.razor`

### JS interop

Any JS required for PDF export or client-side helpers should move behind a service such as `IQuoteExportJsInterop` so the page does not call raw JS except through one abstraction.

---

## 6.4 `AMSPage.razor`

Because the repo explicitly supports slot linking, no-UID warnings, and manual tray linking, the AMS page should also get its own state object. citeturn678208view0

Suggested components:

- `AmsPageState`
- `AmsSlotCard.razor`
- `AmsLinkDialog.razor`
- `AmsDiscrepancyBanner.razor`
- `PrinterStatusPanel.razon`

Small correction: the file should of course be `.razor`; the plan should implement `PrinterStatusPanel.razor`.

---

## 6.5 `SettingsPage.razor`

Suggested split:

- `SettingsPageState`
- `ThresholdSettingsSection.razor`
- `CurrencySettingsSection.razor`
- `TimezoneSettingsSection.razor`
- `BambuLabSettingsSection.razor`
- `MqttRelaySettingsSection.razor`
- `ThemeSettingsSection.razor`

This mirrors the existing settings categories in the README and `AppSettings` model. citeturn678208view0turn652173view2

---

## 7. Database and migration plan

## 7.1 Immediate rule: do not break existing `.db` files

The current startup code is acting as a compatibility migrator for older SQLite databases by:

- creating missing tables
- adding missing columns if absent
- backfilling defaults
- enabling foreign keys
- cleaning orphan reusable spool rows. citeturn222792view0

That means the app likely has real-world databases in multiple schema states. Your refactor plan must respect that.

## 7.2 Step-by-step migration modernization

### Phase A: preserve behavior, just relocate it

Move the existing startup migration logic into `DatabaseCompatibilityMigrator` **without changing the SQL behavior**.

Responsibilities:

- `EnsureCreatedAsync`
- `ColumnExistsAsync`
- `CreateMissingTablesAsync`
- `AddMissingColumnsAsync`
- `BackfillDefaultsAsync`
- `CleanupOrphansAsync`
- `EnableForeignKeysAsync`

Do not improve it yet. Relocate first.

### Phase B: add formal EF migrations for new changes only

After the compatibility layer is extracted and tested, start using EF migrations for **future** schema changes only.

### Phase C: optionally retire ad hoc column checks later

Only after you know all supported customer databases are at a safe baseline should you consider removing some of the ad hoc defensive SQL.

## 7.3 Repository layer

Introduce repositories only where they add clarity. Do not create generic repositories everywhere.

Recommended repositories:

- `IFilamentRepository`
- `ISpoolRepository`
- `ISettingsRepository`
- `IReusableSpoolRepository`
- `IBrandRepository`

These should expose scenario-friendly methods, for example:

```csharp
Task<Filament?> GetWithSpoolsAsync(int id, CancellationToken ct = default);
Task<IReadOnlyList<Filament>> SearchInventoryAsync(InventoryFilter filter, CancellationToken ct = default);
Task<Spool?> FindByAmsTagAsync(string tagUid, CancellationToken ct = default);
Task SaveChangesAsync(CancellationToken ct = default);
```

---

## 8. Detailed implementation phases

## Phase 0 — Baseline and safety net

### Objective

Freeze current behavior before changing internals.

### Tasks

1. Create a full route/workflow checklist:
   - Inventory list
   - Add filament
   - Edit filament/spool
   - Record usage
   - Reusable spool flow
   - CSV export/import
   - AMS linking/unlinking
   - BambuLab connect/disconnect
   - Settings save/load
   - Print calculator
   - PDF export
2. Capture baseline screenshots of all pages.
3. Create sample databases:
   - empty new DB
   - existing DB missing newer columns
   - DB with linked AMS spools
   - DB with reusable spools
4. Create sample CSV files:
   - exported current format
   - older format missing price column
5. Write minimal smoke tests.
6. Add structured logging around startup and AMS sync before the refactor so regressions are easier to localize.

### Exit criteria

- You can prove current behavior on demand.
- You have known-good sample data for regression testing.

---

## Phase 1 — Extract startup/bootstrap code from `Program.cs`

### Objective

Make startup testable without changing runtime behavior.

### Tasks

1. Create `Extensions/ServiceCollectionExtensions.cs`.
2. Move all `builder.Services.Add...` registrations into grouped extension methods.
3. Create `Infrastructure/Data/Startup/DatabasePathResolver.cs`.
4. Move Docker-aware DB path resolution there.
5. Create `Infrastructure/Data/Startup/DatabaseInitializer.cs`.
6. Move `EnsureCreatedAsync()` there.
7. Create `Infrastructure/Data/Startup/DatabaseCompatibilityMigrator.cs`.
8. Copy the existing SQL migration code into it with minimal edits.
9. Create `Infrastructure/Data/Startup/SeedDataService.cs`.
10. Move “ensure default settings exist” there.
11. Create `Application/Settings/StartupSettingsLoader.cs`.
12. Create `Application/Settings/EnvironmentSettingsOverrideService.cs`.
13. Create `Application/Settings/ThresholdBootstrapper.cs`.
14. Create `Infrastructure/Messaging/BambuLab/BambuLabStartupHostedService.cs` or a startup orchestrator.
15. Create `Infrastructure/Messaging/MqttRelay/MqttRelayStartupHostedService.cs` or equivalent.
16. Replace inline `Task.Run` startup blocks with hosted services or a startup orchestrator.

### Rules

- No behavioral changes.
- Same SQL.
- Same env var names.
- Same defaults.
- Same startup side effects.

### Exit criteria

- `Program.cs` is short.
- App boots identically.
- Tests/manual checklist still pass.

---

## Phase 2 — Separate settings and threshold logic

### Objective

Remove settings-related logic from unrelated classes.

### Tasks

1. Introduce `IAppSettingsService`.
2. Move all settings load/save logic to that service.
3. Introduce `ThresholdPolicy` and `StockThresholds`.
4. Replace hard-coded threshold comparisons in entities and pages.
5. Keep `ThresholdService` temporarily as a compatibility facade if pages depend on it.
6. Move timezone and currency helper logic into dedicated services or catalogs.
7. Ensure `InventoryPage` statistics use current settings-based thresholds, not entity hardcoded thresholds.

### Exit criteria

- No page or entity hardcodes threshold values.
- Settings writes and reads flow through one application service.

---

## Phase 3 — Break `FilamentService` apart

### Objective

Turn one large service into focused services without breaking callers.

### Tasks

1. Create `IInventoryService`, `ISpoolService`, `IReusableSpoolService`, `IBrandService`, `IInventoryStatisticsService`, `IAmsLinkService`, `IAmsMatchingService`.
2. Start by moving read-only query methods first.
3. Then move write methods one category at a time.
4. Keep `FilamentService` as a facade delegating to the new services.
5. Move AMS matching logic into `AmsMatchingService`.
6. Move explicit link/unlink and conflict-clearing logic into `AmsLinkService`.
7. Move statistics aggregation into `InventoryStatisticsService`.
8. Move reusable spool lifecycle logic into `ReusableSpoolService`.
9. Move brand logic into `BrandService`.
10. Update one page at a time to inject the new service it actually needs.

### Page migration order

1. `AddFilamentPage`
2. `SpoolsPage`
3. `InventoryPage`
4. `AMSPage`
5. `PrintCalculatorPage` if it still queries inventory through `FilamentService`

This order reduces blast radius.

### Exit criteria

- `FilamentService` is either gone or a thin facade.
- Each page depends on narrow interfaces.

---

## Phase 4 — Refactor Bambu/MQTT integration

### Objective

Separate transport, parsing, state, and persistence.

### Tasks

1. Define `IBambuLabClient` for raw MQTT interaction.
2. Move connect/disconnect/subscription code there.
3. Create `BambuMessageParser` with typed parse outputs.
4. Create `BambuStatusStore` for live status.
5. Create `AmsWeightSyncService` for DB updates.
6. Create `BambuEventDispatcher` to connect parser outputs to store and sync service.
7. Move UI events out of the transport class.
8. Convert startup connection logic to hosted-service orchestration.
9. Ensure the `AmsAutoUpdateWeight` and `AmsAutoUpdateOnlyDecrease` policy reads from current settings consistently.
10. Preserve the event hooks pages currently depend on by adapting them, then gradually replace them with better state subscriptions.

### Exit criteria

- Reconnect bugs do not threaten parsing logic.
- AMS weight-sync bugs do not threaten connection logic.
- UI status can be read from one place.

---

## Phase 5 — Thin the Razor pages

### Objective

Move page logic out of `.razor` files.

### Tasks

1. Introduce page state classes for `InventoryPage`, `PrintCalculatorPage`, `AMSPage`, and `SettingsPage`.
2. Extract child components for visual sections.
3. Create view model classes for page rendering.
4. Add mapping helpers from DTOs to view models.
5. Remove direct business-rule computation from Razor.
6. Keep routes and visible layout stable.

### Exit criteria

- Razor pages primarily contain markup and event bindings.
- Business logic lives in services/state classes.

---

## Phase 6 — Split CSV and PDF/export concerns

### Objective

Separate import/export responsibilities and make them independently testable.

### Tasks

1. Create `CsvExportService` and `CsvImportService`.
2. Extract `CsvFilamentRecord` to `CsvSchema.cs`.
3. Add header/schema validation with backward compatibility warnings.
4. Return structured import results.
5. Extract PDF quote generation to `QuoteExportService` and `QuotePdfService` if both orchestration and rendering exist.
6. Keep current exported column names and import semantics unchanged.

### Exit criteria

- CSV import/export can be tested without loading UI pages.
- PDF export can be tested separately from calculator math.

---

## Phase 7 — Cleanup and hardening

### Objective

Remove temporary compatibility wrappers and pay down leftover debt.

### Tasks

1. Delete obsolete façade methods from `FilamentService` once no callers remain.
2. Remove duplicated helpers.
3. Add XML docs to public interfaces.
4. Standardize cancellation token usage.
5. Standardize result/exception strategy.
6. Add analyzers/style rules if desired.
7. Review nullability and init-only semantics in entities/DTOs.
8. Consider replacing mutable event-heavy state with observable/state-container patterns where appropriate.

### Exit criteria

- No dead service wrappers.
- Clear ownership for each domain concern.

---

## 9. Tests to add before and during the refactor

## 9.1 Unit tests

### Inventory/domain rules

- `ThresholdPolicy` returns `Ok/Low/Critical` correctly for current settings.
- `CalculatedPricePerKg` equivalent logic still works after moving to a policy/service.
- `Spool.IsEmpty` edge cases.
- reusable spool state transitions.

### AMS

- tag-only auto-match behavior.
- tray-only does **not** auto-match.
- manual tray linking remains allowed.
- conflicting mappings are cleared on save.
- no-UID slot warning conditions.
- decrease-only weight sync policy.
- two-way weight sync policy when enabled.

### Calculator

- material cost calculations.
- VAT calculations.
- margin presets.
- batch table outputs.
- multi-material totals.

### CSV

- export headers exactly match current format.
- export writes one row per spool.
- import groups rows into filaments correctly.
- import handles missing price column.
- import creates reusable spool records for reusable spools.

## 9.2 Integration tests

- startup with empty DB.
- startup with older DB missing columns.
- save settings, restart, confirm settings persist.
- startup with env var overrides.
- AMS link then receive sync update.
- CSV round-trip export/import.

## 9.3 UI smoke tests

- inventory counters render correctly.
- filters/search return expected rows.
- calculator renders profiles and recalculates.
- settings save and reload.
- AMS page shows status and warnings.

---

## 10. Coding rules for the refactor

1. **No hidden behavior changes** in “cleanup” commits.
2. One concern per PR or commit series.
3. Keep old public methods as delegating wrappers during migration.
4. Prefer scenario-oriented methods over giant utility classes.
5. Avoid generic base abstractions unless duplication is proven.
6. Keep EF Core queries close to infrastructure/application boundaries.
7. Keep domain entities simple; move environment, IO, and policy concerns out.
8. Prefer immutable DTOs/records for UI and query results.
9. Use explicit names: `AmsMatchingService` is better than `HelperService`.
10. Log all startup compatibility migration actions.

---

## 11. Specific code transformations to make

## 11.1 Replace hardcoded filament stock status

### Current problem

`Filament.Status` uses hardcoded weights while thresholds are configurable in settings. citeturn652173view2turn652173view3

### Refactor

Replace:

```csharp
public string Status
{
    get
    {
        if (WeightRemaining < 250) return "critical";
        if (WeightRemaining < 500) return "low";
        return "ok";
    }
}
```

With something like:

```csharp
public enum StockLevel
{
    Ok,
    Low,
    Critical
}

public sealed class ThresholdPolicy
{
    public StockLevel Evaluate(decimal weightRemaining, StockThresholds thresholds)
    {
        if (weightRemaining < thresholds.Critical) return StockLevel.Critical;
        if (weightRemaining < thresholds.Low) return StockLevel.Low;
        return StockLevel.Ok;
    }
}
```

Then compute display status in a DTO/view model.

## 11.2 Move startup SQL out unchanged first

Take the ad hoc SQL migration block in `Program.cs` and place it into `DatabaseCompatibilityMigrator` with methods like:

```csharp
public sealed class DatabaseCompatibilityMigrator
{
    public async Task ApplyAsync(FilamentContext context, CancellationToken ct = default)
    {
        await CreateMissingTablesAsync(context, ct);
        await AddMissingColumnsAsync(context, ct);
        await BackfillDefaultsAsync(context, ct);
        await CleanupOrphansAsync(context, ct);
        await EnableForeignKeysAsync(context, ct);
    }
}
```

Do not “improve” the SQL during this step. Preserve behavior first. The current SQL is already proven against the project’s expected DB states. citeturn222792view0

## 11.3 Turn `FilamentService` into a facade during migration

```csharp
public sealed class FilamentService
{
    private readonly IInventoryService _inventory;
    private readonly ISpoolService _spools;
    private readonly IAmsMatchingService _amsMatching;
    private readonly IAmsLinkService _amsLink;
    private readonly IInventoryStatisticsService _stats;

    public FilamentService(
        IInventoryService inventory,
        ISpoolService spools,
        IAmsMatchingService amsMatching,
        IAmsLinkService amsLink,
        IInventoryStatisticsService stats)
    {
        _inventory = inventory;
        _spools = spools;
        _amsMatching = amsMatching;
        _amsLink = amsLink;
        _stats = stats;
    }

    public Task<Spool?> FindSpoolByAmsIdAsync(string? trayUuid, string? tagUid)
        => _amsMatching.FindSpoolByAmsIdAsync(trayUuid, tagUid);

    // other methods delegate one by one until callers are migrated
}
```

This gives you a low-risk migration path.

## 11.4 Create page-state classes instead of code-heavy Razor

```csharp
public sealed class InventoryPageState
{
    private readonly IInventoryService _inventoryService;
    private readonly IInventoryStatisticsService _statisticsService;
    private readonly IThresholdPolicyProvider _thresholds;

    public InventoryStatsViewModel Stats { get; private set; } = new();
    public IReadOnlyList<FilamentCardViewModel> Items { get; private set; } = [];
    public InventoryFilter Filter { get; private set; } = new();
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        Error = null;
        try
        {
            Items = await _inventoryService.GetInventoryAsync(Filter, ct);
            Stats = await _statisticsService.GetInventoryStatsAsync(ct);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

---

## 12. Dependency injection plan

## 12.1 New DI groups

```csharp
services.AddData(configuration);
services.AddSettingsServices();
services.AddInventoryServices();
services.AddAmsServices();
services.AddCalculatorServices();
services.AddCsvServices();
services.AddBambuLabServices();
services.AddMqttRelayServices();
services.AddUiState();
services.AddStartupServices();
```

## 12.2 Lifetime guidance

- `DbContextFactory`: scoped or registered as currently needed by EF setup.
- page state classes: scoped.
- calculation/stateless policy services: singleton or scoped depending on dependencies.
- repositories using `IDbContextFactory`: scoped.
- live status store for Bambu state: singleton.
- low-level MQTT client/connection manager: singleton.
- startup hosted services: hosted singleton.

The existing code already registers several services as scoped and singleton from `Program.cs`; the split should preserve equivalent lifetimes where behavior depends on them. citeturn222792view0

---

## 13. Rollback and risk control

## 13.1 Risks

1. Breaking older SQLite files.
2. Changing AMS auto-match behavior accidentally.
3. Changing calculator outputs by “cleanup.”
4. Breaking CSV compatibility.
5. Introducing Blazor state update regressions.
6. Breaking startup connection behavior for BambuLab or MQTT relay.

## 13.2 Controls

- Preserve startup SQL behavior before improving it.
- Snapshot calculator inputs/outputs in tests.
- Snapshot CSV headers and samples.
- Add AMS behavior tests before touching that logic.
- Keep facades until all callers are migrated.
- Ship in phases, not one giant branch if possible.

---

## 14. Recommended order of actual file creation

Create files in this exact order:

1. `Extensions/ServiceCollectionExtensions.cs`
2. `Extensions/ApplicationBuilderExtensions.cs`
3. `Infrastructure/Data/Startup/DatabasePathResolver.cs`
4. `Infrastructure/Data/Startup/DatabaseInitializer.cs`
5. `Infrastructure/Data/Startup/DatabaseCompatibilityMigrator.cs`
6. `Infrastructure/Data/Startup/SeedDataService.cs`
7. `Application/Settings/AppSettingsService.cs`
8. `Application/Settings/EnvironmentSettingsOverrideService.cs`
9. `Application/Settings/ThresholdPolicy.cs`
10. `Application/Settings/ThresholdBootstrapper.cs`
11. `Application/Inventory/InventoryService.cs`
12. `Application/Inventory/SpoolService.cs`
13. `Application/Inventory/ReusableSpoolService.cs`
14. `Application/Inventory/BrandService.cs`
15. `Application/Inventory/InventoryStatisticsService.cs`
16. `Application/Ams/AmsMatchingService.cs`
17. `Application/Ams/AmsLinkService.cs`
18. `Application/Ams/AmsWeightSyncService.cs`
19. `Infrastructure/Messaging/BambuLab/IBambuLabClient.cs`
20. `Infrastructure/Messaging/BambuLab/BambuLabClient.cs`
21. `Infrastructure/Messaging/BambuLab/BambuMessageParser.cs`
22. `Infrastructure/Messaging/BambuLab/BambuStatusStore.cs`
23. `Infrastructure/Messaging/BambuLab/BambuEventDispatcher.cs`
24. `Infrastructure/Messaging/BambuLab/BambuLabStartupHostedService.cs`
25. `Application/Calculator/PrintCostCalculatorService.cs`
26. `Application/Calculator/PrinterProfileService.cs`
27. `Application/Calculator/CalculatorPayloadBuilder.cs`
28. `Application/Calculator/QuoteExportService.cs`
29. `Infrastructure/Csv/CsvExportService.cs`
30. `Infrastructure/Csv/CsvImportService.cs`
31. `Infrastructure/Csv/CsvSchema.cs`
32. `UI/State/InventoryPageState.cs`
33. `UI/State/PrintCalculatorPageState.cs`
34. `UI/State/AmsPageState.cs`
35. `UI/State/SettingsPageState.cs`
36. child components under `UI/Components/...`
37. repository interfaces and implementations only where still needed
38. delete old facades and dead methods last

---

## 15. Definition of done

The refactor is done when all of the following are true:

- `Program.cs` is short and only composes the app.
- Startup/database compatibility logic is isolated and testable.
- `FilamentService` is gone or a tiny compatibility facade.
- Bambu/MQTT transport, parsing, state, and AMS sync are separated.
- Calculator math is in pure services.
- CSV import/export are split and testable.
- Razor pages are thin shells over page state and child components.
- Inventory thresholds come from settings everywhere.
- Existing DB files, CSV files, and user workflows still work.
- User-visible behavior is unchanged except for improved reliability/maintainability.

---

## 16. Practical implementation advice

If you want the safest path, do the work in this sequence:

1. extract startup
2. extract settings/threshold logic
3. split `FilamentService`
4. split Bambu/MQTT logic
5. thin pages
6. split CSV/PDF/export
7. cleanup

That sequence attacks the highest structural risk first while minimizing user-facing breakage. It fits the current repo, where startup, service concentration, and smart pages are the biggest complexity centers. citeturn222792view0turn954246view0turn188810view0

---

## 17. Final recommendation

Yes, this project is a strong candidate for a **deep simplification refactor that preserves the exact user experience**. The repo already has enough natural boundaries—inventory, spools, AMS, calculator, settings, CSV, MQTT relay, printer status—that the main work is not inventing architecture; it is **moving existing responsibilities into the right places without violating compatibility**. The biggest wins will come from shrinking `Program.cs`, dissolving `FilamentService`, splitting `BambuLabService`, and removing business logic from Razor pages. Those areas are directly suggested by the current repository structure, startup code, feature set, and model/service composition. citeturn678208view0turn954246view0turn222792view0turn222792view1turn222792view2turn222792view3turn222792view4
