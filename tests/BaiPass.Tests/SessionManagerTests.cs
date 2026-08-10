using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using PasswordManager.Models;
using PasswordManager.Services;
using Xunit;

namespace BaiPass.Tests;

public class SessionManagerTests : IDisposable
{
    private readonly string _dir;
    private readonly string _vaultPath;

    public SessionManagerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "baipass-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _vaultPath = Path.Combine(_dir, "vault.dat");
    }

    private SessionManager NewSession() => new(new VaultService(_vaultPath));

    [Fact]
    public void CreateNew_then_Save_and_Unlock_roundtrips()
    {
        using (var s = NewSession())
        {
            Assert.False(s.VaultExists);
            s.CreateNew("MiClaveMaestra");
            Assert.True(s.IsUnlocked);
            s.Entries.Add(new PasswordEntry { Title = "GitHub", Password = "p@ss", Folder = "Dev" });
            s.Save();
        }

        using var s2 = NewSession();
        Assert.True(s2.VaultExists);
        s2.Unlock("MiClaveMaestra");
        Assert.Single(s2.Entries);
        Assert.Equal("GitHub", s2.Entries[0].Title);
        Assert.Equal("Dev", s2.Entries[0].Folder);
    }

    [Fact]
    public void Unlock_with_wrong_password_throws_and_stays_locked()
    {
        using (var s = NewSession()) { s.CreateNew("correcta"); s.Save(); }

        using var s2 = NewSession();
        Assert.ThrowsAny<Exception>(() => s2.Unlock("incorrecta"));
        Assert.False(s2.IsUnlocked);
    }

    [Fact]
    public void Lock_clears_entries_and_blocks_further_saves()
    {
        using var s = NewSession();
        s.CreateNew("clave");
        s.Entries.Add(new PasswordEntry { Title = "Bank", Password = "secret" });
        s.Save();

        s.Lock();

        Assert.False(s.IsUnlocked);
        Assert.Empty(s.Entries);
        Assert.Throws<InvalidOperationException>(() => s.Save());
    }

    [Fact]
    public void Lock_raises_Locked_event()
    {
        using var s = NewSession();
        s.CreateNew("clave");
        bool raised = false;
        s.Locked += () => raised = true;
        s.Lock();
        Assert.True(raised);
    }

    [Fact]
    public void Inactivity_triggers_AutoLockRequested()
    {
        using var s = NewSession();
        s.AutoLockTimeout = TimeSpan.FromMilliseconds(150);
        var signaled = new ManualResetEventSlim(false);
        s.AutoLockRequested += () => signaled.Set();

        s.CreateNew("clave"); // starts the idle timer

        Assert.True(signaled.Wait(TimeSpan.FromSeconds(5)), "AutoLockRequested no se disparo");
    }

    [Fact]
    public void RegisterActivity_postpones_auto_lock()
    {
        using var s = NewSession();
        s.AutoLockTimeout = TimeSpan.FromMilliseconds(400);
        int count = 0;
        s.AutoLockRequested += () => Interlocked.Increment(ref count);
        s.CreateNew("clave");

        // Keep active for ~1s; auto-lock must not fire while we register activity.
        for (int i = 0; i < 8; i++) { s.RegisterActivity(); Thread.Sleep(100); }
        Assert.Equal(0, count);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }
}
