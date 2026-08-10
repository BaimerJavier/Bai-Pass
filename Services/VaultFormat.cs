namespace PasswordManager.Services;

/// <summary>Key-derivation function a vault was sealed with.</summary>
public enum VaultKdf : byte
{
    Pbkdf2Sha256 = 1, // legacy "PMV1"
    Argon2id = 2,     // "PMV2"
}

/// <summary>
/// Self-describing description of how a vault is sealed: which KDF and its parameters, plus the
/// salt. Stored in the file header (PMV2) so parameters can evolve without breaking old vaults.
/// The key derived from a format is reused for the session; a fresh nonce is used per save.
/// </summary>
public sealed class VaultFormat
{
    public required VaultKdf Kdf { get; init; }
    public required byte[] Salt { get; init; }

    /// <summary>PBKDF2 iteration count, or Argon2 pass count.</summary>
    public int Iterations { get; init; }

    /// <summary>Argon2 memory cost in KiB (ignored for PBKDF2).</summary>
    public int MemoryKiB { get; init; }

    /// <summary>Argon2 degree of parallelism (ignored for PBKDF2).</summary>
    public int Parallelism { get; init; }

    public bool IsLegacy => Kdf == VaultKdf.Pbkdf2Sha256;
}
