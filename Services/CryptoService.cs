using System;
using System.IO;
using System.Security.Cryptography;

namespace PasswordManager.Services;

/// <summary>
/// Handles all cryptography for the vault.
///
/// Key derivation: PBKDF2 (HMAC-SHA256, 600k iterations) over the master password.
/// Encryption:     AES-256-GCM (authenticated) so tampering is detected on decrypt.
///
/// File layout (binary, format "PMV1"):
///   [4]  magic  "PMV1"
///   [16] salt   (for PBKDF2)
///   [12] nonce  (for AES-GCM)
///   [16] tag    (GCM authentication tag)
///   [..] ciphertext
///
/// The session derives the key once at unlock (<see cref="DeriveKey"/>) and reuses it — with a
/// fresh random nonce every save (<see cref="EncryptWithKey"/>) — so the master-password string
/// never has to be kept alive. The salt stored in the header is reused across saves so the same
/// key re-derives on the next unlock.
/// </summary>
public static class CryptoService
{
    private static readonly byte[] Magic = "PMV1"u8.ToArray();
    public const int SaltSize = 16;
    public const int NonceSize = 12; // AesGcm.NonceByteSizes.MaxSize
    public const int TagSize = 16;   // AesGcm.TagByteSizes.MaxSize
    public const int KeySize = 32;   // AES-256
    private const int Iterations = 600_000;

    private static readonly int HeaderLen = Magic.Length + SaltSize + NonceSize + TagSize;

    /// <summary>Generates a fresh random salt for a new vault.</summary>
    public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    /// <summary>Derives the 256-bit vault key from the master password and salt (PBKDF2).</summary>
    public static byte[] DeriveKey(ReadOnlySpan<char> password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

    /// <summary>Reads the salt from a vault file so the key can be derived before decrypting.</summary>
    public static byte[] ReadSalt(byte[] file)
    {
        ValidateHeader(file);
        return file.AsSpan(Magic.Length, SaltSize).ToArray();
    }

    /// <summary>
    /// Encrypts plaintext with an already-derived key, writing the given <paramref name="salt"/>
    /// into the header and generating a fresh random nonce. Returns the full file bytes.
    /// </summary>
    public static byte[] EncryptWithKey(byte[] plaintext, ReadOnlySpan<byte> key, byte[] salt)
    {
        if (key.Length != KeySize) throw new ArgumentException("Key must be 256-bit.", nameof(key));
        if (salt.Length != SaltSize) throw new ArgumentException("Bad salt size.", nameof(salt));

        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];

        using (var aes = new AesGcm(key, TagSize))
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

        using var ms = new MemoryStream(HeaderLen + ciphertext.Length);
        ms.Write(Magic);
        ms.Write(salt);
        ms.Write(nonce);
        ms.Write(tag);
        ms.Write(ciphertext);
        return ms.ToArray();
    }

    /// <summary>
    /// Decrypts a vault file with an already-derived key. Throws
    /// <see cref="CryptographicException"/> when the key is wrong or the data was tampered with
    /// (GCM tag mismatch); no plaintext is produced on authentication failure.
    /// </summary>
    public static byte[] DecryptWithKey(byte[] file, ReadOnlySpan<byte> key)
    {
        ValidateHeader(file);
        if (key.Length != KeySize) throw new ArgumentException("Key must be 256-bit.", nameof(key));

        var span = file.AsSpan();
        int offset = Magic.Length + SaltSize;
        byte[] nonce = span.Slice(offset, NonceSize).ToArray(); offset += NonceSize;
        byte[] tag = span.Slice(offset, TagSize).ToArray(); offset += TagSize;
        byte[] ciphertext = span.Slice(offset).ToArray();

        byte[] plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext); // throws on wrong key / tamper
        return plaintext;
    }

    // ---- Password-based convenience wrappers (used when creating a vault and in tests) ----

    /// <summary>Encrypts with a fresh salt derived from the password. Returns full file bytes.</summary>
    public static byte[] Encrypt(byte[] plaintext, string password)
    {
        byte[] salt = GenerateSalt();
        byte[] key = DeriveKey(password, salt);
        try { return EncryptWithKey(plaintext, key, salt); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    /// <summary>Derives the key from the file's salt and password, then decrypts.</summary>
    public static byte[] Decrypt(byte[] file, string password)
    {
        byte[] salt = ReadSalt(file);
        byte[] key = DeriveKey(password, salt);
        try { return DecryptWithKey(file, key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static void ValidateHeader(byte[] file)
    {
        if (file.Length < HeaderLen)
            throw new InvalidDataException("El archivo del vault esta corrupto o incompleto.");
        if (!file.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("Formato de archivo no reconocido.");
    }
}
