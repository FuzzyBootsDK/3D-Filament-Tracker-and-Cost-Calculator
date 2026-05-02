namespace FilamentTracker.Services;

public class ThemeService
{
    public string ThemeName { get; private set; } = "dark";

    // Backward-compat: true for every theme except "nebula"
    public bool IsDarkMode => ThemeName != "nebula";

    public event Action? OnThemeChanged;

    public void SetTheme(string name)
    {
        // Accept legacy persisted value
        if (string.Equals(name, "light", StringComparison.OrdinalIgnoreCase))
            name = "nebula";

        ThemeName = name;
        OnThemeChanged?.Invoke();
    }

    // Backward-compat wrappers
    public void SetDarkMode(bool isDark) => SetTheme(isDark ? "dark" : "nebula");

    public void ToggleTheme() => SetTheme(IsDarkMode ? "nebula" : "dark");
}
