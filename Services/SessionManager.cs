using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using PasswordManager.Models;
using PasswordManager.Security;

namespace PasswordManager.Services;

/// <summary>
/// Central owner of the unlocked-session lifecycle. See <see cref="ISessionManager"/>.
///
/// Key lifecycle:
///   Unlock  → derive key (PBKDF2 over stored salt) → hold SecureSessionKey → drop password
///   Running → key + decrypted entries in memory; every save reuses key with a fresh nonce
///   Lock    → zeroize key, clear entries, stop inactivity timer
/// </summary>
public sealed class SessionManager : ISessionManager, IDisposable
{
    private readonly VaultService _vault;
    private readonly object _gate = new();

    private SecureSessionKey? _key;
    private VaultFormat? _format;
    private List<PasswordEntry> _entries = new();

    /// <summary>True when the last unlock upgraded a legacy PMV1 vault to PMV2 (Argon2id).</summary>
    public bool MigratedOnUnlock { get; private set; }

    private readonly Timer _idleTimer;
    private long _lastActivityTicks;
    private bool _autoLockRaised;

    public SessionManager(VaultService vault)
    {
        _vault = vault;
        // Threadpool timer (no UI-thread dependency) — checks idle time periodically.
        _idleTimer = new Timer(CheckIdle, null, Timeout.Infinite, Timeout.Infinite);
    }

    public bool IsUnlocked => _key is { IsDisposed: false };
    public bool VaultExists => _vault.VaultExists();
    public IList<PasswordEntry> Entries => _entries;

    public TimeSpan AutoLockTimeout { get; set; } = TimeSpan.FromMinutes(10);

    public event Action? Locked;
    public event Action? AutoLockRequested;

    public void CreateNew(string masterPassword)
    {
        MigratedOnUnlock = false;
        VaultFormat fmt = CryptoService.NewDefaultFormat();
        byte[] key = CryptoService.DeriveKey(masterPassword, fmt);
        _vault.SaveWithKey(new List<PasswordEntry>(), key, fmt);
        BeginSession(key, fmt, new List<PasswordEntry>());
    }

    public void Unlock(string masterPassword)
    {
        MigratedOnUnlock = false;
        VaultFormat fmt = _vault.ReadFormat();
        byte[] key = CryptoService.DeriveKey(masterPassword, fmt);
        List<PasswordEntry> entries;
        try
        {
            entries = _vault.LoadWithKey(key); // throws on wrong password / tamper
        }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            throw;
        }

        // Seamlessly upgrade legacy PMV1 (PBKDF2) vaults to PMV2 (Argon2id). The write is
        // verified and keeps a backup, so a failure leaves the original PMV1 vault intact.
        if (fmt.IsLegacy)
        {
            try
            {
                VaultFormat newFmt = CryptoService.NewDefaultFormat();
                byte[] newKey = CryptoService.DeriveKey(masterPassword, newFmt);
                _vault.SaveWithKey(entries, newKey, newFmt);
                CryptographicOperations.ZeroMemory(key);
                key = newKey;
                fmt = newFmt;
                MigratedOnUnlock = true;
            }
            catch
            {
                // Keep the working PMV1 session; migration can retry on the next save.
            }
        }

        BeginSession(key, fmt, entries);
    }

    private void BeginSession(byte[] key, VaultFormat fmt, List<PasswordEntry> entries)
    {
        _key = new SecureSessionKey(key);
        _format = fmt;
        _entries = entries;
        _autoLockRaised = false;
        RegisterActivity();
        // Poll every 20s (or a quarter of the timeout, whichever is smaller; min 1s).
        var period = TimeSpan.FromSeconds(Math.Max(1, Math.Min(20, AutoLockTimeout.TotalSeconds / 4)));
        _idleTimer.Change(period, period);
    }

    public void Save()
    {
        lock (_gate)
        {
            if (_key is null || _key.IsDisposed || _format is null)
                throw new InvalidOperationException("La sesion esta bloqueada.");
            _vault.SaveWithKey(_entries, _key.Key, _format);
        }
    }

    public void RegisterActivity() =>
        Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);

    private void CheckIdle(object? state)
    {
        if (!IsUnlocked || _autoLockRaised) return;

        long last = Interlocked.Read(ref _lastActivityTicks);
        var idle = DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc);
        if (idle >= AutoLockTimeout)
        {
            _autoLockRaised = true;
            _idleTimer.Change(Timeout.Infinite, Timeout.Infinite);
            AutoLockRequested?.Invoke();
        }
    }

    public void Lock()
    {
        lock (_gate)
        {
            _idleTimer.Change(Timeout.Infinite, Timeout.Infinite);

            _key?.Dispose();               // zeroizes the 256-bit key
            _key = null;
            _format = null;                // salt is not secret, but drop the reference

            _entries.Clear();              // drop references to decrypted entries
            _entries = new List<PasswordEntry>();
        }

        Locked?.Invoke();
    }

    public void Dispose()
    {
        _idleTimer.Dispose();
        _key?.Dispose();
    }
}
