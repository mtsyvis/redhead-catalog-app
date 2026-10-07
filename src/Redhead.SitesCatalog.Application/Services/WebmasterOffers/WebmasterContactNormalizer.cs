using System.Text.RegularExpressions;

namespace Redhead.SitesCatalog.Application.Services.WebmasterOffers;

internal static partial class WebmasterContactNormalizer
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static string Normalize(string? contactRawText)
    {
        var trimmed = contactRawText?.Trim();
        return string.IsNullOrEmpty(trimmed)
            ? string.Empty
            : WhitespaceRegex().Replace(trimmed, " ").ToLowerInvariant();
    }
}
