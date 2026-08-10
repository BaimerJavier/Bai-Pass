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

    private readonly VaultStorage _storage;

    public string VaultPath => _storage.VaultPath;

    public VaultService() : this(ResolveDefaultPath()) { }

    /// <summary>Explicit-path constructor, used by tests to isolate the vault file.</summary>
    public VaultService(string vaultPath)
    {
        _storage = new VaultStorage(vaultPath);
    }

    private static string ResolveDefaultPath()
    {
#if DEBUG
        // Test-only override to isolate the vault file; compiled out of Release builds so it
        // cannot be used to redirect a user's vault in production.
        string? overridePath = Environment.GetEnvironmentVariable("BAIPASS_VAULT");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return overridePath;
#endif

        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PasswordManager");
        return Path.Combine(dir, "vault.dat");
    }

    public bool VaultExists() => _storage.Exists;

    /// <summary>Reads the vault's format/KDF descriptor (needed to derive the key at unlock).</summary>
    public VaultFormat ReadFormat() => CryptoService.ReadFormat(_storage.Read());

    /// <summary>Decrypts and deserializes the entries using an already-derived key.</summary>
    public List<PasswordEntry> LoadWithKey(ReadOnlySpan<byte> key)
    {
        byte[] file = _storage.Read();
        byte[] json = CryptoService.Decrypt(file, key);
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

    /// <summary>
    /// Serializes, encrypts with the given key+salt, and writes crash-safely: the temp file is
    /// verified to decrypt back before it atomically replaces the vault (old one kept as backup).
    /// </summary>
    public void SaveWithKey(IEnumerable<PasswordEntry> entries, ReadOnlySpan<byte> key, VaultFormat fmt)
    {
        string text = JsonSerializer.Serialize(entries, JsonOptions);
        byte[] plaintext = Encoding.UTF8.GetBytes(text);
        byte[] file;
        try
        {
            file = CryptoService.Encrypt(plaintext, key, fmt);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }

        // Short-lived key copy so the verify closure can re-decrypt the temp file; zeroed after.
        byte[] keyCopy = key.ToArray();
        try
        {
            _storage.WriteVerified(file, candidate =>
            {
                try
                {
                    byte[] rt = CryptoService.Decrypt(candidate, keyCopy);
                    CryptographicOperations.ZeroMemory(rt);
                    return true;
                }
                catch { return false; }
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyCopy);
        }
    }

    // ---- Password-based wrappers kept for the create-vault path and tests ----

    public List<PasswordEntry> Load(string masterPassword)
    {
        VaultFormat fmt = ReadFormat();
        byte[] key = CryptoService.DeriveKey(masterPassword, fmt);
        try { return LoadWithKey(key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public void Save(IEnumerable<PasswordEntry> entries, string masterPassword)
    {
        VaultFormat fmt = VaultExists() ? ReadFormat() : CryptoService.NewDefaultFormat();
        byte[] key = CryptoService.DeriveKey(masterPassword, fmt);
        try { SaveWithKey(entries, key, fmt); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
