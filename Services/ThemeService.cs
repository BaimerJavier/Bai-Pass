using Avalonia;
using Avalonia.Styling;

namespace PasswordManager.Services;

/// <summary>Applies the user's color-theme preference ("System", "Light" or "Dark") to the app.</summary>
public static class ThemeService
{
    public static void Apply(string? theme)
    {
        if (Application.Current is null) return;
        Application.Current.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
