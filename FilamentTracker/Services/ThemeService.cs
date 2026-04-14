namespace FilamentTracker.Services;

public class ThemeService
{
    public string ThemeName { get; private set; } = "dark";

    public bool IsDarkMode => ThemeName == "dark";

    public event Action? OnThemeChanged;

    public void SetTheme(string themeName)
    {
        ThemeName = themeName;
        OnThemeChanged?.Invoke();
    }

    public void ToggleTheme()
    {
        SetTheme(IsDarkMode ? "light" : "dark");
    }

    public void SetDarkMode(bool isDark)
    {
        SetTheme(isDark ? "dark" : "light");
    }
}