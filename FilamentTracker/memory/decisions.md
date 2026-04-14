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
