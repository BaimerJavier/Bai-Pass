using System;
using System.IO;
using System.Text.Json;
using PasswordManager.Models;

namespace PasswordManager.Services;

/// <summary>
/// Loads/saves non-sensitive preferences to a plain JSON file next to the vault.
/// This file MUST never contain secrets (passwords, keys, decrypted data).
/// </summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string SettingsPath { get; }

    public SettingsService() : this(DefaultDirectory()) { }

    public SettingsService(string directory)
    {
        Directory.CreateDirectory(directory);
        SettingsPath = Path.Combine(directory, "config.json");
    }

    private static string DefaultDirectory()
    {
#if DEBUG
        string? overridePath = Environment.GetEnvironmentVariable("BAIPASS_VAULT");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetDirectoryName(overridePath)!;
#endif

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PasswordManager");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath))
                       ?? new AppSettings();
        }
        catch { /* corrupt/unreadable settings -> fall back to defaults */ }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch { /* preferences are best-effort; never block the app on this */ }
    }
}
