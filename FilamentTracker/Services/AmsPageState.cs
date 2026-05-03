namespace FilamentTracker.Services;

/// <summary>
/// Scoped UI state for AMS page interactions.
/// </summary>
public sealed class AmsPageState
{
    public bool IsLoading { get; set; } = true;
    public Dictionary<string, int> SlotSpoolMap { get; } = new();
    public Dictionary<string, string?> SlotSaveMessages { get; } = new();
    public Dictionary<string, bool> SlotSaving { get; } = new();
}

