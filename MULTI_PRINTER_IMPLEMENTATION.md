# 🖨️ Multi-Printer Support Implementation Guide

## ✅ Phase 1 Complete: MQTT Log Printer Filtering

### **What Was Implemented:**

1. **Enhanced MqttLogEntry Model**
   - Added `PrinterId` (int?) - Database ID of the printer
   - Added `PrinterName` (string) - Friendly name for display

2. **BambuLabService Tracking**
   - Tracks currently connected printer ID and name
   - Includes printer info in all MQTT log entries
   - New methods: `GetConnectedPrinterId()`, `GetConnectedPrinterName()`

3. **MQTT Log Dropdown Filter**
   - Dropdown showing "All Printers" + list of configured printers
   - Filters log display by selected printer
   - Shows printer name badge `[Printer Name]` in each log entry (colored orange)
   - Message count updates based on filter

### **How to Use:**
1. Navigate to **MQTT** tab
2. Connect to a printer (printer name now tracked)
3. In **MQTT Message Log** section, use the **Filter** dropdown
4. Select a specific printer or "All Printers"
5. Log displays only messages from selected printer

---

## 📋 Remaining Phases Overview

### **Phase 2: AMS Page Multi-Printer Support**
**Status:** ⏳ Not Started  
**Complexity:** Medium  
**Estimated Time:** 2-3 hours

#### Required Changes:
1. **Database Schema:**
   - Add `PrinterId` column to relevant AMS-tracking tables (if you store AMS spool associations in DB)
   - Migration script to update existing records

2. **AMS Page UI:**
   ```razor
   <!-- Add printer selector at top -->
   <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 20px;">
       <label>Printer:</label>
       <select @bind="_selectedAmsPrinterId">
           @foreach (var printer in _printers)
           {
               <option value="@printer.Id">@printer.Name</option>
           }
       </select>
   </div>
   ```

3. **Filtering Logic:**
   - Only show AMS units from selected printer
   - Ensure spool linking saves printer association
   - Update auto-match logic to consider printer ID

#### Architecture Notes:
- Current `PrintStatus.AMSUnits` represents single printer's AMS
- Need to track which printer each AMS unit belongs to
- `FilamentService.FindSpoolByAmsIdAsync()` may need printer context

---

### **Phase 3: MQTT Relay Multi-Printer Support**
**Status:** ⏳ Not Started  
**Complexity:** High  
**Estimated Time:** 4-6 hours

#### Current Limitation:
- **BambuLabService** supports only ONE concurrent connection
- **MqttRelayService** relays from this single connection

#### Required Architecture Refactor:

**Option A: Multi-Connection BambuLabService (Recommended)**
```csharp
public class BambuLabService
{
    private Dictionary<int, IMqttClient> _printerConnections = new();
    private Dictionary<int, PrintStatus> _printerStatuses = new();
    
    public async Task ConnectPrinterAsync(Printer printer)
    {
        var client = CreateMqttClient();
        // ... setup connection
        _printerConnections[printer.Id] = client;
    }
    
    public async Task DisconnectPrinterAsync(int printerId)
    {
        if (_printerConnections.TryGetValue(printerId, out var client))
        {
            await client.DisconnectAsync();
            _printerConnections.Remove(printerId);
        }
    }
    
    public PrintStatus GetPrinterStatus(int printerId)
    {
        return _printerStatuses.TryGetValue(printerId, out var status) 
            ? status 
            : new PrintStatus();
    }
}
```

**Option B: Simple Approach (Current)**
- Keep single connection model
- Users manually switch between printers
- Relay only works for currently connected printer
- ⚠️ This is what's currently implemented

#### MQTT Relay UI Enhancement:
```razor
<h3>Select Printers to Relay</h3>
@foreach (var printer in _printers.Where(p => p.Enabled))
{
    <div class="checkRow">
        <input type="checkbox" 
               @bind="printer.RelayEnabled" 
               id="relay_@printer.Id" 
               disabled="@(!BambuLabService.IsPrinterConnected(printer.Id))"/>
        <label for="relay_@printer.Id">
            @printer.Name 
            @if (BambuLabService.IsPrinterConnected(printer.Id))
            {
                <span style="color: #10b981;">● Connected</span>
            }
        </label>
    </div>
}
```

#### MqttRelayService Changes:
- Subscribe to messages from multiple BambuLabService connections
- Relay all enabled printers' messages
- Clients can subscribe to specific printer topics: `device/{serialNumber}/report`

---

### **Phase 4: Live View Printer Selector**
**Status:** ⏳ Not Started  
**Complexity:** Low  
**Estimated Time:** 1 hour

#### Current Behavior:
- Live view shows status from `BambuLabService.GetCurrentStatus()`
- With single-connection model, this is the ONE connected printer
- No printer selection available

#### Enhancement Plan:
```razor
<!-- In Index.razor Live Tracking Widget -->
<div class="liveTrackerHeader">
    <div class="liveHeaderLeft">
        <span class="liveIcon">🖨️</span>
        <select class="input" 
                style="background: transparent; border: none; font-weight: 600;" 
                @bind="_selectedLiveViewPrinterId">
            @foreach (var printer in _connectedPrinters)
            {
                <option value="@printer.Id">@printer.Name</option>
            }
        </select>
    </div>
    <!-- ... status indicator -->
</div>
```

#### Logic:
- **With Single Connection:** Dropdown shows only connected printer (auto-selected)
- **With Multi-Connection:** Dropdown shows all connected printers, user can switch view
- `_selectedLiveViewPrinterId` determines which `PrintStatus` to display

---

## 🚀 Implementation Priority Recommendation

### **Immediate (Already Done):**
✅ MQTT Log Printer Filtering

### **Next Steps (Recommended Order):**

1. **Live View Printer Selector** (Easy Win - 1 hour)
   - Low complexity, high user value
   - Works with current single-connection model
   - Users can see which printer is being monitored

2. **AMS Page Multi-Printer** (Medium - 2-3 hours)
   - Medium complexity
   - Essential for users with multiple printers
   - Requires database changes (migration needed)

3. **MQTT Relay Multi-Source** (Complex - 4-6 hours)
   - High complexity
   - Requires BambuLabService refactor for true multi-connection support
   - OR keep simple approach: relay only from currently connected printer

---

## 🔧 Technical Decisions

### **Multi-Connection Architecture Decision:**

**Option 1: Full Multi-Connection (Future-Proof)**
- Pros:
  - True concurrent monitoring of all printers
  - Relay can broadcast from multiple sources
  - Live view can show any printer status
  - Professional-grade solution
- Cons:
  - Complex refactor of BambuLabService
  - Event handling becomes more complex (need printer ID in events)
  - Resource intensive (multiple MQTT clients)
  - Testing complexity increases

**Option 2: Single Connection + Manual Switching (Current)**
- Pros:
  - Simple, proven architecture
  - No major refactor needed
  - Easy to test and maintain
  - Lower resource usage
- Cons:
  - Can only monitor one printer at a time
  - User must manually switch connections
  - Relay limited to single source

**Recommendation:**  
Start with **Option 2** (current implementation). If users demand concurrent monitoring, implement **Option 1** as a later enhancement.

---

## 📊 Current Architecture

```
User Interface (Blazor Components)
├── MqttPage.razor
│   ├── Printer Management (CRUD)
│   ├── Connection Section (select + connect)
│   ├── MQTT Relay Settings
│   └── MQTT Log (✅ with printer filter)
├── AMSPage.razor (needs printer selector)
└── Index.razor / Live View (needs printer selector)

Service Layer
├── BambuLabService (Singleton)
│   ├── Single IMqttClient connection
│   ├── Tracks connected printer ID & name ✅
│   ├── Events: OnStatusUpdated, OnMqttMessageLogged
│   └── MQTT Log with printer info ✅
├── MqttRelayService (Singleton)
│   └── Relays from single BambuLabService connection
└── FilamentService (Scoped)
    └── Printer CRUD operations ✅

Database
├── Printers table ✅
├── Filaments table
├── AMSSlot associations (needs PrinterId)
└── AppSettings table
```

---

## 🧪 Testing Checklist

### **Phase 1 (MQTT Log Filter):**
- ✅ Build successful
- ⏳ Test: Connect to printer, verify printer name appears in logs
- ⏳ Test: Filter dropdown shows correct printers
- ⏳ Test: Filtering works correctly (show all vs specific printer)
- ⏳ Test: Message count updates when filter changes
- ⏳ Test: Disconnect/reconnect preserves log entries with correct printer info

### **Future Phases:**
- [ ] AMS page shows correct units for selected printer
- [ ] Spool linking saves printer association
- [ ] Relay broadcasts from enabled printers
- [ ] Live view switches between connected printers
- [ ] Multiple printers can be connected simultaneously (if implementing multi-connection)

---

## 📝 Database Migration Notes

### **Current Schema:**
```sql
CREATE TABLE IF NOT EXISTS Printers (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    PrinterType TEXT NOT NULL DEFAULT 'BambuLab',
    IpAddress TEXT NOT NULL,
    AccessCode TEXT NOT NULL,
    SerialNumber TEXT NOT NULL,
    Enabled INTEGER NOT NULL DEFAULT 1,
    IsDefault INTEGER NOT NULL DEFAULT 0,
    DateAdded TEXT NOT NULL,
    Location TEXT,
    ColorHex TEXT
);
```

### **Future Schema Changes (for AMS multi-printer):**

If you store AMS spool associations in database:
```sql
ALTER TABLE AMSSlotAssociations ADD COLUMN PrinterId INTEGER REFERENCES Printers(Id);
```

Or if using in-memory tracking, no database changes needed.

---

## 💡 How Current Live View Works

### **Current Implementation:**
```csharp
// In Index.razor
private PrintStatus? currentPrint;

protected override async Task OnInitializedAsync()
{
    currentPrint = BambuLabService.GetCurrentStatus();
    BambuLabService.OnStatusUpdated += OnPrintStatusUpdated;
}

private void OnPrintStatusUpdated(PrintStatus status)
{
    currentPrint = status;
    InvokeAsync(StateHasChanged);
}
```

### **With Single Connection:**
- Shows status from the ONE connected printer
- When user connects to Printer A, live view shows Printer A
- When user switches to Printer B, live view automatically updates

### **With Multi-Connection (Future):**
```csharp
// Would need:
private int _selectedLiveViewPrinterId;
private PrintStatus GetDisplayedStatus() => 
    BambuLabService.GetPrinterStatus(_selectedLiveViewPrinterId);

// OnStatusUpdated would include printer ID
BambuLabService.OnStatusUpdated += (printerId, status) => { ... };
```

---

## 🎯 Summary

### **What's Working Now:**
✅ Multi-printer configuration storage  
✅ Printer selection and connection switching  
✅ MQTT log entries tagged with printer info  
✅ MQTT log dropdown filter by printer  
✅ Printer name displayed in log entries  

### **What's Next:**
1. **Live View**: Add printer selector dropdown
2. **AMS Page**: Add printer selector, filter AMS units
3. **MQTT Relay**: Decide on architecture (multi-source vs single-source)

### **Long-Term Vision:**
- Full concurrent multi-printer monitoring
- Per-printer relay enable/disable
- Dashboard view showing all printers simultaneously
- Print queue management across multiple printers

---

## 📞 Questions to Consider:

1. **Do users need to monitor multiple printers simultaneously?**
   - If YES → Implement full multi-connection architecture
   - If NO → Keep current single-connection with manual switching

2. **Should MQTT relay broadcast from all connected printers?**
   - If YES → Need multi-connection + relay service refactor
   - If NO → Current relay (single source) is sufficient

3. **Is AMS multi-printer support high priority?**
   - If YES → Implement Phase 2 next
   - If NO → Focus on Live View selector first (easier)

---

**Version:** 1.0  
**Date:** 2024  
**Status:** Phase 1 Complete ✅ | Phases 2-4 Pending ⏳
