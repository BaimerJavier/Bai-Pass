using System;
using System.Security.Cryptography;
using System.Text;
using PasswordManager.Services;
using Xunit;

namespace BaiPass.Tests;

public class FuzzAndCorruptionTests
{
    private static byte[] ValidVault() =>
        CryptoService.Encrypt(Encoding.UTF8.GetBytes("[]"), "clave");

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]      // magic only
    [InlineData(28)]     // partial PMV2 header
    [InlineData(200)]
    public void Random_bytes_of_any_size_never_crash_and_never_decrypt(int size)
    {
        for (int i = 0; i < 50; i++)
        {
            byte[] junk = RandomNumberGenerator.GetBytes(size);
            // Must throw a controlled exception, never return plaintext, never hang.
            Assert.ThrowsAny<Exception>(() => CryptoService.Decrypt(junk, "clave"));
        }
    }

    [Fact]
    public void Truncated_valid_vault_fails_cleanly()
    {
        byte[] full = ValidVault();
        for (int len = 0; len < full.Length; len += 3)
        {
            byte[] truncated = full[..len];
            Assert.ThrowsAny<Exception>(() => CryptoService.Decrypt(truncated, "clave"));
        }
    }

    [Fact]
    public void Crafted_PMV2_header_with_absurd_memory_is_rejected_not_allocated()
    {
        // Build a well-formed PMV2 prefix but with a huge Argon2 memory cost.
        byte[] file = new byte[4 + 4 + 4 + 1 + 16 + 12 + 16 + 4];
        Encoding.ASCII.GetBytes("PMV2").CopyTo(file, 0);
        // memoryKiB = int.MaxValue (would be ~2 TiB) -> must be rejected by ReadFormat.
        BitConverter.GetBytes(int.MaxValue).CopyTo(file, 4);
        BitConverter.GetBytes(3).CopyTo(file, 8);
        file[12] = 4;

        Assert.Throws<System.IO.InvalidDataException>(() => CryptoService.ReadFormat(file));
        Assert.Throws<System.IO.InvalidDataException>(() => CryptoService.Decrypt(file, "clave"));
    }

    [Fact]
    public void Crafted_PMV2_header_with_zero_parallelism_is_rejected()
    {
        byte[] file = new byte[4 + 4 + 4 + 1 + 16 + 12 + 16];
        Encoding.ASCII.GetBytes("PMV2").CopyTo(file, 0);
        BitConverter.GetBytes(65536).CopyTo(file, 4);
        BitConverter.GetBytes(3).CopyTo(file, 8);
        file[12] = 0; // parallelism 0 -> invalid
        Assert.Throws<System.IO.InvalidDataException>(() => CryptoService.ReadFormat(file));
    }

    [Fact]
    public void Flipping_a_single_byte_of_a_valid_vault_fails_authentication()
    {
        byte[] plain = Encoding.UTF8.GetBytes("[{\"Title\":\"x\",\"Password\":\"y\"}]");
        byte[] good = CryptoService.Encrypt(plain, "clave");

        for (int i = 0; i < good.Length; i++)
        {
            byte[] bad = (byte[])good.Clone();
            bad[i] ^= 0xFF;
            // Either rejected as bad format (magic) or fails GCM auth; never returns plaintext.
            Assert.ThrowsAny<Exception>(() => CryptoService.Decrypt(bad, "clave"));
        }
    }
}
