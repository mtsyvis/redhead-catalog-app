using System.Globalization;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Application.Services;

internal sealed record SitesExportColumnDefinition(
    string Key,
    string Header,
    bool Exportable,
    Func<string, bool> IsAllowedForRole,
    Func<Site, XlsxCell> CreateCell,
    double Width);

internal static class SitesExportColumnRegistry
{
    private static readonly IReadOnlyList<SitesExportColumnDefinition> Columns =
    [
        Exportable("domain", "Domain", site => XlsxCell.Text(site.Domain), 28),
        Exportable("dr", "DR", site => XlsxCell.Number(Convert.ToDecimal(site.DR, CultureInfo.InvariantCulture), XlsxCellStyle.Integer), 8),
        Exportable("traffic", "Traffic", site => XlsxCell.Number(site.Traffic, XlsxCellStyle.Integer), 14),
        Exportable("location", "Location", site => XlsxCell.Text(LocationDisplayFormatter.Format(site.LocationKey, site.CanonicalLocation?.DisplayName, site.Location)), 14),
        Exportable("priceUsd", "Price USD", site => XlsxCell.Number(site.PriceUsd, XlsxCellStyle.Decimal), 12),
        Exportable("priceCasino", "Casino", site => FormatOptionalService(site.PriceCasino, site.PriceCasinoStatus), 16),
        Exportable("priceCrypto", "Crypto", site => FormatOptionalService(site.PriceCrypto, site.PriceCryptoStatus), 16),
        Exportable("priceLinkInsert", "Link Insert", site => FormatOptionalService(site.PriceLinkInsert, site.PriceLinkInsertStatus), 18),
        Exportable("priceLinkInsertCasino", "Link Insert Casino", site => FormatOptionalService(site.PriceLinkInsertCasino, site.PriceLinkInsertCasinoStatus), 24),
        Exportable("priceDating", "Dating", site => FormatOptionalService(site.PriceDating, site.PriceDatingStatus), 16),
        Exportable("niche", "Niche", site => XlsxCell.Text(site.Niche), 20),
        Exportable("categories", "Categories", site => XlsxCell.Text(site.Categories), 28),
        Exportable("numberDFLinks", "DF Links", site => XlsxCell.Number(site.NumberDFLinks, XlsxCellStyle.Integer), 16),
        Exportable("sponsoredTag", "Sponsored Tag", site => XlsxCell.Text(site.SponsoredTag), 16),
        Exportable("term", "Term", site => XlsxCell.Text(FormatTerm(site.TermType, site.TermValue, site.TermUnit)), 16),
        Exportable("language", "Language", site => XlsxCell.Text(FormatLanguage(site.Language)), 14),
        Exportable("isQuarantined", "Status", site => XlsxCell.Text(site.IsQuarantined ? "Unavailable" : "Available"), 14),
        Exportable("lastPublishedDate", "Last Published", site => XlsxCell.Text(FormatLastPublishedDate(site)), 24),
        Exportable("quarantineReason", "Quarantine reason", site => XlsxCell.Text(site.QuarantineReason), 28, NonClientOnly),
        NonExportable("actions", "Actions")
    ];

    private static readonly IReadOnlyDictionary<string, SitesExportColumnDefinition> ColumnsByKey =
        Columns.ToDictionary(column => column.Key, StringComparer.Ordinal);

    public static IReadOnlyList<SitesExportColumnDefinition> ValidateRequestedColumns(
        IReadOnlyList<string>? requestedColumnKeys,
        string userRole)
    {
        if (requestedColumnKeys is not { Count: > 0 })
        {
            throw new RequestValidationException("At least one visible column key is required for export.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var validatedColumns = new List<SitesExportColumnDefinition>(requestedColumnKeys.Count);

        foreach (var key in requestedColumnKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new RequestValidationException("Export column keys cannot be empty.");
            }

            if (!seen.Add(key))
            {
                continue;
            }

            if (!ColumnsByKey.TryGetValue(key, out var column))
            {
                throw new RequestValidationException($"Unknown export column key: {key}.");
            }

            if (!column.Exportable)
            {
                throw new RequestValidationException($"Column cannot be exported: {key}.");
            }

            if (!column.IsAllowedForRole(userRole))
            {
                throw new RequestValidationException($"Column is not allowed for the current role: {key}.");
            }

            validatedColumns.Add(column);
        }

        return validatedColumns;
    }

    public static IReadOnlyList<string> GetDefaultColumnKeysForRole(string userRole)
        => Columns
            .Where(column => column.Exportable && column.IsAllowedForRole(userRole))
            .Select(column => column.Key)
            .ToArray();

    private static SitesExportColumnDefinition Exportable(
        string key,
        string header,
        Func<Site, XlsxCell> createCell,
        double width,
        Func<string, bool>? isAllowedForRole = null)
        => new(key, header, Exportable: true, isAllowedForRole ?? AllowAllRoles, createCell, width);

    private static SitesExportColumnDefinition NonExportable(string key, string header)
        => new(key, header, Exportable: false, AllowAllRoles, _ => XlsxCell.Blank(), 12);

    private static bool AllowAllRoles(string _) => true;

    private static bool NonClientOnly(string role)
        => !string.Equals(role, AppRoles.Client, StringComparison.Ordinal);

    private static string FormatLastPublishedDate(Site site)
    {
        if (!site.LastPublishedDate.HasValue)
        {
            return "Before January 2026";
        }

        return site.LastPublishedDateIsMonthOnly
            ? site.LastPublishedDate.Value.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : site.LastPublishedDate.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    private static XlsxCell FormatOptionalService(decimal? price, ServiceAvailabilityStatus status)
    {
        return status switch
        {
            ServiceAvailabilityStatus.Available when price.HasValue => XlsxCell.Number(price, XlsxCellStyle.Decimal),
            ServiceAvailabilityStatus.AvailableWithUnknownPrice => XlsxCell.Text("YES"),
            ServiceAvailabilityStatus.NotAvailable => XlsxCell.Text("NO"),
            _ => XlsxCell.Blank()
        };
    }

    private static string FormatTerm(TermType? termType, int? termValue, TermUnit? termUnit)
    {
        if (termType is null)
        {
            return string.Empty;
        }

        if (termType == TermType.Permanent)
        {
            return "permanent";
        }

        if (termType == TermType.Finite && termValue.HasValue && termUnit == TermUnit.Year)
        {
            return termValue.Value == 1 ? "1 year" : $"{termValue.Value} years";
        }

        return string.Empty;
    }

    private static string FormatLanguage(string? language)
        => language ?? LanguageNormalizer.Unknown;
}
