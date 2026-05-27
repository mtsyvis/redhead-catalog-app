using Redhead.SitesCatalog.Domain.Constants;
namespace Redhead.SitesCatalog.Application.Services;

public static class LocationDisplayFormatter
{
    public const string OtherPseudoKey = "OTHER";
    public const string UnknownDisplayName = "Unknown";
    public const string OtherDisplayName = "Other";

    public static string Format(string? locationKey, string? canonicalDisplayName, string? fallbackDisplay = null)
    {
        if (string.IsNullOrWhiteSpace(locationKey))
        {
            return OtherDisplayName;
        }

        if (!string.IsNullOrWhiteSpace(canonicalDisplayName))
        {
            return canonicalDisplayName;
        }

        if (string.Equals(locationKey, LocationConstants.UnknownLocationKey, StringComparison.Ordinal))
        {
            return UnknownDisplayName;
        }

        return string.IsNullOrWhiteSpace(fallbackDisplay)
            ? locationKey
            : fallbackDisplay;
    }
}
