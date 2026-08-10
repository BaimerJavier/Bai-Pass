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
/// File layout (binary):
///   [4]  magic  "PMV1"
///   [16] salt   (for PBKDF2)
///   [12] nonce  (for AES-GCM)
///   [16] tag    (GCM authentication tag)
///   [..] ciphertext
/// </summary>
public static class CryptoService
{
    private static readonly byte[] Magic = "PMV1"u8.ToArray();
    private const int SaltSize = 16;
    private const int NonceSize = 12; // AesGcm.NonceByteSizes.MaxSize
    private const int TagSize = 16;   // AesGcm.TagByteSizes.MaxSize
    private const int KeySize = 32;   // AES-256
    private const int Iterations = 600_000;

    /// <summary>Encrypts UTF-8 plaintext with a key derived from the password. Returns the full file bytes.</summary>
    public static byte[] Encrypt(byte[] plaintext, string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] key = DeriveKey(password, salt);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        using var ms = new MemoryStream();
        ms.Write(Magic);
        ms.Write(salt);
        ms.Write(nonce);
        ms.Write(tag);
        ms.Write(ciphertext);
        return ms.ToArray();
    }

    /// <summary>
    /// Decrypts a vault file. Throws <see cref="CryptographicException"/> when the password is
    /// wrong or the data was tampered with (GCM tag mismatch).
    /// </summary>
    public static byte[] Decrypt(byte[] file, string password)
    {
        int minLen = Magic.Length + SaltSize + NonceSize + TagSize;
        if (file.Length < minLen)
            throw new InvalidDataException("El archivo del vault esta corrupto o incompleto.");

        var span = file.AsSpan();
        int offset = 0;

        if (!span.Slice(0, Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("Formato de archivo no reconocido.");
        offset += Magic.Length;

        byte[] salt = span.Slice(offset, SaltSize).ToArray(); offset += SaltSize;
        byte[] nonce = span.Slice(offset, NonceSize).ToArray(); offset += NonceSize;
        byte[] tag = span.Slice(offset, TagSize).ToArray(); offset += TagSize;
        byte[] ciphertext = span.Slice(offset).ToArray();

        byte[] key = DeriveKey(password, salt);
        byte[] plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext); // throws on wrong password / tamper
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
        return plaintext;
    }

    private static byte[] DeriveKey(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
}
