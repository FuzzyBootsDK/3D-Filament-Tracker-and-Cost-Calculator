# Quick Start Guide

## Running the Application

1. Open terminal in the FilamentTracker directory

2. Run the application:
   ```bash
   dotnet run
   ```

3. Open your browser to the URL shown (typically https://localhost:5001)

## First Time Setup

### Option 1: Import Sample Data
1. Go to **Settings** page
2. Click **Import Data**
3. Select the `sample-import.csv` file
4. Click **Import CSV**

### Option 2: Add Manually
1. Go to **Add Filament** page
2. Fill in the form
3. Click **Add Filament**

## Key Features to Try

### Inventory Management
- View all filaments in card layout
- Search and filter by brand, type, or color
- Click any card to see details and manage spools

### Recording Usage
1. Click a filament card
2. Select a spool
3. Enter grams used or use quick buttons (-25g, -50g, -100g)
4. Click **Record**

### Reusable Spools
- Track empty reusable spools
- See which are available vs in use

### Theme Toggle
- Go to **Settings**
- Switch between Dark and Light mode

### Data Export
- Go to **Settings**
- Click **Export to CSV** to backup your data

## Database Location

The SQLite database is created at:
```
FilamentTracker/filament.db
```

## Tips

- Color codes use hex format: #FF0000 for red
- Low stock warning at < 300g
- Critical warning at < 150g
- Multiple spools of same filament are grouped together

## Stopping the Application

Press `Ctrl+C` in the terminal where the app is running.
