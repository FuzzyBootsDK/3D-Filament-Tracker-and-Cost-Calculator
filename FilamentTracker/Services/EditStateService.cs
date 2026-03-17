namespace FilamentTracker.Services;

/// <summary>
/// Singleton service to track when a filament is being edited in the modal.
/// Prevents MQTT updates from refreshing the inventory and disrupting user edits.
/// </summary>
public class EditStateService
{
    private int? _currentFilamentId;

    /// <summary>
    /// Gets whether any filament is currently being edited.
    /// </summary>
    public bool IsEditing => _currentFilamentId.HasValue;

    /// <summary>
    /// Gets the ID of the filament currently being edited, if any.
    /// </summary>
    public int? CurrentFilamentId => _currentFilamentId;

    /// <summary>
    /// Marks a filament as being edited.
    /// </summary>
    public void BeginEditing(int filamentId)
    {
        _currentFilamentId = filamentId;
    }

    /// <summary>
    /// Clears the editing state.
    /// </summary>
    public void EndEditing()
    {
        _currentFilamentId = null;
    }
}
