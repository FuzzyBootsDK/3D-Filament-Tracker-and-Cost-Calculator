# 3D Filament Tracker Refactor Checklist

This checklist turns the refactor plan into an implementation sequence you can execute commit by commit while preserving current user-visible behavior.

---

## Ground rules

Before starting any code changes, lock in these rules:

- Do not change routes, visible page layout, labels, workflows, calculations, CSV shape, AMS/manual-link behavior, or database compatibility unless a task explicitly says otherwise.
- Keep the application runnable after every checkpoint.
- Prefer extraction and delegation over rewrite.
- Add tests before deleting old code.
- Treat `Program.cs`, `FilamentService.cs`, `BambuLabService.cs`, `InventoryPage.razor`, and `PrintCalculatorPage.razor` as the primary complexity hotspots.

Definition of success for the whole checklist:

- Same user-facing behavior.
- Smaller classes with single clear responsibilities.
- `Program.cs` reduced to composition and startup orchestration.
- Inventory, AMS, calculator, settings, CSV, and startup concerns separated.
- Page components mostly UI-only.
- Core behaviors covered by repeatable tests.

---

# Phase 0 - Baseline, safety net, and inventory

## 0.1 Create a baseline branch

- Create a dedicated refactor branch.
- Tag the current state so you can compare behavior at any time.
- Record the commit hash in a working notes file.

Checklist:

- [ ] Create `refactor/simplification` branch.
- [ ] Tag current working state, e.g. `pre-refactor-baseline`.
- [ ] Create `docs/refactor-notes.md` for progress and decisions.

## 0.2 Capture current behavior

Create a manual regression checklist before code changes.

Checklist:

- [ ] Launch the app locally against a copy of a real SQLite database.
- [ ] Record screenshots or screen captures of:
  - [ ] Inventory page
  - [ ] AMS page
  - [ ] Add/Edit filament flows
  - [ ] Spools page
  - [ ] Settings page
  - [ ] Print calculator page
- [ ] Export a CSV sample and save it as a fixture.
- [ ] Save at least one generated PDF quote/output as a fixture.
- [ ] Record startup behavior with:
  - [ ] empty database
  - [ ] existing database
  - [ ] environment variables enabled
  - [ ] Bambu disabled
  - [ ] MQTT relay disabled
  - [ ] Bambu enabled but unavailable
- [ ] Write down current AMS linking behavior for:
  - [ ] RFID/tag-based match
  - [ ] manual tray linking
  - [ ] weight decrease sync
  - [ ] discrepancy display

Recommended fixture folder:

```text
/tests/Fixtures/
  Baseline/
    sample-export.csv
    sample-quote.pdf
    screenshots/
    existing-database-copy.db
```

## 0.3 Add a behavior matrix

Create one document that defines what must remain unchanged.

Checklist:

- [ ] Add `docs/behavior-contract.md`.
- [ ] Document each preserved behavior:
  - [ ] inventory totals
  - [ ] threshold status rules
  - [ ] spool percent remaining
  - [ ] price fallback behavior
  - [ ] CSV import/export rules
  - [ ] AMS auto-match rules
  - [ ] manual AMS link persistence
  - [ ] calculator totals
  - [ ] startup env var overrides

## 0.4 Add baseline tests before refactor

Even a small test suite here will save you later.

Checklist:

- [ ] Add a test project if missing.
- [ ] Add test support for SQLite temp database setup.
- [ ] Add test support for seeded settings.
- [ ] Add snapshot/fixture support for CSV and calculator inputs.

Minimum tests to add now:

- [ ] `Filament_Status_Uses_CurrentBehavior()`
- [ ] `Spool_PercentRemaining_MatchesCurrentBehavior()`
- [ ] `Csv_Export_ProducesExpectedColumns()`
- [ ] `Csv_Import_RecreatesFilamentsAndSpools()`
- [ ] `Ams_ManualLink_Persists()`
- [ ] `Calculator_TotalCost_MatchesFixture()`
- [ ] `Startup_EnvOverrides_AreApplied()`

Commit checkpoint:

- [ ] Commit baseline tests and fixtures.

---

# Phase 1 - Introduce structure without changing behavior

## 1.1 Create target service folders

Do not move major files yet. Create structure first.

Checklist:

- [ ] Create folders:

```text
Services/
  Startup/
  Inventory/
  AMS/
  Calculator/
  Settings/
  Csv/
  Mqtt/
UI/
  State/
  Mapping/
Domain/
  Rules/
Infrastructure/
  Data/
```

- [ ] Add README files or placeholder files in each folder explaining intended responsibility.

## 1.2 Introduce application-facing interfaces

Start by defining contracts, not implementations.

Checklist:

- [ ] Add interfaces:
  - [ ] `IInventoryService`
  - [ ] `ISpoolService`
  - [ ] `IBrandService`
  - [ ] `IReusableSpoolService`
  - [ ] `IInventoryStatisticsService`
  - [ ] `IAmsLinkService`
  - [ ] `IAmsMatchingService`
  - [ ] `IAmsWeightSyncService`
  - [ ] `IPrintCostCalculatorService`
  - [ ] `IPrinterProfileService`
  - [ ] `IQuoteExportService`
  - [ ] `IAppSettingsService`
  - [ ] `IEnvironmentSettingsOverrideService`
  - [ ] `ICsvImportService`
  - [ ] `ICsvExportService`

- [ ] Keep existing services registered and active.
- [ ] Do not remove any existing code yet.

## 1.3 Add composition extension methods

Prepare for a smaller `Program.cs`.

Checklist:

- [ ] Add `ServiceCollectionExtensions` with methods:
  - [ ] `AddDataServices(...)`
  - [ ] `AddInventoryServices()`
  - [ ] `AddAmsServices()`
  - [ ] `AddCalculatorServices()`
  - [ ] `AddSettingsServices()`
  - [ ] `AddCsvServices()`
  - [ ] `AddStartupServices()`
  - [ ] `AddMqttServices()`

- [ ] Initially have these extension methods register the current existing services.
- [ ] Verify application starts with no behavior change.

Commit checkpoint:

- [ ] Commit folder structure, interfaces, and DI extension methods.

---

# Phase 2 - Reduce Program.cs first

## 2.1 Extract database path resolution

Checklist:

- [ ] Create `Services/Startup/DatabasePathResolver.cs`.
- [ ] Move connection-string/path resolution logic from `Program.cs` into it.
- [ ] Add tests for:
  - [ ] default local path
  - [ ] Docker/container path
  - [ ] configured override path

Suggested shape:

```csharp
public interface IDatabasePathResolver
{
    string ResolveConnectionString();
}
```

## 2.2 Extract database initialization

Checklist:

- [ ] Create `DatabaseInitializer`.
- [ ] Move `EnsureCreated` logic out of `Program.cs`.
- [ ] Keep behavior identical.
- [ ] Add logging around initialization start/end.
- [ ] Add integration test for brand-new database startup.

## 2.3 Extract compatibility migration logic

This is critical because it preserves old SQLite files.

Checklist:

- [ ] Create `DatabaseCompatibilityMigrator`.
- [ ] Move all defensive schema checks and `ALTER TABLE` logic into it.
- [ ] Preserve exact sequence of operations.
- [ ] Preserve idempotency.
- [ ] Add tests using fixture databases for:
  - [ ] older schema
  - [ ] partial schema
  - [ ] missing columns
  - [ ] orphan reusable spools cleanup

Recommended class split:

```csharp
DatabaseCompatibilityMigrator
  - ApplyAsync()
  - EnsureTablesExistAsync()
  - EnsureColumnsExistAsync()
  - BackfillDefaultsAsync()
  - CleanupOrphansAsync()
```

## 2.4 Extract settings bootstrap and env var override logic

Checklist:

- [ ] Create `StartupSettingsLoader`.
- [ ] Create `EnvironmentSettingsOverrideService`.
- [ ] Move all startup settings read/write logic out of `Program.cs`.
- [ ] Preserve precedence order exactly:
  - [ ] database settings
  - [ ] environment variable overrides
  - [ ] fallback defaults
- [ ] Add tests for each override scenario.

## 2.5 Extract threshold bootstrap

Checklist:

- [ ] Create `ThresholdBootstrapper`.
- [ ] Move threshold initialization from `Program.cs`.
- [ ] Add test that threshold service receives expected values from settings.

## 2.6 Extract startup connection orchestration

Checklist:

- [ ] Create `BambuLabStartupService`.
- [ ] Create `MqttRelayStartupService`.
- [ ] Move startup connection attempts out of `Program.cs`.
- [ ] Preserve startup error handling and non-fatal behavior.
- [ ] Add logs for attempted start, success, failure, retry policy if any.

## 2.7 Shrink Program.cs

Checklist:

- [ ] Replace inline startup logic with a single `InitializeApplicationAsync()` extension.
- [ ] Keep HTTP pipeline config readable and short.
- [ ] Verify startup still works in all baseline scenarios.

Target outcome:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddDataServices(builder.Configuration)
    .AddInventoryServices()
    .AddAmsServices()
    .AddCalculatorServices()
    .AddSettingsServices()
    .AddCsvServices()
    .AddMqttServices()
    .AddStartupServices();

var app = builder.Build();
await app.Services.InitializeApplicationAsync();
app.ConfigureHttpPipeline();
app.Run();
```

Commit checkpoint:

- [ ] Commit `Program.cs` extraction only after startup parity is verified.

---

# Phase 3 - Break up FilamentService

This is the biggest simplification opportunity.

## 3.1 Inventory current FilamentService responsibilities

Before moving anything, make a method matrix.

Checklist:

- [ ] List every public method in `FilamentService`.
- [ ] Classify each into one category:
  - [ ] filament CRUD
  - [ ] spool CRUD
  - [ ] reusable spool handling
  - [ ] brand operations
  - [ ] inventory statistics
  - [ ] AMS lookup/link logic
  - [ ] settings-related behavior
  - [ ] import/export helper behavior
  - [ ] threshold/status behavior

- [ ] Save this list to `docs/filament-service-split-map.md`.

## 3.2 Extract inventory read/write logic

Checklist:

- [ ] Create `InventoryService`.
- [ ] Move filament-focused operations first:
  - [ ] get all filaments
  - [ ] get filament by id
  - [ ] add filament
  - [ ] update filament
  - [ ] delete filament
  - [ ] inventory search/filter data retrieval if service-owned
- [ ] Keep old `FilamentService` methods delegating to the new class initially.

Suggested responsibility:

```csharp
public class InventoryService : IInventoryService
{
    // Filament entity lifecycle and aggregate loading
}
```

## 3.3 Extract spool operations

Checklist:

- [ ] Create `SpoolService`.
- [ ] Move spool-specific methods:
  - [ ] add spool
  - [ ] update spool
  - [ ] remove spool
  - [ ] adjust weight
  - [ ] consume usage
  - [ ] calculate spool-specific derived outputs if not domain-owned
- [ ] Add focused tests on spool updates and weight boundaries.

## 3.4 Extract reusable spool lifecycle

Checklist:

- [ ] Create `ReusableSpoolService`.
- [ ] Move reusable spool creation, assignment, detachment, deletion rules.
- [ ] Add rules for orphan handling and safe deletion.
- [ ] Add tests for reusable spool attach/detach flows.

## 3.5 Extract brand management

Checklist:

- [ ] Create `BrandService`.
- [ ] Move brand CRUD and lookup logic.
- [ ] Add tests for duplicates, casing behavior, and sorting.

## 3.6 Extract inventory statistics

Checklist:

- [ ] Create `InventoryStatisticsService`.
- [ ] Move aggregate/dashboard calculations:
  - [ ] total spool count
  - [ ] low stock count
  - [ ] critical stock count
  - [ ] unique filament count if present
  - [ ] total estimated value if present
- [ ] Add deterministic tests using seeded fixture data.

## 3.7 Extract AMS lookup helpers from FilamentService

Checklist:

- [ ] Move AMS-specific methods such as spool lookup/match into `AmsLinkService` or `AmsMatchingService`.
- [ ] Keep temporary delegation from `FilamentService` until callers are switched.
- [ ] Add tests for:
  - [ ] tag match precedence
  - [ ] manual tray fallback only when explicitly linked
  - [ ] no accidental cross-match

## 3.8 Turn FilamentService into a compatibility facade

Do not delete it immediately.

Checklist:

- [ ] Reduce `FilamentService` to thin delegation methods only.
- [ ] Mark with comment: temporary compatibility facade.
- [ ] Update DI so pages/services can gradually use the new service interfaces directly.
- [ ] Remove dead code only after all callers are migrated.

Commit checkpoint:

- [ ] Commit after each sub-extraction or at least after inventory/spool split and tests.

---

# Phase 4 - Fix domain logic placement

## 4.1 Move threshold evaluation out of entities

The current entity-level threshold behavior is a design smell if thresholds are configurable.

Checklist:

- [ ] Create `Domain/Rules/StockStatusEvaluator.cs`.
- [ ] Move low/critical threshold decisions there.
- [ ] Pass in thresholds via a value object or settings snapshot.
- [ ] Keep entity convenience properties if needed, but make them call the evaluator or be view-model-only.
- [ ] Add tests for boundary conditions.

Suggested shape:

```csharp
public sealed class StockThresholds
{
    public decimal LowGrams { get; }
    public decimal CriticalGrams { get; }
}

public interface IStockStatusEvaluator
{
    FilamentStatus Evaluate(decimal totalRemainingWeight, StockThresholds thresholds);
}
```

## 4.2 Move pricing fallback rules into a policy

Checklist:

- [ ] Create `PriceCalculationPolicy`.
- [ ] Move fallback logic for spool price vs filament price vs unit cost into one place.
- [ ] Add tests for all price source combinations.

## 4.3 Review derived properties in entities

Checklist:

- [ ] Keep pure mathematical derived properties in entities if they are truly invariant.
- [ ] Move settings-dependent or UI-dependent derived logic out.
- [ ] Review each entity property and classify as:
  - [ ] safe domain invariant
  - [ ] settings-dependent
  - [ ] UI summary only

- [ ] Document this in `docs/domain-property-review.md`.

Commit checkpoint:

- [ ] Commit domain rule extraction after tests pass.

---

# Phase 5 - Split BambuLabService and AMS logic

## 5.1 Create a Bambu responsibility map

Checklist:

- [ ] List all fields, events, timers, and public methods in `BambuLabService`.
- [ ] Classify them into:
  - [ ] transport/connection
  - [ ] message parsing
  - [ ] printer state storage
  - [ ] event broadcasting
  - [ ] AMS weight sync
  - [ ] DB persistence
  - [ ] startup orchestration
- [ ] Save to `docs/bambulab-service-split-map.md`.

## 5.2 Extract transport/client layer

Checklist:

- [ ] Create `BambuLabClient`.
- [ ] Move raw MQTT/topic connect/disconnect/publish/subscribe logic there.
- [ ] Keep it transport-only.
- [ ] No business decisions in this class.

Suggested responsibility:

```csharp
public interface IBambuLabClient
{
    Task ConnectAsync(...);
    Task DisconnectAsync();
    event Func<string, Task> MessageReceived;
}
```

## 5.3 Extract message parsing

Checklist:

- [ ] Create `BambuMessageParser`.
- [ ] Move payload parsing and normalization there.
- [ ] Parser should output typed DTOs/state updates.
- [ ] Add fixture-based parsing tests using saved real payloads.

Recommended fixture folder:

```text
/tests/Fixtures/Bambu/
  print-status-running.json
  print-status-idle.json
  ams-update.json
  malformed-message.json
```

## 5.4 Extract runtime state store

Checklist:

- [ ] Create `BambuStatusStore`.
- [ ] Move state flags and latest printer/AMS status snapshots there.
- [ ] Expose read-only accessors or immutable snapshots.
- [ ] Keep synchronization/thread-safety explicit.

## 5.5 Extract event dispatching

Checklist:

- [ ] Create `BambuEventDispatcher`.
- [ ] Move event fan-out logic there.
- [ ] Ensure all event handlers are resilient to exceptions.
- [ ] Add tests for multiple subscribers and failure isolation.

## 5.6 Extract AMS matching and weight sync

Checklist:

- [ ] Create `AmsMatchingService`.
- [ ] Move spool identification logic there.
- [ ] Create `AmsWeightSyncService`.
- [ ] Move “only decrease remaining weight” rules there.
- [ ] Add tests for:
  - [ ] tag-based matching
  - [ ] manual tray link match
  - [ ] no-match scenarios
  - [ ] lower weight accepted
  - [ ] higher weight ignored if current behavior requires that

## 5.7 Extract persistence updates

Checklist:

- [ ] Move database updates triggered by Bambu events out of `BambuLabService` into a dedicated synchronizer class, e.g. `AmsPersistenceSyncService`.
- [ ] Ensure transport layer no longer touches EF Core directly.
- [ ] Add integration tests with seeded DB and message fixtures.

## 5.8 Leave BambuLabService as orchestration or facade

Checklist:

- [ ] Reduce `BambuLabService` to coordinating the client, parser, store, and sync services.
- [ ] Preserve public surface for callers initially.
- [ ] Gradually switch callers to more specific interfaces.

Commit checkpoint:

- [ ] Commit after transport/parser/store split.
- [ ] Commit again after AMS sync extraction.

---

# Phase 6 - Split CSV and PDF/export logic cleanly

## 6.1 Split current CsvService

Checklist:

- [ ] Create `CsvExportService`.
- [ ] Move export-only behavior there.
- [ ] Create `CsvImportService`.
- [ ] Move import/grouping logic there.
- [ ] Keep schema mapping in `CsvSchema` or `CsvRecordMap`.
- [ ] Add explicit versioning comments to schema mapping.

Tests to add:

- [ ] export headers stable
- [ ] export one record per spool
- [ ] import groups rows into filaments/spools correctly
- [ ] import handles optional columns/defaults the same way as before
- [ ] export-import roundtrip preserves expected values

## 6.2 Separate PDF export from calculator orchestration

Checklist:

- [ ] Create `QuoteExportService` or `QuotePdfService`.
- [ ] Keep PDF rendering and file creation isolated from calculator logic.
- [ ] Add fixture-based smoke tests if possible.
- [ ] Preserve file naming and visible output shape if user-facing.

Commit checkpoint:

- [ ] Commit CSV/PDF split after roundtrip tests pass.

---

# Phase 7 - Refactor calculator internals without changing UI

## 7.1 Inventory calculator responsibilities

Checklist:

- [ ] List everything currently handled by `PrintCalculatorPage.razor` and any linked services:
  - [ ] loading printer profiles
  - [ ] loading filament inventory
  - [ ] building dropdown data
  - [ ] preparing payload for JS
  - [ ] cost computation
  - [ ] batch optimization
  - [ ] preset handling
  - [ ] PDF export trigger
- [ ] Save to `docs/print-calculator-split-map.md`.

## 7.2 Create calculator services

Checklist:

- [ ] Create `PrintCostCalculatorService`.
- [ ] Move pure cost math there.
- [ ] Create `PrinterProfileService`.
- [ ] Move printer profile retrieval/default logic there.
- [ ] Create `CalculatorPayloadBuilder`.
- [ ] Move inventory-to-UI/JS payload mapping there.
- [ ] Add tests for each service independently.

## 7.3 Introduce page state object

Checklist:

- [ ] Create `UI/State/PrintCalculatorPageState.cs`.
- [ ] Move mutable page state and orchestration there.
- [ ] Keep the Razor page mostly to:
  - [ ] inject state/service
  - [ ] bind controls
  - [ ] call event handlers
  - [ ] render output

Suggested page-state responsibilities:

```csharp
public class PrintCalculatorPageState
{
    public Task InitializeAsync();
    public Task RecalculateAsync();
    public Task ExportPdfAsync();
    public Task LoadProfilesAsync();
}
```

## 7.4 Keep JS interop thin

Checklist:

- [ ] Move JS interop calls behind a dedicated adapter if there is more than a couple of calls.
- [ ] Avoid mixing serialization, business rules, and JS invocation in the page.
- [ ] Add testable boundaries wherever possible.

Commit checkpoint:

- [ ] Commit calculator service extraction and page thinning separately.

---

# Phase 8 - Refactor InventoryPage and other smart pages

## 8.1 Introduce inventory page state

Checklist:

- [ ] Create `UI/State/InventoryPageState.cs`.
- [ ] Move non-render logic out of `InventoryPage.razor`:
  - [ ] initial loading
  - [ ] refresh orchestration
  - [ ] filtering
  - [ ] sorting
  - [ ] dashboard count calculation if page-owned
  - [ ] AMS discrepancy loading/mapping
  - [ ] event subscription/unsubscription
- [ ] Keep the Razor page focused on display and user interactions.

## 8.2 Extract view-model mapping

Checklist:

- [ ] Create `UI/Mapping/ViewModelMappers.cs` or page-specific mappers.
- [ ] Map domain/application objects to UI row models.
- [ ] Stop using entities directly in the UI if that is currently happening.
- [ ] Create explicit row/view models where useful:
  - [ ] `InventoryRowViewModel`
  - [ ] `AmsSlotViewModel`
  - [ ] `CalculatorMaterialViewModel`

## 8.3 Repeat for AMS and Settings pages if needed

Checklist:

- [ ] Create `AmsPageState` if AMS page contains substantial orchestration.
- [ ] Create `SettingsPageState` if settings page contains substantial save/load/validation logic.
- [ ] Move save pipelines, validation, and connection test actions into state/services.

## 8.4 Create smaller reusable components

Checklist:

- [ ] Split giant Razor pages into subcomponents when there are clearly repeated or isolated sections.

Examples:

- [ ] `InventoryDashboardCards.razor`
- [ ] `InventoryFilterBar.razor`
- [ ] `InventoryTable.razor`
- [ ] `AmsDiscrepancyPanel.razor`
- [ ] `CalculatorCostSummary.razor`
- [ ] `CalculatorMaterialTable.razor`
- [ ] `SettingsBambuSection.razor`
- [ ] `SettingsThresholdSection.razor`

Commit checkpoint:

- [ ] Commit page-state extraction per page, not all at once.

---

# Phase 9 - Add repository/query seams where useful

Only do this if the service layer still has too much EF-specific query code.

## 9.1 Decide repository scope pragmatically

Checklist:

- [ ] Review service classes for duplicated EF Core include/query logic.
- [ ] If duplication is high, add repositories or query services.
- [ ] If duplication is low, avoid over-engineering.

Suggested repositories if needed:

- [ ] `FilamentRepository`
- [ ] `SpoolRepository`
- [ ] `BrandRepository`
- [ ] `SettingsRepository`

Good candidates for repository extraction:

- complex aggregate loading
- repeated include trees
- AMS-specific lookup queries
- brand lookup and normalization
- settings read/update access

## 9.2 Add query DTOs for read models

Checklist:

- [ ] Create DTOs for complex page reads instead of loading entities everywhere.
- [ ] Add specialized queries for inventory dashboard and AMS snapshot views.
- [ ] Keep write-side methods on services.

Commit checkpoint:

- [ ] Commit repository/query seams only if they reduce real duplication.

---

# Phase 10 - Migrate callers to new services

## 10.1 Replace direct FilamentService injections gradually

Checklist:

- [ ] Search for all `FilamentService` injections/usages.
- [ ] Replace page-by-page or service-by-service with narrower interfaces.

Suggested migration order:

1. calculator-related callers
2. settings/brand callers
3. inventory page callers
4. AMS page callers
5. background services

- [ ] Keep `FilamentService` facade until no direct callers remain.

## 10.2 Replace monolithic BambuLabService usages where possible

Checklist:

- [ ] Search for all `BambuLabService` injections/usages.
- [ ] Replace read-only consumers with `IBambuStatusStore` or equivalent.
- [ ] Replace AMS-linking consumers with `IAmsLinkService` or `IAmsMatchingService`.
- [ ] Keep the old facade/orchestrator only where necessary.

Commit checkpoint:

- [ ] Commit caller migrations in small batches.

---

# Phase 11 - Remove dead code and finalize structure

## 11.1 Delete compatibility facades only when unused

Checklist:

- [ ] Confirm no production callers remain for old monolithic service methods.
- [ ] Delete dead methods from `FilamentService`.
- [ ] Delete dead methods from `BambuLabService`.
- [ ] Remove unused DI registrations.
- [ ] Remove stale comments and TODOs.

## 11.2 Normalize namespaces and folder alignment

Checklist:

- [ ] Align namespaces with new folder structure.
- [ ] Ensure file names match primary class names.
- [ ] Remove duplicate helper classes created during transition.

## 11.3 Add architecture overview documentation

Checklist:

- [ ] Add `docs/architecture-overview.md`.
- [ ] Include:
  - [ ] layer responsibilities
  - [ ] startup sequence
  - [ ] AMS flow
  - [ ] inventory flow
  - [ ] calculator flow
  - [ ] CSV import/export flow

Commit checkpoint:

- [ ] Commit cleanup and docs as final structural pass.

---

# Phase 12 - Final testing pass and hardening

## 12.1 Full regression run

Checklist:

- [ ] Run all automated tests.
- [ ] Re-run manual regression checklist against the refactored branch.
- [ ] Compare screenshots with baseline.
- [ ] Verify exported CSV matches header/schema expectations.
- [ ] Verify generated PDF still matches expected visible content.
- [ ] Verify old database upgrade path still works.
- [ ] Verify environment overrides still work in container startup.
- [ ] Verify Bambu disabled/unavailable startup remains non-fatal.

## 12.2 Performance/safety checks

Checklist:

- [ ] Check inventory page load time did not regress.
- [ ] Check calculator page initialization did not regress.
- [ ] Check Bambu reconnect behavior did not regress.
- [ ] Check no extra DB queries were introduced in tight loops.
- [ ] Check event subscriptions are disposed correctly.
- [ ] Check background services stop cleanly.

## 12.3 Logging and observability

Checklist:

- [ ] Ensure startup stages are logged clearly.
- [ ] Ensure Bambu connection lifecycle is logged clearly.
- [ ] Ensure compatibility migration steps are logged with enough detail for diagnosis.
- [ ] Ensure import/export failures report actionable messages.

Commit checkpoint:

- [ ] Commit final hardening changes.

---

# Recommended commit sequence

Use this commit order to keep changes reviewable and reversible:

1. baseline fixtures and tests
2. folder structure and interfaces
3. DI extension methods
4. `Program.cs` path resolver extraction
5. database initializer extraction
6. compatibility migrator extraction
7. settings/env override extraction
8. startup orchestration extraction
9. inventory service extraction
10. spool service extraction
11. reusable spool service extraction
12. brand/statistics extraction
13. threshold/pricing rule extraction
14. Bambu transport/parser/store extraction
15. AMS matching/weight sync extraction
16. CSV import/export split
17. calculator service extraction
18. calculator page-state extraction
19. inventory page-state extraction
20. AMS/settings page-state extraction
21. caller migration off facades
22. dead code removal
23. architecture docs and final cleanup
24. regression/hardening pass

---

# Detailed class creation checklist

Create these classes or their nearest equivalents.

## Startup

- [ ] `DatabasePathResolver`
- [ ] `DatabaseInitializer`
- [ ] `DatabaseCompatibilityMigrator`
- [ ] `StartupSettingsLoader`
- [ ] `EnvironmentSettingsOverrideService`
- [ ] `ThresholdBootstrapper`
- [ ] `BambuLabStartupService`
- [ ] `MqttRelayStartupService`
- [ ] `ApplicationInitializationExtensions`
- [ ] `ApplicationBuilderExtensions`

## Inventory

- [ ] `InventoryService`
- [ ] `SpoolService`
- [ ] `ReusableSpoolService`
- [ ] `BrandService`
- [ ] `InventoryStatisticsService`
- [ ] compatibility facade version of `FilamentService`

## Domain rules

- [ ] `StockStatusEvaluator`
- [ ] `PriceCalculationPolicy`
- [ ] `ReusableSpoolRules`
- [ ] `StockThresholds`

## AMS/Bambu/MQTT

- [ ] `BambuLabClient`
- [ ] `BambuMessageParser`
- [ ] `BambuStatusStore`
- [ ] `BambuEventDispatcher`
- [ ] `AmsMatchingService`
- [ ] `AmsLinkService`
- [ ] `AmsWeightSyncService`
- [ ] `AmsPersistenceSyncService`
- [ ] orchestration/facade version of `BambuLabService`

## Calculator and export

- [ ] `PrintCostCalculatorService`
- [ ] `PrinterProfileService`
- [ ] `CalculatorPayloadBuilder`
- [ ] `QuoteExportService` or `QuotePdfService`

## CSV

- [ ] `CsvImportService`
- [ ] `CsvExportService`
- [ ] `CsvSchema` or `CsvRecordMap`

## UI state and mapping

- [ ] `InventoryPageState`
- [ ] `PrintCalculatorPageState`
- [ ] `AmsPageState`
- [ ] `SettingsPageState`
- [ ] `ViewModelMappers`
- [ ] `InventoryRowViewModel`
- [ ] `AmsSlotViewModel`
- [ ] calculator row/view models as needed

---

# Exit criteria per phase

Use these explicit “done” checks before moving on.

## Program.cs extraction done when:

- [ ] `Program.cs` only composes services and runs initialization.
- [ ] All startup behaviors still match baseline.
- [ ] Compatibility migration still works on old DB files.

## FilamentService split done when:

- [ ] inventory, spool, brand, reusable spool, statistics, and AMS methods have dedicated homes.
- [ ] `FilamentService` is only a thin facade or is fully removed.
- [ ] all associated tests pass.

## Bambu split done when:

- [ ] transport, parser, store, sync, and eventing are separate.
- [ ] no raw MQTT transport code remains mixed with DB write logic.
- [ ] AMS sync rules are test-covered.

## Page refactor done when:

- [ ] page components are mostly UI markup and event binding.
- [ ] orchestration/state lives in page-state classes and services.
- [ ] no giant mixed-concern code blocks remain in pages.

## Final refactor done when:

- [ ] old compatibility facades are gone or intentionally retained as thin wrappers.
- [ ] all baseline scenarios pass.
- [ ] architecture docs are updated.
- [ ] code is meaningfully easier to navigate and extend.

---

# Risk watchlist

These are the areas most likely to break during refactor. Check them after every relevant change.

- [ ] startup migration ordering
- [ ] env var precedence ordering
- [ ] AMS tag vs tray matching behavior
- [ ] “only decrease weight” synchronization policy
- [ ] page event subscriptions and `StateHasChanged` behavior
- [ ] calculator JS initialization timing
- [ ] CSV schema compatibility
- [ ] PDF export formatting assumptions
- [ ] reusable spool orphan cleanup
- [ ] threshold behavior when settings are missing/defaulted

---

# Optional enhancements after parity is achieved

Do not do these during the parity refactor unless they are free.

- [ ] replace ad hoc schema migration with formal EF Core migrations plus compatibility bootstrap for legacy DBs
- [ ] add MediatR or command/query separation if the app keeps growing
- [ ] add richer diagnostics/health endpoints for Bambu and MQTT status
- [ ] add snapshot tests for rendered UI sections
- [ ] add structured domain events for AMS and inventory updates

These are second-wave improvements, not first-wave refactor tasks.

---

# Final note

The safest way to execute this refactor is:

- extract first
- delegate second
- migrate callers third
- delete last

At no point should a single commit attempt to simplify startup, inventory, AMS, calculator, and UI pages all together. Small vertical slices with tests after each slice will keep the application stable and make regressions easy to isolate.
