using System;

namespace PasswordManager.Services;

/// <summary>
/// Watches for OS events that should force the vault to lock (screen lock, session
/// switch, suspend). Cross-platform abstraction; no-op where unsupported.
/// </summary>
public interface ISystemLockMonitor : IDisposable
{
    /// <summary>Raised (possibly off the UI thread) when an OS event warrants locking.</summary>
    event Action? LockRequested;

    void Start();
    void Stop();
}
