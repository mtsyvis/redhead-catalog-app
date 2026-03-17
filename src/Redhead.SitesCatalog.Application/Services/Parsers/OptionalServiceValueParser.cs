using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Services.Parsers;

public static class OptionalServiceValueParser
{
    private static readonly HashSet<string> NotAvailableMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "NO",
        "N/A",
        "NA",
        "-",
        "NONE",
        "NOT AVAILABLE"
    };

    public sealed record ParseResult(
        bool IsValid,
        ServiceAvailabilityStatus Status,
        decimal? Price,
        string? ErrorMessage);

    public static ParseResult Parse(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return new ParseResult(true, ServiceAvailabilityStatus.Unknown, null, null);
        }

        var trimmed = rawValue.Trim();
        var normalizedMarker = NormalizeMarker(trimmed);

        if (NotAvailableMarkers.Contains(normalizedMarker))
        {
            return new ParseResult(true, ServiceAvailabilityStatus.NotAvailable, null, null);
        }

        if (!DecimalParsingHelper.TryParseDecimalFlexible(trimmed, out var parsedPrice))
        {
            return new ParseResult(
                false,
                ServiceAvailabilityStatus.Unknown,
                null,
                "Invalid optional service value. Use a number, empty cell, or a supported not-available marker (NO, N/A, NA, -, NONE, NOT AVAILABLE).");
        }

        if (parsedPrice < 0)
        {
            return new ParseResult(false, ServiceAvailabilityStatus.Unknown, null, "Price must be >= 0.");
        }

        return new ParseResult(true, ServiceAvailabilityStatus.Available, parsedPrice, null);
    }

    private static string NormalizeMarker(string value)
    {
        var normalized = value.Trim().Replace("_", " ", StringComparison.Ordinal);
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized.ToUpperInvariant();
    }
}
