using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace PasswordManager.Services;

/// <summary>
/// Generates strong random passwords using only a cryptographic RNG
/// (<see cref="RandomNumberGenerator"/>). Never uses Random/Guid/DateTime for entropy.
/// </summary>
public static class PasswordGenerator
{
    // Ambiguous-excluded sets (default) and full sets.
    private const string LowerSafe = "abcdefghijkmnopqrstuvwxyz";     // no l
    private const string UpperSafe = "ABCDEFGHJKLMNPQRSTUVWXYZ";      // no I, O
    private const string DigitsSafe = "23456789";                    // no 0, 1
    private const string LowerFull = "abcdefghijklmnopqrstuvwxyz";
    private const string UpperFull = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string DigitsFull = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{}";

    public static string Generate(int length = 20,
        bool useLower = true, bool useUpper = true, bool useDigits = true, bool useSymbols = true,
        bool excludeAmbiguous = true)
    {
        // Build the enabled category alphabets.
        var categories = new List<string>();
        if (useLower) categories.Add(excludeAmbiguous ? LowerSafe : LowerFull);
        if (useUpper) categories.Add(excludeAmbiguous ? UpperSafe : UpperFull);
        if (useDigits) categories.Add(excludeAmbiguous ? DigitsSafe : DigitsFull);
        if (useSymbols) categories.Add(Symbols);

        if (categories.Count == 0) categories.Add(LowerSafe); // never produce an empty alphabet
        if (length < categories.Count) length = categories.Count; // room for one of each

        string all = string.Concat(categories);
        var chars = new char[length];

        // Guarantee at least one character from every enabled category...
        int i = 0;
        foreach (var cat in categories)
            chars[i++] = cat[RandomNumberGenerator.GetInt32(cat.Length)];

        // ...fill the rest from the combined alphabet...
        for (; i < length; i++)
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        // ...then shuffle so the guaranteed characters are not in fixed positions (Fisher-Yates).
        for (int n = length - 1; n > 0; n--)
        {
            int j = RandomNumberGenerator.GetInt32(n + 1);
            (chars[n], chars[j]) = (chars[j], chars[n]);
        }

        return new string(chars);
    }

    /// <summary>Rough strength score 0..4 based on length and character variety.</summary>
    public static (int Score, string Label) Strength(string password)
    {
        if (string.IsNullOrEmpty(password)) return (0, "Vacia");

        int variety = 0;
        if (System.Text.RegularExpressions.Regex.IsMatch(password, "[a-z]")) variety++;
        if (System.Text.RegularExpressions.Regex.IsMatch(password, "[A-Z]")) variety++;
        if (System.Text.RegularExpressions.Regex.IsMatch(password, "[0-9]")) variety++;
        if (System.Text.RegularExpressions.Regex.IsMatch(password, "[^a-zA-Z0-9]")) variety++;

        int score = 0;
        if (password.Length >= 8) score++;
        if (password.Length >= 12) score++;
        if (variety >= 3) score++;
        if (password.Length >= 16 && variety >= 4) score++;

        string label = score switch
        {
            0 or 1 => "Debil",
            2 => "Aceptable",
            3 => "Fuerte",
            _ => "Muy fuerte",
        };
        return (score, label);
    }
}
