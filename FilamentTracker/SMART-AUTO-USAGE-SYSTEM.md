# Smart Auto-Usage System - Implementation Guide

## Date: February 18, 2026

## 🎯 Feature Overview

Implemented an intelligent filament usage tracking system that:
1. **Automatically selects the right spool** - Uses partially-used spools first, then oldest
2. **Handles multi-spool usage** - Can use filament across multiple spools in one recording
3. **Shows next spool indicator** - Visual cue showing which spool will be used next
4. **Quick usage buttons** - Fast recording with preset amounts (25g, 50g, 100g, 250g)
5. **Smart ordering** - Spools displayed in usage order (next-to-use at top)

---

## 🧠 How It Works

### Priority System

**Spool Selection Order:**
1. **Partially-used spools first** (< 100% remaining)
   - Rationale: Finish opened spools before starting new ones
2. **Oldest spools next** (by DateAdded)
   - Rationale: FIFO (First In, First Out) inventory management
3. **Empty spools** shown at bottom
   - Marked with DateEmptied timestamp

### Example Scenario

**You have 3 spools:**
- Spool A: 750g remaining (75%) - Added Jan 1
- Spool B: 1000g remaining (100%) - Added Jan 5  
- Spool C: 1000g remaining (100%) - Added Jan 10

**Usage order:**
1. ✅ **Spool A first** (partially used, priority)
2. ✅ **Spool B second** (100%, oldest)
3. ✅ **Spool C third** (100%, newest)

**Recording 800g usage:**
- Uses 750g from Spool A → Spool A is now empty
- Uses 50g from Spool B → Spool B has 950g left
- Message: "✅ Used 800g across 2 spool(s). 1 spool(s) emptied."

---

## 📊 Visual Indicators

### Spool List Display

**Next-to-use spool:**
```
┌─────────────────────────────────────────┐
│ ▶ On Spool - 750g                       │ ← Green arrow
│   75% • plastic • Next to use           │ ← Green text
│                               [Low]      │
├─────────────────────────────────────────┤
│ On Spool - 1000g                        │
│   100% • cardboard                      │
│                               [OK]       │
├─────────────────────────────────────────┤
│ Refill - 1000g                          │
│   100% • none                           │
│                               [OK]       │
└─────────────────────────────────────────┘
```

- **Green arrow (▶)** - Shows next spool to be used
- **"Next to use"** label in green
- **Green left border** - Visual emphasis
- **Automatic ordering** - Usage priority from top to bottom

---

## 🎨 UI Components

### Quick Usage Panel

Located at the top of "Filament Details" section:

```
┌─────────────────────────────────────────┐
│ 🎯 Quick Usage Recording                │
│ Automatically uses oldest/partially-    │
│ used spools first                       │
│                                         │
│ [  Grams used  ]  [  Record  ]          │
│                                         │
│ [-25g] [-50g] [-100g] [-250g]           │
│                                         │
│ ✅ Used 150g from spool. 850g remaining.│
└─────────────────────────────────────────┘
```

**Features:**
- Input field for custom amount
- Quick buttons for common amounts
- Real-time feedback message
- Green success theme

---

## 🔧 Technical Implementation

### Backend: Smart Usage Service

**File:** `Services/FilamentService.cs`

**New Method:** `RecordFilamentUsageAsync()`

```csharp
public async Task<UsageResult> RecordFilamentUsageAsync(
    int filamentId, 
    decimal gramsUsed)
{
    // 1. Get filament with available spools
    // 2. Order by: partially used first, then oldest
    // 3. Subtract from spools in order
    // 4. Mark spools as empty if depleted
    // 5. Return detailed result
}
```

**Key Logic:**
```csharp
var availableSpools = filament.Spools
    .Where(s => s.WeightRemaining > 0 && !s.DateEmptied.HasValue)
    .OrderBy(s => s.PercentRemaining == 100 ? 1 : 0) // Partial first
    .ThenBy(s => s.DateAdded)                        // Then oldest
    .ToList();
```

### Result Object

**UsageResult class:**
- `TotalGramsUsed` - Amount requested
- `SpoolsAffected` - List of spools used
- `InsufficientFilament` - Warning if not enough
- `ShortfallGrams` - How much short
- `GetSummaryMessage()` - Human-readable summary

**SpoolUsage class:**
- `SpoolId` - Which spool was used
- `GramsUsed` - How much from this spool
- `WasEmptied` - Was spool depleted
- `RemainingAfter` - Weight left in spool

---

## 💡 User Experience

### Before (Manual Selection):
1. Click on filament
2. Must figure out which spool to use
3. Select spool manually
4. Record usage on that specific spool
5. ❌ If not enough, need to manually use next spool
6. ❌ Easy to forget which spool was opened

### After (Smart Auto-Selection):
1. Click on filament
2. See "Next to use" indicator on spool
3. Enter grams in Quick Usage
4. Click Record
5. ✅ Automatically uses correct spool(s)
6. ✅ Handles multi-spool usage automatically
7. ✅ Shows detailed feedback

---

## 🎯 Usage Examples

### Example 1: Simple Usage
**Scenario:**
- 1 spool with 500g remaining
- Record 100g usage

**Result:**
```
✅ Used 100g from spool. 400g remaining.
```

**What happened:**
- Spool A: 500g → 400g

---

### Example 2: Multi-Spool Usage
**Scenario:**
- Spool A: 75g remaining (partial, oldest)
- Spool B: 1000g remaining (full)
- Record 200g usage

**Result:**
```
✅ Used 200g across 2 spool(s). 1 spool(s) emptied.
```

**What happened:**
- Spool A: 75g → 0g (emptied, DateEmptied set)
- Spool B: 1000g → 875g

---

### Example 3: Insufficient Filament
**Scenario:**
- Spool A: 100g remaining
- Spool B: 150g remaining
- Record 300g usage

**Result:**
```
⚠️ Only 250g available. Short by 50g!
```

**What happened:**
- Spool A: 100g → 0g (emptied)
- Spool B: 150g → 0g (emptied)
- User warned about shortfall

---

### Example 4: Multiple Partial Spools
**Scenario:**
- Spool A: 200g (20%, added Jan 1)
- Spool B: 500g (50%, added Jan 5)
- Spool C: 1000g (100%, added Jan 10)
- Record 750g usage

**Result:**
```
✅ Used 750g across 3 spool(s). 1 spool(s) emptied.
```

**What happened:**
- Spool A: 200g → 0g (emptied - partial, oldest)
- Spool B: 500g → 0g (emptied - partial, next)
- Spool C: 1000g → 950g (full spool, last)

---

## 🧪 Testing Guide

### Test 1: Single Spool Usage
1. Open filament with 1 spool (500g)
2. Enter 100 in Quick Usage
3. Click Record
4. ✅ Should show: "Used 100g from spool. 400g remaining."
5. ✅ Spool should update to 400g

### Test 2: Partial Spool Priority
1. Open filament with 2 spools:
   - Spool A: 300g (30%)
   - Spool B: 1000g (100%)
2. Note that Spool A shows "▶ Next to use"
3. Enter 150 in Quick Usage
4. Click Record
5. ✅ Spool A should go to 150g
6. ✅ Spool B should remain 1000g

### Test 3: Multi-Spool Usage
1. Open filament with 2 spools:
   - Spool A: 100g
   - Spool B: 500g
2. Enter 250 in Quick Usage
3. Click Record
4. ✅ Should show: "Used 250g across 2 spool(s). 1 spool(s) emptied."
5. ✅ Spool A should be empty (DateEmptied set)
6. ✅ Spool B should have 350g

### Test 4: Quick Buttons
1. Open any filament
2. Click "-50g" button
3. ✅ Should automatically record 50g
4. ✅ Message should appear immediately
5. ✅ Spool weight should update

### Test 5: Visual Indicators
1. Open filament with 3 spools (mixed partial/full)
2. ✅ Partially used spool should be at top
3. ✅ Green arrow "▶" on next-to-use spool
4. ✅ "Next to use" label in green
5. ✅ Green left border on that spool
6. ✅ Full spools below, ordered by date

### Test 6: Insufficient Filament
1. Open filament with 100g total
2. Enter 200 in Quick Usage
3. Click Record
4. ✅ Should show: "⚠️ Only 100g available. Short by 100g!"
5. ✅ All spools emptied
6. ✅ User warned about shortage

### Test 7: Spool Ordering After Usage
1. Open filament, note spool order
2. Record usage that empties first spool
3. ✅ Empty spool moves to bottom
4. ✅ Next spool becomes "Next to use"
5. ✅ Ordering updates automatically

---

## 🔄 Backward Compatibility

### Old Per-Spool Recording Still Available

The manual per-spool recording in the right panel is still functional:
- Select a specific spool
- Use the "Record Usage" section
- Quick buttons: -25g, -50g, -100g
- For manual control when needed

### Migration

No database changes needed:
- Uses existing Spool table
- Uses existing DateEmptied field
- Uses existing DateAdded field
- 100% compatible with existing data

---

## 📋 Files Modified

1. **Services/FilamentService.cs**
   - Added `RecordFilamentUsageAsync()` method
   - Added `UsageResult` class
   - Added `SpoolUsage` class

2. **Components/FilamentDetailModal.razor**
   - Added Quick Usage panel
   - Updated spool list ordering
   - Added visual indicators for next-to-use
   - Added quick usage methods
   - Added success/error messaging

---

## ✨ Benefits

### For Users:
1. **Faster workflow** - One click instead of multiple
2. **No thinking required** - System knows which spool to use
3. **Visual guidance** - See next spool clearly
4. **Accurate tracking** - Automatic FIFO inventory management
5. **Multi-spool support** - Handles prints that span spools

### For Inventory Management:
1. **FIFO compliance** - Oldest filament used first
2. **Partial spool priority** - Finish opened spools first
3. **Automatic emptying** - DateEmptied tracked automatically
4. **Detailed logging** - Know exactly what was used
5. **Shortage warnings** - Alert if not enough filament

### For Print Planning:
1. **Quick checks** - See if enough filament for print
2. **Visual indicators** - Know spool status at a glance
3. **Multi-spool awareness** - Know if print will span spools
4. **Preset amounts** - Common usage amounts pre-configured

---

## 🎊 Summary

**What's New:**
- ✅ Smart auto-selection of spools
- ✅ Priority system (partial first, then oldest)
- ✅ Multi-spool usage support
- ✅ Quick usage panel with presets
- ✅ Visual "next to use" indicators
- ✅ Automatic spool ordering
- ✅ Detailed usage feedback
- ✅ Shortage warnings

**How It Works:**
1. Click filament → See ordered spools
2. Enter grams → Click Record
3. System automatically uses correct spool(s)
4. Get instant feedback on what happened
5. Spools update and reorder automatically

**Build Status:**
- ✅ SUCCESS (0 errors, 0 warnings)

**Ready to Use:**
- Restart app
- Open any filament
- Try Quick Usage panel
- Watch automatic spool selection work!

The smart usage system is fully implemented and ready to streamline your filament tracking! 🎉

