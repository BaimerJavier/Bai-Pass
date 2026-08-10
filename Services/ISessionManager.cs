using System;
using System.Collections.Generic;
using PasswordManager.Models;

namespace PasswordManager.Services;

/// <summary>
/// Owns the security-sensitive session state: the derived vault key, the decrypted entries,
/// persistence, and the inactivity lifecycle. Keeps that responsibility out of the ViewModels.
/// </summary>
public interface ISessionManager
{
    bool IsUnlocked { get; }

    /// <summary>Whether a vault file already exists (create vs. unlock flow).</summary>
    bool VaultExists { get; }

    /// <summary>The decrypted entries. Empty and not meaningful while locked.</summary>
    IList<PasswordEntry> Entries { get; }

    /// <summary>Idle time before the session auto-locks.</summary>
    TimeSpan AutoLockTimeout { get; set; }

    /// <summary>Raised (on the UI thread) after the session is locked and state is cleared.</summary>
    event Action? Locked;

    /// <summary>Raised (possibly off the UI thread) when the inactivity timeout elapses.</summary>
    event Action? AutoLockRequested;

    /// <summary>Creates a brand-new vault with the given master password.</summary>
    void CreateNew(string masterPassword);

    /// <summary>Unlocks an existing vault. Throws on a wrong password / tampered file.</summary>
    void Unlock(string masterPassword);

    /// <summary>Encrypts and persists the current entries with the session key.</summary>
    void Save();

    /// <summary>Records user activity, resetting the inactivity timer.</summary>
    void RegisterActivity();

    /// <summary>Locks the session: zeroizes the key, clears entries, raises <see cref="Locked"/>.</summary>
    void Lock();
}
