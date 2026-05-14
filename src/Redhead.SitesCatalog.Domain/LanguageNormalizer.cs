using System.Text.RegularExpressions;

namespace Redhead.SitesCatalog.Domain;

/// <summary>
/// Normalizes optional site language values for catalog writes.
/// </summary>
public static partial class LanguageNormalizer
{
    public const string Unknown = "UNKNOWN";
    public const string Multi = "MULTI";

    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        if (trimmed.Equals("english", StringComparison.OrdinalIgnoreCase))
        {
            return "EN";
        }

        if (trimmed.Equals(Unknown, StringComparison.OrdinalIgnoreCase))
        {
            return Unknown;
        }

        if (trimmed.Equals(Multi, StringComparison.OrdinalIgnoreCase))
        {
            return Multi;
        }

        var code = trimmed;
        var separatorIndex = code.IndexOfAny(['-', '_']);
        if (separatorIndex >= 0)
        {
            code = code[..separatorIndex];
        }

        code = code.ToUpperInvariant();
        return LanguageCodeRegex().IsMatch(code) ? code : null;
    }

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex LanguageCodeRegex();
}
