using System;
using System.IO;
using System.Text;
using PasswordManager.Models;
using PasswordManager.Services;
using Xunit;

namespace BaiPass.Tests;

public class VaultStorageTests : IDisposable
{
    private readonly string _dir;
    private readonly string _vaultPath;

    public VaultStorageTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "baipass-storage", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _vaultPath = Path.Combine(_dir, "vault.dat");
    }

    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void First_write_creates_vault_no_backup()
    {
        var storage = new VaultStorage(_vaultPath);
        storage.WriteVerified(B("A"), _ => true);

        Assert.True(storage.Exists);
        Assert.False(storage.BackupExists);
        Assert.Equal("A", Encoding.UTF8.GetString(storage.Read()));
    }

    [Fact]
    public void Second_write_moves_previous_to_backup()
    {
        var storage = new VaultStorage(_vaultPath);
        storage.WriteVerified(B("v1"), _ => true);
        storage.WriteVerified(B("v2"), _ => true);

        Assert.Equal("v2", Encoding.UTF8.GetString(storage.Read()));
        Assert.True(storage.BackupExists);
        Assert.Equal("v1", Encoding.UTF8.GetString(storage.ReadBackup()));
    }

    [Fact]
    public void Failed_verification_leaves_existing_vault_untouched()
    {
        var storage = new VaultStorage(_vaultPath);
        storage.WriteVerified(B("good"), _ => true);

        Assert.Throws<IOException>(() => storage.WriteVerified(B("bad"), _ => false));

        // Original content preserved; no partial write.
        Assert.Equal("good", Encoding.UTF8.GetString(storage.Read()));
    }

    [Fact]
    public void VaultService_keeps_previous_valid_vault_as_encrypted_backup()
    {
        var vault = new VaultService(_vaultPath);
        vault.Save(new List<PasswordEntry> { new() { Title = "one" } }, "clave");
        vault.Save(new List<PasswordEntry> { new() { Title = "two" } }, "clave");

        // Backup exists and still decrypts to the previous state.
        var storage = new VaultStorage(_vaultPath);
        Assert.True(storage.BackupExists);

        byte[] backupPlain = CryptoService.Decrypt(storage.ReadBackup(), "clave");
        Assert.Contains("one", Encoding.UTF8.GetString(backupPlain));

        byte[] currentPlain = CryptoService.Decrypt(storage.Read(), "clave");
        Assert.Contains("two", Encoding.UTF8.GetString(currentPlain));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
