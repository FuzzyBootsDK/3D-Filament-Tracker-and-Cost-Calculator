namespace FilamentTracker.Services;

public class ThemeService
{
    private bool _isDarkMode = true;
    
    public event Action? OnThemeChanged;
    
    public bool IsDarkMode => _isDarkMode;
    
    public void ToggleTheme()
    {
        _isDarkMode = !_isDarkMode;
        OnThemeChanged?.Invoke();
    }
    
    public void SetDarkMode(bool isDark)
    {
        _isDarkMode = isDark;
        OnThemeChanged?.Invoke();
    }
}
