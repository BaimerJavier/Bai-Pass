using System;
using PasswordManager.Security;
using Xunit;

namespace BaiPass.Tests;

public class SecureSessionKeyTests
{
    [Fact]
    public void Exposes_key_until_disposed()
    {
        var raw = new byte[] { 1, 2, 3, 4 };
        var key = new SecureSessionKey(raw);

        Assert.False(key.IsDisposed);
        Assert.True(key.Key.SequenceEqual(new byte[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public void Dispose_zeroes_the_underlying_buffer()
    {
        var raw = new byte[] { 9, 9, 9, 9, 9 };
        var key = new SecureSessionKey(raw);

        key.Dispose();

        // The caller-provided array is wiped in place.
        Assert.All(raw, b => Assert.Equal(0, b));
        Assert.True(key.IsDisposed);
    }

    [Fact]
    public void Accessing_key_after_dispose_throws()
    {
        var key = new SecureSessionKey(new byte[] { 1 });
        key.Dispose();
        Assert.Throws<ObjectDisposedException>(() => key.Key.Length);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var key = new SecureSessionKey(new byte[] { 1 });
        key.Dispose();
        key.Dispose(); // must not throw
        Assert.True(key.IsDisposed);
    }
}
