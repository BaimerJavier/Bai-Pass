using System.Linq;
using PasswordManager.Services;
using Xunit;

namespace BaiPass.Tests;

public class PasswordGeneratorTests
{
    [Fact]
    public void Generates_requested_length()
    {
        Assert.Equal(20, PasswordGenerator.Generate(20).Length);
        Assert.Equal(32, PasswordGenerator.Generate(32).Length);
    }

    [Fact]
    public void Guarantees_every_enabled_category_is_present()
    {
        // Run many times: each selected category must always appear (no random omission).
        for (int i = 0; i < 200; i++)
        {
            string p = PasswordGenerator.Generate(8, useLower: true, useUpper: true,
                useDigits: true, useSymbols: true, excludeAmbiguous: true);

            Assert.Contains(p, char.IsLower);
            Assert.Contains(p, char.IsUpper);
            Assert.Contains(p, char.IsDigit);
            Assert.Contains(p, c => "!@#$%^&*()-_=+[]{}".Contains(c));
        }
    }

    [Fact]
    public void Excludes_ambiguous_characters_by_default()
    {
        for (int i = 0; i < 100; i++)
        {
            string p = PasswordGenerator.Generate(40);
            Assert.DoesNotContain('l', p);
            Assert.DoesNotContain('I', p);
            Assert.DoesNotContain('O', p);
            Assert.DoesNotContain('0', p);
            Assert.DoesNotContain('1', p);
        }
    }

    [Fact]
    public void Only_selected_categories_are_used()
    {
        string p = PasswordGenerator.Generate(30, useLower: false, useUpper: false,
            useDigits: true, useSymbols: false);
        Assert.All(p, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void Produces_distinct_outputs()
    {
        var set = Enumerable.Range(0, 50).Select(_ => PasswordGenerator.Generate(20)).ToHashSet();
        Assert.True(set.Count > 45); // effectively always unique
    }
}
