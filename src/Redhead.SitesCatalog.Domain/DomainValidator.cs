using System.Globalization;
using System.Net;

namespace Redhead.SitesCatalog.Domain;

/// <summary>Validates normalized domain keys without changing them or performing DNS lookups.</summary>
public static class DomainValidator
{
    private const int MaxDomainLength = 253;
    private const int MaxLabelLength = 63;

    /// <summary>Checks the domain syntax accepted by catalog stop lists, including Unicode labels.</summary>
    public static bool IsValidNormalizedDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain) || domain.Length > MaxDomainLength)
        {
            return false;
        }

        if (!domain.Contains('.', StringComparison.Ordinal) ||
            domain.Any(char.IsWhiteSpace) ||
            domain.Any(c => c is '/' or '?' or '#' or ':' or '@'))
        {
            return false;
        }

        return domain.Split('.').All(IsValidDomainLabel);
    }

    /// <summary>
    /// Also checks IDN encoding and excludes IP addresses and short or numeric top-level labels.
    /// The ASCII conversion is used only for validation; callers retain the original domain key.
    /// </summary>
    public static bool IsValidDnsDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain) || domain.Length > MaxDomainLength ||
            IPAddress.TryParse(domain, out _))
        {
            return false;
        }

        string ascii;
        try
        {
            ascii = new IdnMapping().GetAscii(domain);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (!IsValidNormalizedDomain(ascii))
        {
            return false;
        }

        var topLevelLabel = ascii[(ascii.LastIndexOf('.') + 1)..];
        return topLevelLabel.Length >= 2 && topLevelLabel.Any(char.IsAsciiLetter);
    }

    private static bool IsValidDomainLabel(string label)
    {
        if (label.Length is 0 or > MaxLabelLength || label[0] == '-' || label[^1] == '-')
        {
            return false;
        }

        return label.All(c => char.IsLetterOrDigit(c) || c == '-' ||
            char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark);
    }
}
