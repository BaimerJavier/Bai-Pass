using System;
using System.IO;
using System.Text;
using PasswordManager.Models;
using PasswordManager.Services;
using Xunit;

namespace BaiPass.Tests;

/// <summary>
/// A crash while saving must never destroy the last valid vault. These tests exercise the
/// failure points around the atomic write (verification, exception mid-write).
/// </summary>
public class CrashDuringSaveTests : IDisposable
{
    private readonly string _dir;
    private readonly string _vaultPath;

    public CrashDuringSaveTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "baipass-crash", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _vaultPath = Path.Combine(_dir, "vault.dat");
    }

    [Fact]
    public void Exception_during_write_leaves_previous_vault_intact()
    {
        var storage = new VaultStorage(_vaultPath);
        storage.WriteVerified(Encoding.UTF8.GetBytes("valid-1"), _ => true);

        // Verify callback throws (simulates a crash mid-save, after temp write).
        Assert.ThrowsAny<Exception>(() =>
            storage.WriteVerified(Encoding.UTF8.GetBytes("valid-2"),
                _ => throw new InvalidOperationException("boom")));

        Assert.Equal("valid-1", Encoding.UTF8.GetString(storage.Read()));
    }

    [Fact]
    public void Leftover_temp_file_does_not_corrupt_the_vault()
    {
        var vault = new VaultService(_vaultPath);
        vault.Save(new List<PasswordEntry> { new() { Title = "real" } }, "clave");

        // Simulate a crash that left a garbage temp file behind.
        File.WriteAllBytes(_vaultPath + ".tmp", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        // Vault still opens correctly; the next save overwrites the temp safely.
        var reopened = new VaultService(_vaultPath).Load("clave");
        Assert.Single(reopened);
        vault.Save(new List<PasswordEntry> { new() { Title = "real2" } }, "clave");
        Assert.Single(new VaultService(_vaultPath).Load("clave"));
    }

    [Fact]
    public void A_failed_save_keeps_the_vault_decryptable_with_old_content()
    {
        var vault = new VaultService(_vaultPath);
        vault.Save(new List<PasswordEntry> { new() { Title = "keepme" } }, "clave");

        // Corrupt the ability to write by making the temp path a directory (write fails).
        Directory.CreateDirectory(_vaultPath + ".tmp");
        try
        {
            Assert.ThrowsAny<Exception>(() =>
                vault.Save(new List<PasswordEntry> { new() { Title = "newer" } }, "clave"));
        }
        finally
        {
            Directory.Delete(_vaultPath + ".tmp");
        }

        var entries = new VaultService(_vaultPath).Load("clave");
        Assert.Single(entries);
        Assert.Equal("keepme", entries[0].Title);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
