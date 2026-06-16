using System.Globalization;
using Redhead.SitesCatalog.Domain;

namespace Redhead.SitesCatalog.Application.Services.Analytics;

internal static class AnalyticsTermLabelFormatter
{
    public const string AnyTermLabel = "Any term";

    public static string FormatTermKey(string? termKey)
    {
        if (string.IsNullOrWhiteSpace(termKey))
        {
            return AnyTermLabel;
        }

        var normalized = termKey.Trim();
        if (string.Equals(normalized, PricingTerm.UnknownKey, StringComparison.Ordinal))
        {
            return "Unknown term";
        }

        if (string.Equals(normalized, PricingTerm.PermanentKey, StringComparison.Ordinal))
        {
            return "Permanent";
        }

        const string finitePrefix = "finite:";
        const string finiteYearSuffix = ":year";
        if (normalized.StartsWith(finitePrefix, StringComparison.Ordinal) &&
            normalized.EndsWith(finiteYearSuffix, StringComparison.Ordinal))
        {
            var valueText = normalized[finitePrefix.Length..^finiteYearSuffix.Length];
            if (int.TryParse(valueText, NumberStyles.None, CultureInfo.InvariantCulture, out var value) &&
                value > 0)
            {
                return value == 1 ? "1 year" : $"{value} years";
            }
        }

        return normalized;
    }
}
