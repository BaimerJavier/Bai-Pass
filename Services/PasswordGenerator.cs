using System;
using System.Security.Cryptography;
using System.Text;

namespace PasswordManager.Services;

/// <summary>Generates strong random passwords using a cryptographic RNG.</summary>
public static class PasswordGenerator
{
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";      // no l
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";       // no I, O
    private const string Digits = "23456789";                     // no 0, 1
    private const string Symbols = "!@#$%^&*()-_=+[]{}";

    public static string Generate(int length = 20,
        bool useUpper = true, bool useDigits = true, bool useSymbols = true)
    {
        if (length < 4) length = 4;

        var pool = new StringBuilder(Lower);
        if (useUpper) pool.Append(Upper);
        if (useDigits) pool.Append(Digits);
        if (useSymbols) pool.Append(Symbols);

        string chars = pool.ToString();
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(chars[RandomNumberGenerator.GetInt32(chars.Length)]);
        return sb.ToString();
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
