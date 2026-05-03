# Key Decisions

## 2025 — Architecture

**Multi-printer MQTT support**
- Refactored `BambuLabService` from single-connection to a dictionary-keyed multi-connection model (`_mqttClients`, `_printerStatuses`, etc., all keyed by `int printerId`)
- Old single-connection API kept with `[Obsolete]` attributes for backward compatibility
- `OnStatusUpdated` event signature changed to `Action<int, PrintStatus?>` (includes printer ID)
- Rationale: future-proof for users with multiple BambuLab printers

**Null-safety on `OnStatusUpdated` invocations**
- All four call sites in `BambuLabService` now guard `OnStatusUpdated?.Invoke` behind a null-check on the status object
- `HandleStatusUpdate` in `Index.razor` also guards with early return on null status
- Rationale: `TryGetValue` can fail if a printer is removed mid-event (race condition during disconnect), previously resulting in a `NullReferenceException`

## 2025 — Data & Models

**SQLite via EF Core with `IDbContextFactory`**
- `FilamentContext` injected as factory to support scoped DB access from background MQTT threads (e.g., AMS auto-weight updates)
- Rationale: `DbContext` is not thread-safe; factory pattern avoids lifetime conflicts in Blazor Server

**AMS auto-weight sync**
- Only updates spools that have a confirmed RFID `TagUid` (not just `TrayUuid`) to avoid false matches
- Respects `AmsAutoUpdateOnlyDecrease` flag (default: true) — never adds weight back unless explicitly enabled
- Rationale: conservative default prevents accidental data corruption

## 2025 — UI / Design

**CSS variable system as single source of truth**
- All theme colors, spacing tokens, and shadow values defined as CSS custom properties in `site.css`
- Components use variables exclusively — no hardcoded hex for theme-sensitive values
- Two complete theme sets: `.dark` and `.light`, with radial gradient backgrounds on both

**Multi-theme system with 5 named themes**
- `ThemeService` refactored from `bool IsDarkMode` → `string ThemeName` (values: `dark`, `light`, `starbucks`, `harmony`, `spring`)
- `IsDarkMode` kept as a computed bool for backward compatibility
- Each theme is a full CSS variable block in `site.css` targeting the body class (e.g. `body.starbucks { --bg: ...; }`)
- Each theme also overrides accent colors on `.navbtn.active`, `.btn.primary`, `.spoolRow.selected`, `.tinyBtn.primary` so interactive elements match the theme mood
- SettingsPage replaced the 2-button dark/light toggle with a visual 5-card theme picker (`ThemeOption` record + `_themes` array)
- Rationale: richer personalisation without complexity in data model; still no DB persistence (design decision — acceptable reset on restart)

**Printer tab added to navigation; nav reorganised into explicit 3×3 grid**
- Nav grew from 8 to 9 buttons to accommodate the new Printer page
- Exact order: Row 1: Inventory / Add Filament / Spools; Row 2: Calculator / MQTT / Help; Row 3: Printer / AMS / Settings
- MQTT button icon changed from 🖨️ to 📡 (printer emoji moved to the new Printer tab)
- Rationale: logical grouping — inventory tools row 1, utility tools row 2, hardware/config row 3

**MqttPage vs PrinterPage separation of concerns**
- `MqttPage.razor` (📡 MQTT tab): printer CRUD (add/edit/delete/connect), AMS settings, MQTT relay config, raw message log
- `PrinterPage.razor` (🖨️ Printer tab): live status dashboard only — real-time per-printer cards with progress, temps, layers
- Rationale: management (config + log) and monitoring (live status) are distinct use cases; separating them keeps each page focused and avoids one mega-page

**Calculator uses its own isolated variable namespace (`--calc-*`)**
- Rationale: calculator predates the unified variable system and has a distinct dark visual identity; isolated to avoid conflicts

**Emoji as icon language**
- Consistent emoji set used in all section headers, nav buttons, and status indicators
- Rationale: zero dependency, universally rendered, immediately recognizable

**Heavy font weights throughout**
- 700 labels / 800 body UI / 900+ values and headings
- Rationale: improves readability on dark translucent panels; creates clear visual hierarchy

**`formCard` as the universal section container**
- Border-radius 22px, subtle translucent background, 1px border using `var(--border)`
- Used on Settings, Help, and all form pages for consistency

## 2026 — Refactor & Maintainability

**Shared live printer card component**
- Introduced `PrinterStatusCard.razor` and switched both `Index.razor` and `PrinterPage.razor` to use it
- Rationale: remove duplicated live status markup and guarantee consistent UI/state presentation

**Scoped CSS for page-local styles**
- Moved large inline style blocks from `Index.razor` and `AMSPage.razor` into `Index.razor.css` and `AMSPage.razor.css`
- Rationale: keep Razor files focused on markup/logic and avoid page-level style sprawl

**Startup responsibilities split out of `Program.cs`**
- Added `DatabaseBootstrapService` (schema/default bootstrap) and `AppSettingsBootstrapService` (settings/env/runtime init)
- Rationale: simplify app entrypoint and make startup behavior easier to reason about and test

**Scoped UI state services for complex pages**
- Added `InventoryPageState` (filters/search/sort state) and `AmsPageState` (slot-linking transient state)
- Rationale: reduce page code-behind clutter and prepare state logic for testability

**Debounced live status fan-out**
- `PrinterStatusStore` now coalesces rapid MQTT updates into ~200ms notification windows
- Rationale: reduce unnecessary Blazor rerender churn under high-frequency printer telemetry

**Scoped CSS bundle conflict fix**
- Removed stale committed `wwwroot/FilamentTracker.styles.css` once scoped CSS files were introduced
- Rationale: avoid Static Web Assets manifest collision (`Sequence contains more than one element`)

