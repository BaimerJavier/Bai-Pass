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
}
