using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Services.Parsers;

public static class TermValueParser
{
    public sealed record ParseResult(bool IsValid, TermType? TermType, int? TermValue, TermUnit? TermUnit, string? ErrorMessage);

    public static ParseResult Parse(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return new ParseResult(true, null, null, null, null);
        }

        var trimmed = rawValue.Trim();
        if (string.Equals(trimmed, "permanent", StringComparison.OrdinalIgnoreCase))
        {
            return new ParseResult(true, TermType.Permanent, null, null, null);
        }

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return Invalid();
        }

        if (!int.TryParse(parts[0], out var value) || value <= 0)
        {
            return Invalid();
        }

        if (!string.Equals(parts[1], "year", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(parts[1], "years", StringComparison.OrdinalIgnoreCase))
        {
            return Invalid();
        }

        return new ParseResult(true, TermType.Finite, value, TermUnit.Year, null);
    }

    private static ParseResult Invalid()
        => new(false, null, null, null, "Invalid Term value.");
}
