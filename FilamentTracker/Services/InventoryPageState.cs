namespace FilamentTracker.Services;

/// <summary>
/// Scoped UI state for Inventory page filters/search.
/// </summary>
public sealed class InventoryPageState
{
    public string SearchText { get; set; } = string.Empty;
    public string TypeFilter { get; set; } = "all";
    public string SortOption { get; set; } = "newest";
    public bool FilterLowOnly { get; set; }
}

