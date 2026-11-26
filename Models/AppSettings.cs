namespace PasswordManager.Models;

/// <summary>
/// Non-sensitive user preferences, persisted as plain JSON (never contains secrets).
/// </summary>
public class AppSettings
{
    /// <summary>Idle minutes before the session auto-locks. Allowed: 1, 5, 10, 15, 30.</summary>
    public int AutoLockMinutes { get; set; } = 10;

    /// <summary>Seconds before a copied password is cleared from the clipboard. Allowed: 5,10,15,30.</summary>
    public int ClipboardClearSeconds { get; set; } = 15;

    /// <summary>Custom full path to vault.dat. Null/empty = use the default per-user location.</summary>
    public string? VaultPath { get; set; }

    /// <summary>Color theme: "System" (follow Windows), "Light" or "Dark".</summary>
    public string Theme { get; set; } = "System";

    /// <summary>Main layout: "Tabla", "Mosaico" or "Buscador".</summary>
    public string ViewMode { get; set; } = "Tabla";
}
