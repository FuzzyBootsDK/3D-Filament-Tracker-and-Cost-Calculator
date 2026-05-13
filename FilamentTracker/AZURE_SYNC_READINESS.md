# Azure Inventory Sync Readiness

Last updated: 2026-05-13

## What Is Implemented

### 1) Settings + persistence
- Added Azure sync settings to `AppSettings`:
  - `AzureSyncEnabled`
  - `AzureAutoPushEnabled`
  - `AzureEndpoint`
  - `AzureUsername`
  - `AzurePassword`
- Updated settings persistence in `FilamentService`:
  - `GetSettingsAsync()` reads all Azure fields.
  - `UpdateSettingsAsync()` writes all Azure fields.
- Updated DB bootstrap in `DatabaseBootstrapService`:
  - Adds missing Azure columns for existing SQLite DB files.
  - Sets default values for bool fields.

### 2) UI in Settings
- Added new section in `Components/SettingsPage.razor`:
  - Enable/disable Azure sync
  - Enable/disable hourly auto-push
  - Endpoint, username, password inputs
  - Save Azure settings button
  - Manual actions:
    - `Push Inventory Now`
    - `Pull Inventory`

### 3) Manual push from Inventory page
- Added `Push to Azure` button in `Components/InventoryPage.razor` controls.
- Displays inline success/error status message after push attempt.

### 4) Sync services and background worker
- Added `Services/AzureInventorySyncService.cs`:
  - `PushInventoryAsync(source)`
    - Validates settings.
    - Exports inventory to CSV (`CsvService`).
    - Sends JSON payload to configured endpoint.
  - `PullInventoryAsync()` currently returns a "not wired" message.
- Added `Services/AzureInventoryBackgroundSyncService.cs`:
  - Runs hourly using `PeriodicTimer`.
  - Attempts push only when:
    - `AzureSyncEnabled == true`
    - `AzureAutoPushEnabled == true`

### 5) Startup registration
- Updated `Program.cs`:
  - `AddHttpClient()`
  - `AddScoped<AzureInventorySyncService>()`
  - `AddHostedService<AzureInventoryBackgroundSyncService>()`

### 6) Optional env var overrides
- Added startup overrides in `AppSettingsBootstrapService`:
  - `AZURE_SYNC_ENABLED`
  - `AZURE_SYNC_AUTO_PUSH_ENABLED`
  - `AZURE_SYNC_ENDPOINT`
  - `AZURE_SYNC_USERNAME`
  - `AZURE_SYNC_PASSWORD`

## What Is Missing (Before Production Use)

### 1) Final Azure API contract
You still need to define and implement the server-side Azure endpoint contract.

Current app expectation for push payload:
- JSON body with:
  - `username`
  - `password`
  - `source`
  - `exportedAtUtc`
  - `format` (`csv`)
  - `inventoryCsv`

Open questions:
- Exact endpoint path and method
- Auth model (basic auth, bearer token, API key, etc.)
- Response format and error schema
- Rate limits and idempotency behavior

### 2) Pull/import flow
- `PullInventoryAsync()` is intentionally scaffolded and does not import data yet.
- Missing implementation includes:
  - Download inventory payload from Azure
  - Parse/validate payload
  - Conflict strategy:
    - replace local
    - merge by key
    - user-confirmed import mode

### 3) Security hardening
- Credentials are currently stored in SQLite settings as plain text.
- Recommended before release:
  - Use token-based auth
  - Avoid storing raw passwords where possible
  - Add secrets handling/encryption strategy

### 4) Retry and resilience
Current push behavior is single-attempt.
Recommended additions:
- Retry policy with backoff
- Last successful sync timestamp
- Last error reason in UI
- Optional queue/offline retry

### 5) Observability and admin UX
Recommended additions:
- Display next scheduled auto-push time
- Display last successful/failed push in Settings
- Add a lightweight sync log/history

## Quick Verification Status
- Build verification was run after implementation and succeeded.
- Existing unrelated warnings remain in `MqttRelayService` (obsolete API warnings).

## Suggested Next Implementation Order
1. Define Azure push/pull API contract.
2. Implement and test `PullInventoryAsync()` import pipeline.
3. Add auth hardening (token/secrets strategy).
4. Add retry + sync status tracking.
5. Add merge/replace UX for restore scenarios.

