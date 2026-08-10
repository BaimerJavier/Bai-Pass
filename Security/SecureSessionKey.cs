using System;
using System.Security.Cryptography;

namespace PasswordManager.Security;

/// <summary>
/// Holds the derived 256-bit vault key for the duration of an unlocked session.
///
/// The key lives in a plain <c>byte[]</c> (not a <see cref="string"/>) so it can be wiped
/// with <see cref="CryptographicOperations.ZeroMemory(System.Span{byte})"/> on lock. Callers
/// read it as a <see cref="ReadOnlySpan{Byte}"/> and must never copy it into a longer-lived
/// buffer. Once disposed the key is zeroed and further access throws.
///
/// Limitation (documented, not hidden): while unlocked the key bytes are managed memory and
/// can still be captured by a privileged memory dump. This class only guarantees the key is
/// wiped promptly on lock — it is not a defense against a compromised OS.
/// </summary>
public sealed class SecureSessionKey : IDisposable
{
    private byte[]? _key;

    /// <summary>Takes ownership of <paramref name="key"/>; the caller must not reuse it.</summary>
    public SecureSessionKey(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _key = key;
    }

    public bool IsDisposed => _key is null;

    /// <summary>The raw key bytes. Do not copy into a longer-lived buffer.</summary>
    public ReadOnlySpan<byte> Key =>
        _key ?? throw new ObjectDisposedException(nameof(SecureSessionKey));

    public void Dispose()
    {
        if (_key is not null)
        {
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }
    }
}
