using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using PasswordManager.Models;
using PasswordManager.Services;
using Xunit;

namespace BaiPass.Tests;

public class MigrationTests : IDisposable
{
    private readonly string _dir;
    private readonly string _vaultPath;

    public MigrationTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "baipass-migration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _vaultPath = Path.Combine(_dir, "vault.dat");
    }

    /// <summary>Writes a legacy PMV1 (PBKDF2) vault directly, as the old app would have.</summary>
    private void WriteLegacyPmv1(string password, List<PasswordEntry> entries)
    {
        // PMV1 headers do not persist the iteration count; the reader assumes the historical 600k,
        // so the file must be derived with 600k to stay readable.
        var pmv1 = new VaultFormat
        {
            Kdf = VaultKdf.Pbkdf2Sha256,
            Salt = CryptoService.GenerateSalt(),
            Iterations = 600_000,
        };

        byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entries));
        byte[] key = CryptoService.DeriveKey(password, pmv1);
        byte[] file = CryptoService.Encrypt(json, key, pmv1);
        File.WriteAllBytes(_vaultPath, file);
    }

    [Fact]
    public void Legacy_PMV1_vault_reads_as_pbkdf2()
    {
        WriteLegacyPmv1("clave", new List<PasswordEntry> { new() { Title = "Old" } });
        Assert.Equal("PMV1", Encoding.ASCII.GetString(File.ReadAllBytes(_vaultPath), 0, 4));
        Assert.Equal(VaultKdf.Pbkdf2Sha256, new VaultService(_vaultPath).ReadFormat().Kdf);
    }

    [Fact]
    public void Unlocking_PMV1_migrates_to_PMV2_preserving_entries()
    {
        var original = new List<PasswordEntry>
        {
            new() { Title = "GitHub", Username = "me", Password = "p@ss", Folder = "Dev" },
            new() { Title = "Banco",  Username = "acct", Password = "unicodé-🔐", Notes = "ñ áéíóú" },
        };
        WriteLegacyPmv1("MiClave", original);

        // Unlock migrates in place.
        using (var s = new SessionManager(new VaultService(_vaultPath)))
        {
            s.Unlock("MiClave");
            Assert.True(s.MigratedOnUnlock);
            Assert.Equal(2, s.Entries.Count);
        }

        // File is now PMV2 (Argon2id) and still opens with the same password.
        Assert.Equal("PMV2", Encoding.ASCII.GetString(File.ReadAllBytes(_vaultPath), 0, 4));
        using var s2 = new SessionManager(new VaultService(_vaultPath));
        s2.Unlock("MiClave");
        Assert.False(s2.MigratedOnUnlock); // already PMV2, no second migration
        Assert.Equal(2, s2.Entries.Count);
        Assert.Equal("GitHub", s2.Entries[0].Title);
        Assert.Equal("unicodé-🔐", s2.Entries[1].Password);
    }

    [Fact]
    public void Migration_keeps_the_original_as_backup()
    {
        WriteLegacyPmv1("clave", new List<PasswordEntry> { new() { Title = "keep" } });

        using var s = new SessionManager(new VaultService(_vaultPath));
        s.Unlock("clave");

        // The pre-migration PMV1 file is preserved as the backup (still decryptable).
        string backup = _vaultPath + ".backup";
        Assert.True(File.Exists(backup));
        Assert.Equal("PMV1", Encoding.ASCII.GetString(File.ReadAllBytes(backup), 0, 4));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
