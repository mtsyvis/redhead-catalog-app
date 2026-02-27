using System.Globalization;

namespace Redhead.SitesCatalog.Application.Services.Parsers;

/// <summary>
/// Helper for culture-independent decimal parsing that supports both dot and comma as decimal separators.
/// </summary>
public static class DecimalParsingHelper
{
    /// <summary>
    /// Tries to parse a decimal value in a culture-independent way.
    /// Supports both "0.9" and "0,9" (with comma only when no dot is present).
    /// Does not rely on CurrentCulture.
    /// </summary>
    public static bool TryParseDecimalFlexible(string? s, out decimal value)
    {
        value = 0m;

        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        var normalized = s.Trim();

        if (normalized.Contains(',') && !normalized.Contains('.'))
        {
            normalized = normalized.Replace(',', '.');
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}

