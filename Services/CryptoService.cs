using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

[assembly: InternalsVisibleTo("BaiPass.Tests")]

namespace PasswordManager.Services;

/// <summary>
/// All vault cryptography. Two on-disk formats are supported:
///
///   PMV1 (legacy, read + write): PBKDF2-HMAC-SHA256 (600k)
///     [4]"PMV1" [16]salt [12]nonce [16]tag [..]ciphertext
///
///   PMV2 (current, Argon2id): parameters are stored so they can evolve
///     [4]"PMV2" [4]memoryKiB(LE) [4]iterations(LE) [1]parallelism [16]salt [12]nonce [16]tag [..]ct
///
/// Encryption is AES-256-GCM in both. The session derives the key once (<see cref="DeriveKey"/>)
/// and reuses it with a fresh random nonce per save (<see cref="Encrypt"/>).
/// </summary>
public static class CryptoService
{
    private static readonly byte[] MagicV1 = "PMV1"u8.ToArray();
    private static readonly byte[] MagicV2 = "PMV2"u8.ToArray();

    public const int SaltSize = 16;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int KeySize = 32;
    private const int LegacyPbkdf2Iterations = 600_000;

    // Argon2id defaults for new vaults. Internal so tests can dial them down for speed;
    // production values target a few hundred ms of unlock time on modern hardware.
    internal static int Argon2MemoryKiB = 65_536; // 64 MiB
    internal static int Argon2Iterations = 3;
    internal static int Argon2Parallelism = 4;

    public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    /// <summary>The format used for brand-new vaults (Argon2id with a fresh salt).</summary>
    public static VaultFormat NewDefaultFormat() => new()
    {
        Kdf = VaultKdf.Argon2id,
        Salt = GenerateSalt(),
        MemoryKiB = Argon2MemoryKiB,
        Iterations = Argon2Iterations,
        Parallelism = Argon2Parallelism,
    };

    /// <summary>Parses the header to learn how a vault file is sealed.</summary>
    public static VaultFormat ReadFormat(byte[] file)
    {
        if (file.Length >= MagicV2.Length && file.AsSpan(0, MagicV2.Length).SequenceEqual(MagicV2))
        {
            if (file.Length < V2PrefixLen) throw new InvalidDataException("Vault PMV2 incompleto.");
            int o = MagicV2.Length;
            int mem = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(o, 4)); o += 4;
            int iters = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(o, 4)); o += 4;
            int par = file[o]; o += 1;
            byte[] salt = file.AsSpan(o, SaltSize).ToArray();
            return new VaultFormat
            {
                Kdf = VaultKdf.Argon2id, Salt = salt,
                MemoryKiB = mem, Iterations = iters, Parallelism = par,
            };
        }
        if (file.Length >= MagicV1.Length && file.AsSpan(0, MagicV1.Length).SequenceEqual(MagicV1))
        {
            if (file.Length < V1PrefixLen + NonceSize + TagSize)
                throw new InvalidDataException("Vault PMV1 incompleto.");
            byte[] salt = file.AsSpan(MagicV1.Length, SaltSize).ToArray();
            return new VaultFormat
            {
                Kdf = VaultKdf.Pbkdf2Sha256, Salt = salt, Iterations = LegacyPbkdf2Iterations,
            };
        }
        throw new InvalidDataException("Formato de archivo no reconocido.");
    }

    /// <summary>Derives the 256-bit vault key from the password using the format's KDF.</summary>
    public static byte[] DeriveKey(ReadOnlySpan<char> password, VaultFormat fmt)
    {
        if (fmt.Kdf == VaultKdf.Pbkdf2Sha256)
            return Rfc2898DeriveBytes.Pbkdf2(password, fmt.Salt, fmt.Iterations, HashAlgorithmName.SHA256, KeySize);

        // Argon2id: hash the UTF-8 password bytes; wipe them afterwards.
        byte[] pwd = Encoding.UTF8.GetBytes(password.ToArray());
        try
        {
            using var argon = new Argon2id(pwd)
            {
                Salt = fmt.Salt,
                DegreeOfParallelism = fmt.Parallelism,
                Iterations = fmt.Iterations,
                MemorySize = fmt.MemoryKiB,
            };
            return argon.GetBytes(KeySize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pwd);
        }
    }

    /// <summary>Encrypts plaintext with an already-derived key, writing the given format's header.</summary>
    public static byte[] Encrypt(byte[] plaintext, ReadOnlySpan<byte> key, VaultFormat fmt)
    {
        if (key.Length != KeySize) throw new ArgumentException("Key must be 256-bit.", nameof(key));
        if (fmt.Salt.Length != SaltSize) throw new ArgumentException("Bad salt size.");

        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];
        using (var aes = new AesGcm(key, TagSize))
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

        using var ms = new MemoryStream();
        if (fmt.Kdf == VaultKdf.Argon2id)
        {
            ms.Write(MagicV2);
            Span<byte> hdr = stackalloc byte[9];
            BinaryPrimitives.WriteInt32LittleEndian(hdr[..4], fmt.MemoryKiB);
            BinaryPrimitives.WriteInt32LittleEndian(hdr.Slice(4, 4), fmt.Iterations);
            hdr[8] = (byte)fmt.Parallelism;
            ms.Write(hdr);
        }
        else
        {
            ms.Write(MagicV1);
        }
        ms.Write(fmt.Salt);
        ms.Write(nonce);
        ms.Write(tag);
        ms.Write(ciphertext);
        return ms.ToArray();
    }

    /// <summary>
    /// Decrypts a vault file with an already-derived key. Throws
    /// <see cref="CryptographicException"/> on wrong key / tamper; no plaintext on auth failure.
    /// </summary>
    public static byte[] Decrypt(byte[] file, ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize) throw new ArgumentException("Key must be 256-bit.", nameof(key));
        int prefix = PrefixLen(file);

        var span = file.AsSpan();
        int offset = prefix;
        byte[] nonce = span.Slice(offset, NonceSize).ToArray(); offset += NonceSize;
        byte[] tag = span.Slice(offset, TagSize).ToArray(); offset += TagSize;
        byte[] ciphertext = span.Slice(offset).ToArray();

        byte[] plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext); // throws on wrong key / tamper
        return plaintext;
    }

    // ---- Password-based convenience wrappers (create-vault path and tests) ----

    /// <summary>Encrypts with a fresh Argon2id (PMV2) format derived from the password.</summary>
    public static byte[] Encrypt(byte[] plaintext, string password)
    {
        VaultFormat fmt = NewDefaultFormat();
        byte[] key = DeriveKey(password, fmt);
        try { return Encrypt(plaintext, key, fmt); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    /// <summary>Reads the format, derives the key, and decrypts (works for PMV1 and PMV2).</summary>
    public static byte[] Decrypt(byte[] file, string password)
    {
        VaultFormat fmt = ReadFormat(file);
        byte[] key = DeriveKey(password, fmt);
        try { return Decrypt(file, key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static readonly int V1PrefixLen = MagicV1.Length + SaltSize;            // 20
    private static readonly int V2PrefixLen = MagicV2.Length + 9 + SaltSize;        // 29

    private static int PrefixLen(byte[] file)
    {
        if (file.Length >= MagicV2.Length && file.AsSpan(0, MagicV2.Length).SequenceEqual(MagicV2))
            return V2PrefixLen;
        if (file.Length >= MagicV1.Length && file.AsSpan(0, MagicV1.Length).SequenceEqual(MagicV1))
            return V1PrefixLen;
        throw new InvalidDataException("Formato de archivo no reconocido.");
    }
}
