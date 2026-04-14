# Thoughts

## On the CSS variable architecture
The project uses a well-structured dual-theme CSS variable system. All semantically meaningful values (color, shadow, radius, focus ring) are tokenised. The one inconsistency is the `calculator-root` scope which duplicates many of the same values under `--calc-*` names. When the calculator was built it was likely a self-contained component paste; migrating it to the global tokens is safe but low-priority.

## On the multi-printer refactor
The refactor from single to multi-connection was structurally sound — dictionaries keyed by `printerId` are idiomatic. The main risk area is the event-driven MQTT callbacks that read from those dictionaries after a potential removal (disconnect race). The null-guard fix addresses the symptom correctly; the underlying pattern (read-then-act outside the lock) is acceptable for this use case since the worst case is a silently dropped update.

## On the Blazor component model and MQTT threading
`InvokeAsync` + `StateHasChanged` inside `HandleStatusUpdate` is the correct Blazor Server pattern for updating UI from a background thread. The `try/catch` around the inner block correctly handles the `ObjectDisposedException` that occurs when a component unmounts while an async update is in-flight. This pattern should be replicated consistently wherever `OnStatusUpdated` is subscribed.

## On the design language
The use of emoji as icons is unconventional but works well here — the app is a personal/hobbyist tool, not an enterprise product, so the casual aesthetic is appropriate. The heavy font weights (800–900) are a deliberate choice that improves readability on the semi-transparent dark panels. Maintaining this weight scale is important when adding new UI elements.

## On AMS auto-weight sync
The decision to require `TagUid` (RFID) rather than `TrayUuid` for auto-matching is the right conservative call. `TrayUuid` is position-based and can change when spools are moved; `TagUid` is tied to the physical chip. The `OnlyDecrease` default is also correct — unexpected weight increases would confuse users more than slightly stale weight readings.
