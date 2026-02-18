# Calculator Fix - Script Loading Issue Resolved

## Date: February 18, 2026 - Final Fix

## 🔥 Critical Issue Found

### The Problem
The JavaScript files for the calculator were **NOT being loaded at all**!

The previous code removed the script loading logic, so when you navigated to the Print Calculator page:
- No `calculator-app.js` loaded
- No `calculator-gcode.js` loaded  
- No `calculator-pdf.js` loaded
- Result: Nothing worked!

### The Fix
Added back the dynamic script loading in `PrintCalculatorPage.razor` with:
1. **Script loading** - Loads all three JS files dynamically
2. **Cache busting** - Uses timestamp to force reload (`?v=timestamp`)
3. **Error handling** - Retries if initialization fails
4. **Proper timing** - Waits for scripts to load before calling functions

---

## ✅ What's Fixed Now

### 1. Scripts Load Properly
```csharp
// Loads with cache busting
gcodeScript.src = 'js/calculator-gcode.js?v=timestamp'
pdfScript.src = 'js/calculator-pdf.js?v=timestamp'
appScript.src = 'js/calculator-app.js?v=timestamp'
```

### 2. Inventory Data Passed
```csharp
await JS.InvokeVoidAsync("initCalculatorWithInventory", new
{
    currency = settings.Currency,
    inventory = inventoryData  // All your filaments
});
```

### 3. Everything Wired Up
- Material rows with dropdown
- Add/remove materials
- Printer profiles
- Calculations
- PDF export
- G-code import

---

## 🚀 How to Test NOW

### Step 1: Kill and Restart
```bash
pkill -f FilamentTracker
cd "/Users/lassesorensen/Library/CloudStorage/OneDrive-Personal/Projects/Filament Tracker v2/Tracker v2/FilamentTracker"
dotnet run --urls "http://localhost:5000"
```

### Step 2: Clear Browser Cache (IMPORTANT!)
**In your browser:**
- **Chrome/Edge**: Ctrl+Shift+Delete (Cmd+Shift+Delete on Mac)
  - Or: DevTools (F12) → Network tab → Check "Disable cache"
- **Safari**: Cmd+Option+E → Clear cache
- **Firefox**: Ctrl+Shift+Delete → Clear cache

**OR just do a hard refresh:**
- **Chrome/Edge**: Ctrl+F5 or Ctrl+Shift+R
- **Mac**: Cmd+Shift+R
- **Safari**: Cmd+Option+R

### Step 3: Test Basic Functionality
1. Go to Print Calculator
2. Open browser console (F12 → Console tab)
3. You should see:
   ```
   Initializing calculator with inventory data: {currency: "DKK", inventory: Array(X)}
   Inventory filaments loaded: X
   Materials body found: true
   Calculator initialized
   ```

### Step 4: Test Material Dropdown
1. Look at the first material row
2. Click the dropdown
3. Should see:
   - "Manual Entry" (first option)
   - All your filaments listed below

### Step 5: Test Inventory Selection
1. Select a filament from dropdown
2. Price should auto-fill
3. Price field should become disabled (grayed out)
4. Enter a weight (e.g., 25)
5. Watch calculations update

### Step 6: Test Manual Entry
1. Add another material (+ Add Material button)
2. Keep "Manual Entry" selected
3. Enter price (e.g., 199)
4. Enter weight (e.g., 30)
5. See calculations include both materials

### Step 7: Test Printer Profiles
1. Select "Bambu X1C" from Printer Profile dropdown
2. Scroll down to Advanced Settings
3. Should see all values update:
   - Printer Price: 8999
   - Lifetime: 4 years
   - Uptime: 85%
   - Maintenance: 600
   - Power: 150W
   - Electricity: 0.2
   - Material Factor: 1.12
   - Buffer Factor: 1.25
4. Calculations should update

### Step 8: Test Print Time
1. Enter Print Hours: 2
2. Enter Print Minutes: 30
3. Calculations should update immediately

### Step 9: Test Multiple Materials
1. Click "+ Add Material" 3 times
2. Should have 4 material rows total
3. Mix inventory and manual:
   - Row 1: Select inventory filament
   - Row 2: Manual entry
   - Row 3: Select different filament
   - Row 4: Manual entry
4. Enter weights for all
5. See total cost update

### Step 10: Test Remove
1. Click ✕ on second material
2. Should remove that row
3. Calculations update
4. Other rows unaffected

---

## 🐛 Debugging if It Still Doesn't Work

### Check Console for Errors
Open browser console (F12) and look for:

**Expected (Good):**
```
Initializing calculator with inventory data: ...
Inventory filaments loaded: 5
Materials body found: true
Calculator initialized
```

**If you see errors:**
1. **"initCalculatorWithInventory is not a function"**
   - Scripts didn't load
   - Clear cache and hard refresh

2. **"Materials body found: false"**
   - DOM not ready
   - Try refreshing the page

3. **No console messages at all**
   - Scripts not loaded
   - Check Network tab (F12 → Network)
   - Should see calculator-app.js, calculator-gcode.js, calculator-pdf.js
   - If 404 errors, check file paths

### Check Network Tab
1. F12 → Network tab
2. Reload page
3. Filter by "JS"
4. Should see:
   - ✅ calculator-app.js (Status: 200)
   - ✅ calculator-gcode.js (Status: 200)
   - ✅ calculator-pdf.js (Status: 200)

### Still Not Working?
1. **Close ALL browser tabs** with the app
2. **Restart the app**
3. **Open in incognito/private window**
4. Check console for errors

---

## 📊 Complete Feature Checklist

Test each feature:

- [ ] Scripts load (check console)
- [ ] Material dropdown shows filaments
- [ ] Select inventory filament
- [ ] Price auto-fills from inventory
- [ ] Price field disabled for inventory
- [ ] Manual entry mode works
- [ ] Add multiple materials (no crash)
- [ ] Remove materials
- [ ] Print time updates calculations
- [ ] Handling time updates calculations
- [ ] Printer profiles populate values
- [ ] Printer profiles update calculations
- [ ] Hardware cost affects total
- [ ] Packaging cost affects total
- [ ] VAT rate changes price
- [ ] Batch size changes unit cost
- [ ] G-code import works
- [ ] PDF export works
- [ ] Pricing tiers show correctly
- [ ] Custom margin slider works
- [ ] Batch optimization table shows
- [ ] Cost breakdown displays

---

## 🎯 Expected Behavior

### Material Row with Inventory Filament:
```
[Bambu Lab PLA - Blue ▼] [149.00 (disabled)] [25] [✕]
```

### Material Row with Manual Entry:
```
[Manual Entry ▼] [199 (editable)] [30] [✕]
```

### After Selecting Printer Profile (Bambu X1C):
- Printer Price: 8999
- Lifetime: 4
- Uptime: 85
- Maintenance: 600
- Power: 150
- Electricity: 0.2
- Material Factor: 1.12
- Buffer Factor: 1.25

### After Entering Data:
- Print Time: 2h 30m
- Material 1: Bambu PLA (149/kg, 25g)
- Material 2: Manual (199/kg, 30g)
- Hardware: 50
- Packaging: 25
- **Result**: Should show total cost, pricing options, batch optimization

---

## 💡 Key Changes Made

### File: `Components/PrintCalculatorPage.razor`
```csharp
// Added script loading with cache busting
await JS.InvokeVoidAsync("eval", $@"
    const appScript = document.createElement('script');
    appScript.src = 'js/calculator-app.js?v={version}';
    document.body.appendChild(appScript);
");

// Added delay for scripts to load
await Task.Delay(500);

// Added error handling and retry
try {
    await JS.InvokeVoidAsync("initCalculatorWithInventory", ...);
} catch {
    await Task.Delay(1000);
    await JS.InvokeVoidAsync("initCalculatorWithInventory", ...);
}
```

### File: `wwwroot/js/calculator-app.js`
```javascript
// Added console logging for debugging
console.log("Initializing calculator with inventory data:", data);
console.log("Inventory filaments loaded:", inventoryFilaments.length);
console.log("Materials body found:", materialsBody !== null);
console.log("Calculator initialized");
```

---

## ✅ Summary

**Root Cause**: Scripts weren't being loaded  
**Fix**: Added dynamic script loading with cache busting  
**Status**: Should work now!

**To verify it's working:**
1. Restart app
2. Clear browser cache
3. Open console
4. Look for "Calculator initialized" message
5. Test features

If you still have issues after following these steps, check the console for specific error messages and share them!

