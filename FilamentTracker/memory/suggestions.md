# Suggestions

## UI / UX

- ~~**Multi-printer live widget**~~: **Addressed** — `PrinterPage.razor` (🖨️ Printer tab) now shows a real-time card for every configured printer with printing/idle/offline states, progress bar, temps, and layer count. The live widget in `Index.razor` still shows the first connected printer only; a tab/carousel upgrade there remains optional.
- **Print history log**: No persistent print history exists. Consider storing completed print events (file name, duration, filament used) to the DB — enables per-filament usage analytics over time.
- **Filament usage prediction**: Using print history + AMS weight tracking, a "days remaining" or "prints remaining" estimate per spool could be surfaced on the inventory tiles.
- **Notification support**: Browser push notifications or a Blazor toast/snackbar system for events like "print complete", "filament critical", or "printer disconnected".
- **Drag-to-reorder spools in AMS page**: Currently spools are listed but not manually orderable; drag-and-drop would improve usability when assigning slots.

## Architecture / Code Quality

- **Migrate calculator to use global CSS variables**: The calculator uses its own `--calc-*` namespace isolated from the theme system. Migrating to use `var(--bg)`, `var(--panel)` etc. would allow it to honor light mode (and all 5 themes) properly without duplicating variable overrides.
- **Centralise `HandleStatusUpdate` pattern**: Multiple pages (Index, AMSPage, PrinterPage) subscribe to `OnStatusUpdated`. A shared base component or service wrapper could reduce boilerplate and ensure consistent null-handling.
- **Add integration tests for BambuLabService**: The MQTT connection/disconnection race conditions that caused the `NullReferenceException` are hard to catch without concurrency tests. Consider adding tests with a mock MQTT client.
- **Rate-limit `StateHasChanged` calls**: High-frequency MQTT messages invoke `StateHasChanged` on every update. Debouncing (e.g., max once per 200ms) would reduce unnecessary re-renders.

## Settings & Configuration

- ~~**Printer management UI**~~: **Addressed** — `MqttPage.razor` (📡 MQTT tab) provides full per-printer management: add, edit (rename/update IP/access code), delete, connect/disconnect without leaving the page.
- **Export print history to CSV**: Extend the existing CSV export to include print history once that feature is added.
- **Theme preference persisted per user**: Theme is currently stored app-wide in `ThemeService` (singleton, resets on restart). Consider tying it to `AppSettings` in the DB so the chosen theme survives restarts and Docker volume resets.

## Design

- ~~**Consistent idle state across all live widgets**~~: **Partially addressed** — `PrinterPage.razor` implements a unified offline/idle/printing card pattern for the Printer tab. The live widget in `Index.razor` still uses its own inline styling. A shared `<PrinterStatusWidget>` component would fully unify these.
- **Skeleton loading states**: Pages that load data from the DB show nothing until loaded. Skeleton placeholders (translucent animated boxes) would improve perceived performance.
- **Colour-coded AMS slot indicators**: AMS slot tiles could use the filament `ColorHex` as a subtle background tint for quicker visual scanning.
