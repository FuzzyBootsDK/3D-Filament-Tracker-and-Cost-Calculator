namespace FilamentTracker.Services;

public class ThemeService
{
    public bool IsDarkMode { get; private set; } = true;

    public event Action? OnThemeChanged;

    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        OnThemeChanged?.Invoke();
    }

    public void SetDarkMode(bool isDark)
    {
        IsDarkMode = isDark;
        OnThemeChanged?.Invoke();
    }
}