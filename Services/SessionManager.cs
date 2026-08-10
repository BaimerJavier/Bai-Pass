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
    private byte[]? _salt;
    private List<PasswordEntry> _entries = new();

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
        byte[] salt = CryptoService.GenerateSalt();
        byte[] key = CryptoService.DeriveKey(masterPassword, salt);
        _vault.SaveWithKey(new List<PasswordEntry>(), key, salt);
        BeginSession(key, salt, new List<PasswordEntry>());
    }

    public void Unlock(string masterPassword)
    {
        byte[] salt = _vault.ReadSalt();
        byte[] key = CryptoService.DeriveKey(masterPassword, salt);
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
        BeginSession(key, salt, entries);
    }

    private void BeginSession(byte[] key, byte[] salt, List<PasswordEntry> entries)
    {
        _key = new SecureSessionKey(key);
        _salt = salt;
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
            if (_key is null || _key.IsDisposed || _salt is null)
                throw new InvalidOperationException("La sesion esta bloqueada.");
            _vault.SaveWithKey(_entries, _key.Key, _salt);
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

            if (_salt is not null)
            {
                CryptographicOperations.ZeroMemory(_salt);
                _salt = null;
            }

            _entries.Clear();              // drop references to decrypted entries
            _entries = new List<PasswordEntry>();
        }

        Locked?.Invoke();
    }

    public void Dispose()
    {
        _idleTimer.Dispose();
        _key?.Dispose();
        if (_salt is not null) CryptographicOperations.ZeroMemory(_salt);
    }
}
