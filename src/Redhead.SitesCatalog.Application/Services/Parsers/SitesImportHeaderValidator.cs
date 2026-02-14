using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Application.Services.Parsers;

/// <summary>
/// Validates that the import file header has all required columns in the correct order.
/// All columns must be present; cell values for optional fields may be empty.
/// </summary>
public static class SitesImportHeaderValidator
{
    /// <summary>
    /// Returns null if valid; otherwise returns a user-facing error message.
    /// </summary>
    public static string? Validate(string[] header)
    {
        var required = ImportConstants.SitesImportRequiredColumnOrder;
        if (header.Length < required.Length)
        {
            return "Missing required columns. Expected in order: Domain, DR, Traffic, Location, PriceUsd, PriceCasino, PriceCrypto, PriceLinkInsert, Niche, Categories. "
                + $"Found {header.Length} column(s).";
        }

        for (var i = 0; i < required.Length; i++)
        {
            var actual = header[i]?.Trim() ?? "";
            var expected = required[i];
            if (string.IsNullOrEmpty(actual))
            {
                return $"Column {i + 1} must be '{expected}'. Found empty.";
            }

            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                return $"Column {i + 1} must be '{expected}'. Found: '{actual}'.";
            }
        }

        return null;
    }
}
