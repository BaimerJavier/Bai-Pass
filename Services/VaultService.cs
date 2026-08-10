using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PasswordManager.Models;

namespace PasswordManager.Services;

/// <summary>
/// Loads and saves the encrypted vault file. Location is per-user, per-OS:
///   Windows: %APPDATA%\PasswordManager\vault.dat
///   macOS:   ~/.config/PasswordManager/vault.dat  (ApplicationData on Unix)
///
/// The key-based methods (<see cref="LoadWithKey"/> / <see cref="SaveWithKey"/>) are the ones
/// used by <see cref="SessionManager"/>: the derived key is passed in, so the master password
/// never reaches this layer.
/// </summary>
public class VaultService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public string VaultPath { get; }

    public VaultService() : this(ResolveDefaultPath()) { }

    /// <summary>Explicit-path constructor, used by tests to isolate the vault file.</summary>
    public VaultService(string vaultPath)
    {
        VaultPath = vaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(vaultPath)!);
    }

    private static string ResolveDefaultPath()
    {
        // Optional override (used for isolated testing); falls back to the per-user location.
        string? overridePath = Environment.GetEnvironmentVariable("BAIPASS_VAULT");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return overridePath;

        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PasswordManager");
        return Path.Combine(dir, "vault.dat");
    }

    public bool VaultExists() => File.Exists(VaultPath);

    /// <summary>Reads the salt stored in the vault header (needed to derive the key at unlock).</summary>
    public byte[] ReadSalt() => CryptoService.ReadSalt(File.ReadAllBytes(VaultPath));

    /// <summary>Decrypts and deserializes the entries using an already-derived key.</summary>
    public List<PasswordEntry> LoadWithKey(ReadOnlySpan<byte> key)
    {
        byte[] file = File.ReadAllBytes(VaultPath);
        byte[] json = CryptoService.DecryptWithKey(file, key);
        try
        {
            string text = Encoding.UTF8.GetString(json);
            return JsonSerializer.Deserialize<List<PasswordEntry>>(text, JsonOptions)
                   ?? new List<PasswordEntry>();
        }
        finally
        {
            // Wipe the decrypted JSON bytes. The intermediate UTF-8 string is immutable and
            // stays GC-managed until collected (documented limitation).
            CryptographicOperations.ZeroMemory(json);
        }
    }

    /// <summary>Serializes, encrypts with the given key+salt, and writes atomically (temp + replace).</summary>
    public void SaveWithKey(IEnumerable<PasswordEntry> entries, ReadOnlySpan<byte> key, byte[] salt)
    {
        string text = JsonSerializer.Serialize(entries, JsonOptions);
        byte[] plaintext = Encoding.UTF8.GetBytes(text);
        byte[] file;
        try
        {
            file = CryptoService.EncryptWithKey(plaintext, key, salt);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }

        string tmp = VaultPath + ".tmp";
        File.WriteAllBytes(tmp, file);
        if (File.Exists(VaultPath))
            File.Replace(tmp, VaultPath, null);
        else
            File.Move(tmp, VaultPath);
    }

    // ---- Password-based wrappers kept for the create-vault path and tests ----

    public List<PasswordEntry> Load(string masterPassword)
    {
        byte[] salt = ReadSalt();
        byte[] key = CryptoService.DeriveKey(masterPassword, salt);
        try { return LoadWithKey(key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public void Save(IEnumerable<PasswordEntry> entries, string masterPassword)
    {
        byte[] salt = VaultExists() ? ReadSalt() : CryptoService.GenerateSalt();
        byte[] key = CryptoService.DeriveKey(masterPassword, salt);
        try { SaveWithKey(entries, key, salt); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
