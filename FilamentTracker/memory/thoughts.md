# Thoughts

## On the CSS variable architecture
The project uses a well-structured dual-theme CSS variable system. All semantically meaningful values (color, shadow, radius, focus ring) are tokenised. The one inconsistency is the `calculator-root` scope which duplicates many of the same values under `--calc-*` names. When the calculator was built it was likely a self-contained component paste; migrating it to the global tokens is safe but low-priority. With 5 themes now active, the calculator's internal overrides also mean it won't honor Starbucks/Harmony/Spring automatically — this is the strongest motivation to eventually migrate it.

## On the multi-printer refactor
The refactor from single to multi-connection was structurally sound — dictionaries keyed by `printerId` are idiomatic. The main risk area is the event-driven MQTT callbacks that read from those dictionaries after a potential removal (disconnect race). The null-guard fix addresses the symptom correctly; the underlying pattern (read-then-act outside the lock) is acceptable for this use case since the worst case is a silently dropped update.

## On the Blazor component model and MQTT threading
`InvokeAsync` + `StateHasChanged` inside `HandleStatusUpdate` is the correct Blazor Server pattern for updating UI from a background thread. The `try/catch` around the inner block correctly handles the `ObjectDisposedException` that occurs when a component unmounts while an async update is in-flight. This pattern should be replicated consistently wherever `OnStatusUpdated` is subscribed — `PrinterPage.razor` follows this pattern.

## On the design language
The use of emoji as icons is unconventional but works well here — the app is a personal/hobbyist tool, not an enterprise product, so the casual aesthetic is appropriate. The heavy font weights (800–900) are a deliberate choice that improves readability on the semi-transparent dark panels. Maintaining this weight scale is important when adding new UI elements.

## On the multi-theme system
The 5-theme approach (dark/light/starbucks/harmony/spring) is a clean extension of the existing CSS variable architecture — each theme is just a new CSS class block overriding the same token names. The background radial gradients per theme give each one a distinct personality without requiring any component-level changes. The visual card picker in SettingsPage is a better UX than radio buttons, and the `ThemeOption` record + `_themes` array makes adding future themes trivial. The main outstanding gap is persistence: theme resets to dark on app restart, which is surprising behaviour for users.

## On AMS auto-weight sync
The decision to require `TagUid` (RFID) rather than `TrayUuid` for auto-matching is the right conservative call. `TrayUuid` is position-based and can change when spools are moved; `TagUid` is tied to the physical chip. The `OnlyDecrease` default is also correct — unexpected weight increases would confuse users more than slightly stale weight readings.

## On PrinterPage vs MqttPage separation
Splitting "management" (`MqttPage` — printer CRUD, connection, MQTT log) from "monitoring" (`PrinterPage` — live status dashboard) is the right call. The MQTT tab was already long; adding a full status dashboard would have made it unwieldy. The Printer tab gives the live view a dedicated home and is the natural first destination for a user who just wants to check on an active print.
