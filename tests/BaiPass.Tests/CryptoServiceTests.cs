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
        // Encrypt with the password wrapper (old-style call path)...
        byte[] plain = Utf8("compat-check");
        byte[] file = CryptoService.Encrypt(plain, "clave");

        // ...and decrypt via the key-based path used by the session (derive from stored salt).
        byte[] salt = CryptoService.ReadSalt(file);
        byte[] key = CryptoService.DeriveKey("clave", salt);
        byte[] outp = CryptoService.DecryptWithKey(file, key);

        Assert.Equal(plain, outp);
    }

    [Fact]
    public void Reusing_salt_and_key_produces_readable_vault_with_fresh_nonce()
    {
        // Mirrors the session's save path: derive once, reuse salt+key, new nonce each save.
        byte[] salt = CryptoService.GenerateSalt();
        byte[] key = CryptoService.DeriveKey("clave", salt);

        byte[] f1 = CryptoService.EncryptWithKey(Utf8("v1"), key, salt);
        byte[] f2 = CryptoService.EncryptWithKey(Utf8("v2"), key, salt);

        // Different ciphertext/nonce, both decrypt with the same key.
        Assert.NotEqual(f1, f2);
        Assert.Equal("v1", Encoding.UTF8.GetString(CryptoService.DecryptWithKey(f1, key)));
        Assert.Equal("v2", Encoding.UTF8.GetString(CryptoService.DecryptWithKey(f2, key)));
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
