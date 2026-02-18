# Weighted Average Pricing System - Implementation Guide

## Date: February 18, 2026

## 🎯 Feature Overview

Implemented a sophisticated weighted average pricing system that:
1. **Tracks purchase price per spool** - Each spool can have its own purchase price
2. **Calculates weighted average automatically** - Average price from all spools with prices
3. **Allows manual override** - Set a manual price at filament level if needed
4. **Updates in real-time** - Calculator always uses the latest calculated average

---

## 💰 How Pricing Works

### Pricing Priority (in order):
1. **Manual Override** - If set on filament, always use this
2. **Weighted Average** - Average of all spool prices that have values
3. **Default Fallback** - 149 if no prices are set

### Example Scenario:

**Day 1**: Add 1 spool at 150/kg
- Calculated price: **150**

**Day 2**: Add 2 spools at 200/kg each  
- Calculation: (150 + 200 + 200) / 3
- Calculated price: **183.33**

**Day 3**: Add 1 spool at 175/kg
- Calculation: (150 + 200 + 200 + 175) / 4
- Calculated price: **181.25**

**Set Manual Override**: 190/kg
- Calculated price: **190** (ignores spool prices)

**Remove Manual Override**: (set to empty)
- Back to weighted average: **181.25**

---

## 📊 Database Changes

### 1. Spool Model
Added `PurchasePricePerKg` field:
```csharp
public decimal? PurchasePricePerKg { get; set; }
```

### 2. Filament Model  
Added `CalculatedPricePerKg` computed property:
```csharp
public decimal CalculatedPricePerKg
{
    get
    {
        // 1. Use manual override if set
        if (PricePerKg.HasValue)
            return PricePerKg.Value;
        
        // 2. Calculate average from spools with prices
        var spoolsWithPrices = Spools
            .Where(s => s.PurchasePricePerKg.HasValue)
            .ToList();
        if (spoolsWithPrices.Any())
        {
            return spoolsWithPrices
                .Average(s => s.PurchasePricePerKg!.Value);
        }
        
        // 3. Default fallback
        return 149m;
    }
}
```

### 3. Database Migration
Automatically adds column to existing databases:
```sql
ALTER TABLE Spools ADD COLUMN PurchasePricePerKg REAL
```

---

## 🎨 UI Changes

### 1. Add Filament Page
**Changed:**
- ~~"Price Per Kg (optional)"~~ on filament
- ✅ **"Purchase Price Per Kg (optional)"** on spool

**Why:** Each spool can have different purchase prices, system calculates average

**Location:** After Diameter field, before Location

**Helper Text:** "Price for these spool(s) - used for weighted average"

---

### 2. Filament Detail Modal (Edit Popup)

#### Added: Price Display Section
```
💰 Calculated Average Price for Calculator
[Large display of calculated price]
Average from X spool(s) with prices
```

Shows:
- Current calculated price (used in calculator)
- Source of price (manual override, spool average, or default)
- Number of spools contributing to average

#### Added: Manual Override Field
```
Manual Price Override (optional)
[Input field]
Set this to override automatic average calculation
```

- Leave empty: Uses spool average
- Set value: Overrides calculation, always use this price

#### Added: Spool Purchase Price Field
When editing a spool:
```
Purchase Price Per Kg (optional)
[Input field]
Used for weighted average in calculator
```

- Set price for individual spool
- Updates weighted average when saved
- Each spool can have different price

#### Added: New Spool Purchase Price
When adding new spool:
```
Purchase Price Per Kg (optional)  
[Input field]
Updates weighted average for calculator
```

---

## 🔧 How to Use

### Scenario 1: Add New Filament with Price
1. Go to "Add Filament"
2. Fill in all required fields
3. Enter "Purchase Price Per Kg" (e.g., 149)
4. Click "Add Filament"
5. ✅ Price is saved for that spool
6. Calculator will use 149 for this filament

---

### Scenario 2: Add More Spools with Different Prices
1. Open filament in Inventory (click on it)
2. Click "➕ Add New Spool"
3. Fill in weight, etc.
4. Enter "Purchase Price Per Kg" (e.g., 199)
5. Click "Add Spool"
6. ✅ Calculator now uses average: (149 + 199) / 2 = **174**

---

### Scenario 3: Edit Existing Filament to Add Price
1. Click on filament in Inventory
2. Modal opens showing all spools
3. Look at "💰 Calculated Average Price" section
4. Select a spool from the list
5. Scroll to "Purchase Price Per Kg (optional)"
6. Enter price (e.g., 159)
7. Click "Save All Changes"
8. ✅ Price saved, average updates

---

### Scenario 4: Set Manual Override
**Why:** You want to use a specific price regardless of purchase prices

1. Open filament detail modal
2. Look at "Filament Details" section
3. Find "Manual Price Override (optional)"
4. Enter your desired price (e.g., 180)
5. Click "Save All Changes"
6. ✅ Calculator always uses 180, ignoring spool prices

**To remove override:**
1. Clear the "Manual Price Override" field (delete value)
2. Click "Save All Changes"
3. ✅ Back to using spool average

---

### Scenario 5: Mixed Prices (Real World Example)

**You have:**
- 3 spools from cheap batch: 120/kg each
- 2 spools from premium batch: 180/kg each
- 1 spool without price set

**Calculator shows:**
```
Average from 5 spool(s) with prices
(120 + 120 + 120 + 180 + 180) / 5 = 144/kg
```

**You add another spool at 200/kg:**
```
Average from 6 spool(s) with prices  
(120 + 120 + 120 + 180 + 180 + 200) / 6 = 153.33/kg
```

---

## 📈 Visual Examples

### Pricing Display in Modal

```
┌─────────────────────────────────────┐
│ 💰 Calculated Average Price         │
│                                     │
│ 165.50 per kg                       │
│ Average from 4 spool(s) with prices │
└─────────────────────────────────────┘

Manual Price Override (optional)
[ Leave empty to use spool average  ]
```

### Add New Spool with Price

```
┌─────────────────────────────────────┐
│ ➕ Add New Spool                    │
│                                     │
│ Total Weight (g)                    │
│ [ 1000          ]                   │
│                                     │
│ Weight Remaining (g)                │
│ [ 1000          ]                   │
│                                     │
│ Purchase Price Per Kg (optional)    │
│ [ 149.00        ]                   │
│ Updates weighted average            │
│                                     │
│ [  Add Spool  ]                     │
└─────────────────────────────────────┘
```

---

## 💻 Calculator Integration

### Before (Old Behavior):
- Used `filament.PricePerKg` directly
- If null, defaulted to 149
- No automatic averaging

### After (New Behavior):
- Uses `filament.CalculatedPricePerKg`
- Automatically calculates from all spools
- Respects manual override
- Always has a value

### Code Change:
```csharp
// OLD
pricePerKg = f.PricePerKg ?? 149m

// NEW  
pricePerKg = f.CalculatedPricePerKg
```

---

## 🧪 Testing Guide

### Test 1: Basic Average
1. Add filament with 1 spool at 150/kg
2. Go to calculator → Price shows **150**
3. Add another spool at 200/kg via edit modal
4. Refresh calculator → Price shows **175** (average)
✅ Pass if average is correct

### Test 2: Manual Override
1. Open filament edit modal
2. Set manual override to 190
3. Save
4. Go to calculator → Price shows **190**
5. Edit modal shows "Using manual override price"
✅ Pass if override works

### Test 3: Remove Override
1. Edit modal → Clear manual override
2. Save
3. Calculator shows spool average again
✅ Pass if goes back to average

### Test 4: No Prices Set
1. Add filament without setting any spool prices
2. Calculator shows **149** (default)
3. Edit modal shows "Default price (no spools have price set)"
✅ Pass if default works

### Test 5: Mixed Prices
1. Add spool 1: 120/kg
2. Add spool 2: 140/kg
3. Add spool 3: 160/kg
4. Calculator should show: **(120+140+160)/3 = 140**
✅ Pass if calculates correctly

### Test 6: Add Spool Updates Average
1. Filament has 2 spools: 150/kg, 150/kg (avg: 150)
2. Add 3rd spool at 180/kg
3. Average should become: **(150+150+180)/3 = 160**
4. Calculator updates automatically
✅ Pass if updates without refresh

---

## 🎯 Benefits

### For Users:
1. **Accurate costing** - Uses actual purchase prices
2. **Automatic averaging** - No manual calculation needed
3. **Flexible** - Can override when needed
4. **Historical tracking** - See what you paid for each spool
5. **Real-world pricing** - Accounts for bulk discounts, sales, etc.

### For Workflow:
1. **Set and forget** - Add prices once, used everywhere
2. **Evolves over time** - Average adjusts as you buy more
3. **Multiple suppliers** - Mix prices from different vendors
4. **Batch tracking** - Know which batch was cheaper
5. **Budget planning** - See average cost trend

---

## 📝 Summary

**What Changed:**
- ✅ Added `PurchasePricePerKg` to Spool model
- ✅ Added `CalculatedPricePerKg` to Filament model
- ✅ Database migration for new column
- ✅ Updated Add Filament page
- ✅ Enhanced Filament Detail modal
- ✅ Updated calculator integration
- ✅ Backward compatible

**How It Works:**
1. Each spool can have a purchase price
2. System calculates average of all prices
3. Manual override available if needed
4. Calculator always uses calculated price
5. Updates automatically as you add spools

**User Experience:**
1. Optional - works without prices
2. Flexible - manual or automatic
3. Visual - see calculated price
4. Transparent - know where price comes from
5. Accurate - real-world costing

---

## 🚀 Next Steps

1. **Restart app** to apply database migration
2. **Edit existing filaments** to add prices
3. **Add prices when adding new spools**
4. **Watch calculator use averages** automatically
5. **Enjoy accurate costing!** 🎉

The system is fully backward compatible - existing filaments without prices will continue to work with the default 149/kg pricing.

