namespace FilamentTracker.Services;

public class ThemeService
{
    public string ThemeName { get; private set; } = "dark";

    // Backward-compat: true for every theme except "light"
    public bool IsDarkMode => ThemeName != "light";

    public event Action? OnThemeChanged;

    public void SetTheme(string name)
    {
        ThemeName = name;
        OnThemeChanged?.Invoke();
    }

    // Backward-compat wrappers
    public void SetDarkMode(bool isDark) => SetTheme(isDark ? "dark" : "light");

    public void ToggleTheme() => SetTheme(IsDarkMode ? "light" : "dark");
}
