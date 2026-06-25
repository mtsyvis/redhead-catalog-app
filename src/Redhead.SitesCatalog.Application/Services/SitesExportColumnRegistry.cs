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
    Func<Site, string?, XlsxCell> CreateCell,
    double Width);

internal static class SitesExportColumnRegistry
{
    private static readonly IReadOnlyList<SitesExportColumnDefinition> Columns =
    [
        Exportable("domain", "Domain", site => XlsxCell.Text(site.Domain), 28),
        Exportable("dr", "DR", site => XlsxCell.Number(Convert.ToDecimal(site.DR, CultureInfo.InvariantCulture), XlsxCellStyle.Integer), 8),
        Exportable("traffic", "Traffic", site => XlsxCell.Number(site.Traffic, XlsxCellStyle.Integer), 14),
        Exportable("location", "Location", site => XlsxCell.Text(LocationDisplayFormatter.Format(site.LocationKey, site.CanonicalLocation?.DisplayName, site.Location)), 14),
        ExportableWithTerm("priceUsd", "Price USD", (site, selectedTermKey) => FormatMainPrice(site, selectedTermKey), 24),
        ExportableWithTerm("priceCasino", "Casino", (site, selectedTermKey) => FormatOptionalService(site, PriceType.Casino, selectedTermKey), 24),
        ExportableWithTerm("priceCrypto", "Crypto", (site, selectedTermKey) => FormatOptionalService(site, PriceType.Crypto, selectedTermKey), 24),
        ExportableWithTerm("priceLinkInsert", "Link Insert", (site, selectedTermKey) => FormatOptionalService(site, PriceType.LinkInsertion, selectedTermKey), 24),
        ExportableWithTerm("priceLinkInsertCasino", "Link Insert Casino", (site, selectedTermKey) => FormatOptionalService(site, PriceType.LinkInsertionCasino, selectedTermKey), 28),
        ExportableWithTerm("priceDating", "Dating", (site, selectedTermKey) => FormatOptionalService(site, PriceType.Dating, selectedTermKey), 24),
        Exportable("niche", "Niche", site => XlsxCell.Text(site.Niche), 20),
        Exportable("categories", "Categories", site => XlsxCell.Text(site.Categories), 28),
        Exportable("numberDFLinks", "DF Links", site => XlsxCell.Number(site.NumberDFLinks, XlsxCellStyle.Integer), 16),
        Exportable("sponsoredTag", "Sponsored Tag", site => XlsxCell.Text(site.SponsoredTag), 16),
        Exportable("term", "Term", site => XlsxCell.Text(FormatTerm(site.TermType, site.TermValue, site.TermUnit)), 16),
        Exportable("language", "Language", site => XlsxCell.Text(FormatLanguage(site.Language)), 14),
        Exportable("isQuarantined", "Status", site => XlsxCell.Text(site.IsQuarantined ? "Unavailable" : "Available"), 14),
        Exportable("lastPublishedDate", "Last Published", site => XlsxCell.Text(FormatLastPublishedDate(site)), 24),
        Exportable("createdAt", "Created Date", site => XlsxCell.Text(FormatAuditDate(site.CreatedAtUtc)), 16),
        Exportable("quarantineReason", "Quarantine reason", site => XlsxCell.Text(site.QuarantineReason), 28, NonClientOnly),
        Exportable("updatedAt", "Updated Date", site => XlsxCell.Text(FormatAuditDate(site.UpdatedAtUtc)), 16, NonClientOnly),
        Exportable("createdBy", "Created By", site => XlsxCell.Text(AuditUserFormatter.Format(site.CreatedBy)), 28, NonClientOnly),
        Exportable("updatedBy", "Updated By", site => XlsxCell.Text(AuditUserFormatter.Format(site.UpdatedBy)), 28, NonClientOnly),
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
        => new(key, header, Exportable: true, isAllowedForRole ?? AllowAllRoles, (site, _) => createCell(site), width);

    private static SitesExportColumnDefinition ExportableWithTerm(
        string key,
        string header,
        Func<Site, string?, XlsxCell> createCell,
        double width,
        Func<string, bool>? isAllowedForRole = null)
        => new(key, header, Exportable: true, isAllowedForRole ?? AllowAllRoles, createCell, width);

    private static SitesExportColumnDefinition NonExportable(string key, string header)
        => new(key, header, Exportable: false, AllowAllRoles, (_, _) => XlsxCell.Blank(), 12);

    private static bool AllowAllRoles(string _) => true;

    private static bool NonClientOnly(string role)
        => !string.Equals(role, AppRoles.Client, StringComparison.Ordinal) &&
           !string.Equals(role, AppRoles.Lite, StringComparison.Ordinal);

    private static string FormatLastPublishedDate(Site site)
    {
        if (!site.LastPublishedDate.HasValue)
        {
            return string.Empty;
        }

        return site.LastPublishedDateIsMonthOnly
            ? site.LastPublishedDate.Value.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : site.LastPublishedDate.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    private static string FormatAuditDate(DateTime? value)
        => value.HasValue
            ? value.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
            : "—";

    private static XlsxCell FormatMainPrice(Site site, string? selectedTermKey)
        => FormatPriceAmount(SelectExportPrice(
            site.PriceOptions.Where(price => price.PriceType == PriceType.Main),
            selectedTermKey));

    private static XlsxCell FormatOptionalService(Site site, PriceType serviceType, string? selectedTermKey)
    {
        var normalizedTermKey = NormalizeSelectedTermKey(selectedTermKey);
        var selectedPrice = SelectExportPrice(
            site.PriceOptions.Where(price => price.PriceType == serviceType),
            normalizedTermKey);
        if (selectedPrice.HasValue)
        {
            return FormatPriceAmount(selectedPrice);
        }

        if (normalizedTermKey is not null)
        {
            return XlsxCell.Text(EmptyPricingLabel);
        }

        var status = site.ServiceAvailabilities
            .FirstOrDefault(availability => availability.ServiceType == serviceType)
            ?.Status ?? ServiceAvailabilityStatus.Unknown;

        return status switch
        {
            ServiceAvailabilityStatus.AvailableWithUnknownPrice => XlsxCell.Text("YES"),
            ServiceAvailabilityStatus.NotAvailable => XlsxCell.Text("NO"),
            _ => XlsxCell.Text(EmptyPricingLabel)
        };
    }

    private static readonly string EmptyPricingLabel = string.Empty;

    private static decimal? SelectExportPrice(
        IEnumerable<SitePriceOption> priceOptions,
        string? selectedTermKey)
    {
        var normalizedTermKey = NormalizeSelectedTermKey(selectedTermKey);
        var matchingPrices = priceOptions
            .Where(price => price.AmountUsd > 0);

        if (normalizedTermKey is not null)
        {
            matchingPrices = matchingPrices.Where(price =>
                string.Equals(price.TermKey, normalizedTermKey, StringComparison.Ordinal));
        }

        return matchingPrices
            .OrderBy(price => price.AmountUsd)
            .ThenBy(GetTermSortOrder)
            .ThenBy(price => price.TermValue)
            .Select(price => (decimal?)price.AmountUsd)
            .FirstOrDefault();
    }

    private static XlsxCell FormatPriceAmount(decimal? amount)
        => amount.HasValue
            ? XlsxCell.Number(amount.Value, XlsxCellStyle.Decimal)
            : XlsxCell.Text(EmptyPricingLabel);

    private static string? NormalizeSelectedTermKey(string? selectedTermKey)
        => string.IsNullOrWhiteSpace(selectedTermKey) ? null : selectedTermKey.Trim();

    private static int GetTermSortOrder(SitePriceOption priceOption)
        => priceOption.TermKey switch
        {
            PricingTerm.UnknownKey => 0,
            _ when priceOption.TermType == TermType.Finite => 1,
            PricingTerm.PermanentKey => 2,
            _ => 3
        };

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
