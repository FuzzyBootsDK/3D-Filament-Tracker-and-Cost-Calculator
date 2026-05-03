# Suggestions

## UI / UX

- ~~**Multi-printer live widget**~~: **Addressed** — both `PrinterPage.razor` and `Index.razor` now render all enabled printers with the same shared card component.
- **Print history log**: No persistent print history exists. Consider storing completed print events (file name, duration, filament used) to the DB — enables per-filament usage analytics over time.
- **Filament usage prediction**: Using print history + AMS weight tracking, a "days remaining" or "prints remaining" estimate per spool could be surfaced on the inventory tiles.
- **Notification support**: Browser push notifications or a Blazor toast/snackbar system for events like "print complete", "filament critical", or "printer disconnected".
- **Drag-to-reorder spools in AMS page**: Currently spools are listed but not manually orderable; drag-and-drop would improve usability when assigning slots.

## Architecture / Code Quality

- ~~**Migrate calculator to use global CSS variables**~~: **Addressed** — calculator tokens now derive from global theme variables (`--panel`, `--panel2`, `--border`, `--accent`, `--text`) in `calculator.css`/`site.css`, so themes apply consistently.
- ~~**Centralise `HandleStatusUpdate` pattern**~~: **Addressed** — `PrinterStatusStore` now centralizes live status fan-out; pages subscribe to one shared source instead of duplicating direct MQTT subscription wiring.
- **Add integration tests for BambuLabService**: The MQTT connection/disconnection race conditions that caused the `NullReferenceException` are hard to catch without concurrency tests. Consider adding tests with a mock MQTT client.
- ~~**Rate-limit `StateHasChanged` calls**~~: **Addressed** — `PrinterStatusStore` now debounces status fan-out (~200ms window) before notifying UI subscribers.

## Settings & Configuration

- ~~**Printer management UI**~~: **Addressed** — `MqttPage.razor` (📡 MQTT tab) provides full per-printer management: add, edit (rename/update IP/access code), delete, connect/disconnect without leaving the page.
- **Export print history to CSV**: Extend the existing CSV export to include print history once that feature is added.
- **Theme preference persisted per user**: Theme is currently stored app-wide in `ThemeService` (singleton, resets on restart). Consider tying it to `AppSettings` in the DB so the chosen theme survives restarts and Docker volume resets.

## Design

- ~~**Consistent idle state across all live widgets**~~: **Addressed** — `PrinterStatusCard.razor` is now shared by `Index.razor` and `PrinterPage.razor` for consistent live/idle/offline rendering.
- ~~**Skeleton loading states**~~: **Addressed** — reusable `LoadingSkeleton` component is in place and used on `Index`, `PrinterPage`, and `AMSPage`.
- ~~**Colour-coded AMS slot indicators**~~: **Preview implemented (optional)** — color-tinted slot-card backgrounds were tested; currently reverted per user preference in favor of wider slot color swatches.

## Simplification Opportunities (Next)

- **Add tests for bootstrap services**: Unit/integration tests around `DatabaseBootstrapService` and `AppSettingsBootstrapService` would make startup/schema changes safer.
- **Extract inventory tile primitives**: `InventoryPage.razor` still contains large repeated tile markup that could be split into focused subcomponents.
- **Single source for calculator token mapping**: Keep `--calc-*` mapping in one stylesheet to avoid drift between `site.css` and `calculator.css`.

