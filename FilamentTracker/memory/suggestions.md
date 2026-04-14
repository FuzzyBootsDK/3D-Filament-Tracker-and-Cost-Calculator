# Suggestions

## UI / UX

- **Multi-printer live widget**: The live tracking widget in `Index.razor` currently shows only the first connected printer. Consider a tab or carousel UI to switch between printers when multiple are connected simultaneously.
- **Print history log**: No persistent print history exists. Consider storing completed print events (file name, duration, filament used) to the DB — enables per-filament usage analytics over time.
- **Filament usage prediction**: Using print history + AMS weight tracking, a "days remaining" or "prints remaining" estimate per spool could be surfaced on the inventory tiles.
- **Notification support**: Browser push notifications or a Blazor toast/snackbar system for events like "print complete", "filament critical", or "printer disconnected".
- **Drag-to-reorder spools in AMS page**: Currently spools are listed but not manually orderable; drag-and-drop would improve usability when assigning slots.

## Architecture / Code Quality

- **Migrate calculator to use global CSS variables**: The calculator uses its own `--calc-*` namespace isolated from the theme system. Migrating to use `var(--bg)`, `var(--panel)` etc. would allow it to honor light mode properly without duplicating variable overrides.
- **Centralise `HandleStatusUpdate` pattern**: Multiple pages (Index, AMSPage, MqttPage) subscribe to `OnStatusUpdated`. A shared base component or service wrapper could reduce boilerplate and ensure consistent null-handling.
- **Add integration tests for BambuLabService**: The MQTT connection/disconnection race conditions that caused the `NullReferenceException` are hard to catch without concurrency tests. Consider adding tests with a mock MQTT client.
- **Rate-limit `StateHasChanged` calls**: High-frequency MQTT messages invoke `StateHasChanged` on every update. Debouncing (e.g., max once per 200ms) would reduce unnecessary re-renders.

## Settings & Configuration

- **Printer management UI**: Currently printers are added/removed in `SettingsPage.razor` but there is no per-printer edit (rename, update IP/access code without full reconnect). A proper printer management panel would improve UX.
- **Export print history to CSV**: Extend the existing CSV export to include print history once that feature is added.
- **Theme preference persisted per user**: Theme is currently stored app-wide. Consider tying it to the `AppSettings` model so it survives restarts and Docker volume resets.

## Design

- **Consistent idle state across all live widgets**: Different pages show slightly different idle/disconnected states. A shared Blazor component (`<PrinterStatusWidget>`) would ensure visual consistency.
- **Skeleton loading states**: Pages that load data from the DB show nothing until loaded. Skeleton placeholders (translucent animated boxes) would improve perceived performance.
- **Colour-coded AMS slot indicators**: AMS slot tiles could use the filament `ColorHex` as a subtle background tint for quicker visual scanning.
