using System.Security.Cryptography;

namespace Redhead.SitesCatalog.Api.Services;

/// <summary>
/// Generates a random password that meets typical Identity requirements
/// (digit, upper, lower, non-alphanumeric, length 8). Do not log the result.
/// </summary>
public static class PasswordGenerator
{
    private const string Lower = "abcdefghjkmnpqrstuvwxyz";
    private const string Upper = "ABCDEFGHJKMNPQRSTUVWXYZ";
    private const string Digit = "23456789";
    private const string Special = "!@#$%&*";
    private const string All = Lower + Upper + Digit + Special;

    public static string Generate(int length = 12)
    {
        var bytes = new byte[length * 2];
        RandomNumberGenerator.Fill(bytes);

        var chars = new char[length];
        chars[0] = Lower[bytes[0] % Lower.Length];
        chars[1] = Upper[bytes[1] % Upper.Length];
        chars[2] = Digit[bytes[2] % Digit.Length];
        chars[3] = Special[bytes[3] % Special.Length];

        for (var i = 4; i < length; i++)
        {
            chars[i] = All[bytes[4 + i] % All.Length];
        }

        // Shuffle
        for (var i = length - 1; i > 0; i--)
        {
            var j = bytes[length + i] % (i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
