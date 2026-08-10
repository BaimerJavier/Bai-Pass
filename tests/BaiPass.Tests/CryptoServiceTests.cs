using System.Security.Cryptography;
using System.Text;
using PasswordManager.Services;
using Xunit;

namespace BaiPass.Tests;

public class CryptoServiceTests
{
    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Encrypt_then_Decrypt_roundtrips_with_correct_password()
    {
        byte[] plain = Utf8("[{\"Title\":\"Gmail\",\"Password\":\"s3cr3t\"}]");
        byte[] file = CryptoService.Encrypt(plain, "MasterClave123");

        byte[] outp = CryptoService.Decrypt(file, "MasterClave123");

        Assert.Equal(plain, outp);
    }

    [Fact]
    public void Decrypt_with_wrong_password_throws()
    {
        byte[] file = CryptoService.Encrypt(Utf8("data"), "correcta");
        Assert.Throws<AuthenticationTagMismatchException>(
            () => CryptoService.Decrypt(file, "incorrecta"));
    }

    [Fact]
    public void Key_based_and_password_based_are_compatible()
    {
        // Encrypt with the password wrapper...
        byte[] plain = Utf8("compat-check");
        byte[] file = CryptoService.Encrypt(plain, "clave");

        // ...and decrypt via the key-based path used by the session (derive from stored format).
        VaultFormat fmt = CryptoService.ReadFormat(file);
        byte[] key = CryptoService.DeriveKey("clave", fmt);
        byte[] outp = CryptoService.Decrypt(file, key);

        Assert.Equal(plain, outp);
    }

    [Fact]
    public void New_vaults_use_PMV2_Argon2id()
    {
        byte[] file = CryptoService.Encrypt(Utf8("x"), "clave");
        Assert.Equal("PMV2", Encoding.ASCII.GetString(file, 0, 4));
        Assert.Equal(VaultKdf.Argon2id, CryptoService.ReadFormat(file).Kdf);
    }

    [Fact]
    public void Reusing_format_and_key_produces_readable_vault_with_fresh_nonce()
    {
        // Mirrors the session's save path: derive once, reuse key+format, new nonce each save.
        VaultFormat fmt = CryptoService.NewDefaultFormat();
        byte[] key = CryptoService.DeriveKey("clave", fmt);

        byte[] f1 = CryptoService.Encrypt(Utf8("v1"), key, fmt);
        byte[] f2 = CryptoService.Encrypt(Utf8("v2"), key, fmt);

        Assert.NotEqual(f1, f2);
        Assert.Equal("v1", Encoding.UTF8.GetString(CryptoService.Decrypt(f1, key)));
        Assert.Equal("v2", Encoding.UTF8.GetString(CryptoService.Decrypt(f2, key)));
    }

    [Fact]
    public void Argon2id_derivation_with_production_params_is_deterministic_and_salt_dependent()
    {
        // Explicit production-like parameters (not the test-lowered static defaults).
        byte[] salt = CryptoService.GenerateSalt();
        var fmt = new VaultFormat
        {
            Kdf = VaultKdf.Argon2id, Salt = salt,
            MemoryKiB = 65_536, Iterations = 3, Parallelism = 4,
        };

        byte[] k1 = CryptoService.DeriveKey("clave", fmt);
        byte[] k2 = CryptoService.DeriveKey("clave", fmt);
        Assert.Equal(32, k1.Length);
        Assert.Equal(k1, k2); // same password+params+salt -> same key

        var fmt2 = new VaultFormat
        {
            Kdf = VaultKdf.Argon2id, Salt = CryptoService.GenerateSalt(),
            MemoryKiB = 65_536, Iterations = 3, Parallelism = 4,
        };
        Assert.NotEqual(k1, CryptoService.DeriveKey("clave", fmt2)); // different salt -> different key
    }

    [Theory]
    [InlineData(0)]    // magic byte
    [InlineData(5)]    // salt
    [InlineData(21)]   // nonce
    [InlineData(33)]   // tag
    public void Tampering_any_region_fails_authentication(int byteIndex)
    {
        byte[] file = CryptoService.Encrypt(Utf8("sensitive-data-here"), "clave");
        file[byteIndex] ^= 0xFF; // flip a byte

        // A flipped magic byte is rejected as bad format; everything else fails GCM auth.
        Assert.ThrowsAny<System.Exception>(() => CryptoService.Decrypt(file, "clave"));
    }

    [Fact]
    public void Tampering_ciphertext_fails_authentication()
    {
        byte[] file = CryptoService.Encrypt(Utf8("sensitive-data-here"), "clave");
        file[^1] ^= 0xFF; // last byte = ciphertext
        Assert.Throws<AuthenticationTagMismatchException>(
            () => CryptoService.Decrypt(file, "clave"));
    }

    [Theory]
    [InlineData("")]                 // empty vault content
    [InlineData("ñ áéíóú 中文 🔐")]     // unicode + emoji
    public void Roundtrips_edge_content(string content)
    {
        byte[] file = CryptoService.Encrypt(Utf8(content), "clave");
        Assert.Equal(content, Encoding.UTF8.GetString(CryptoService.Decrypt(file, "clave")));
    }

    [Fact]
    public void Large_vault_roundtrips()
    {
        byte[] big = RandomNumberGenerator.GetBytes(2_000_000);
        byte[] file = CryptoService.Encrypt(big, "clave");
        Assert.Equal(big, CryptoService.Decrypt(file, "clave"));
    }
}
