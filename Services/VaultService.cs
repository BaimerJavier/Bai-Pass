using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using PasswordManager.Models;

namespace PasswordManager.Services;

/// <summary>
/// Loads and saves the encrypted vault file. Location is per-user, per-OS:
///   Windows: %APPDATA%\PasswordManager\vault.dat
///   macOS:   ~/.config/PasswordManager/vault.dat  (ApplicationData on Unix)
/// </summary>
public class VaultService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public string VaultPath { get; }

    public VaultService()
    {
        // Optional override (used for isolated testing); falls back to the per-user location.
        string? overridePath = Environment.GetEnvironmentVariable("BAIPASS_VAULT");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(overridePath)!);
            VaultPath = overridePath;
            return;
        }

        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PasswordManager");
        Directory.CreateDirectory(dir);
        VaultPath = Path.Combine(dir, "vault.dat");
    }

    public bool VaultExists() => File.Exists(VaultPath);

    /// <summary>Decrypts and deserializes the entries. Throws on wrong password.</summary>
    public List<PasswordEntry> Load(string masterPassword)
    {
        byte[] file = File.ReadAllBytes(VaultPath);
        byte[] json = CryptoService.Decrypt(file, masterPassword);
        string text = Encoding.UTF8.GetString(json);
        return JsonSerializer.Deserialize<List<PasswordEntry>>(text, JsonOptions)
               ?? new List<PasswordEntry>();
    }

    /// <summary>Serializes, encrypts, and writes the entries atomically (temp file + replace).</summary>
    public void Save(IEnumerable<PasswordEntry> entries, string masterPassword)
    {
        string text = JsonSerializer.Serialize(entries, JsonOptions);
        byte[] plaintext = Encoding.UTF8.GetBytes(text);
        byte[] file = CryptoService.Encrypt(plaintext, masterPassword);

        string tmp = VaultPath + ".tmp";
        File.WriteAllBytes(tmp, file);
        if (File.Exists(VaultPath))
            File.Replace(tmp, VaultPath, null);
        else
            File.Move(tmp, VaultPath);
    }
}
