using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services.Import.ValueParsers;
using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Services.Import.Sites;

/// <summary>
/// Insert-import-specific validation for the new term-aware pricing CSV format.
/// </summary>
public static class SitesImportRowValidator
{
    private static readonly PriceType[] OptionalServiceTypes =
    [
        PriceType.Casino,
        PriceType.Crypto,
        PriceType.LinkInsertion,
        PriceType.LinkInsertionCasino,
        PriceType.Dating
    ];

    public sealed record ValidatedSitesPriceOption(
        PriceType PriceType,
        string TermKey,
        TermType? TermType,
        int? TermValue,
        TermUnit? TermUnit,
        decimal AmountUsd);

    public sealed record ValidatedSitesServiceAvailability(
        PriceType ServiceType,
        ServiceAvailabilityStatus Status);

    /// <summary>
    /// Validated insert row data. Domain is the normalized lookup key.
    /// </summary>
    public sealed record ValidatedSitesRow(
        string NormalizedDomain,
        double DR,
        long Traffic,
        string Location,
        int? NumberDFLinks,
        string? Language,
        string? Niche,
        string? Categories,
        string? SponsoredTag,
        IReadOnlyList<ValidatedSitesPriceOption> PriceOptions,
        IReadOnlyList<ValidatedSitesServiceAvailability> ServiceAvailabilities);

    public static (bool IsEmpty, SitesImportError? Error, ValidatedSitesRow? Data) Validate(SitesImportRowDto row)
    {
        if (IsEmptyRow(row))
        {
            return (true, null, null);
        }

        if (string.IsNullOrWhiteSpace(row.Domain))
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "Domain is required."
            }, null);
        }

        var domain = DomainNormalizer.Normalize(row.Domain);
        if (string.IsNullOrEmpty(domain))
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "Domain could not be normalized."
            }, null);
        }

        var requiredFieldErrors = new List<string>();
        if (!TryParseRequiredDouble(row.DRRaw, row.DR, out var dr))
        {
            requiredFieldErrors.Add("DR is required.");
        }
        else if (dr < SiteWriteValidator.DrMin || dr > SiteWriteValidator.DrMax)
        {
            requiredFieldErrors.Add($"DR must be between {SiteWriteValidator.DrMin} and {SiteWriteValidator.DrMax}.");
        }

        if (!TryParseRequiredLong(row.TrafficRaw, row.Traffic, out var traffic))
        {
            requiredFieldErrors.Add("Traffic is required.");
        }
        else if (traffic < 0)
        {
            requiredFieldErrors.Add("Traffic must be 0 or greater.");
        }

        var location = row.Location?.Trim() ?? string.Empty;
        if (location.Length > SiteFieldLimits.LocationMaxLength)
        {
            requiredFieldErrors.Add($"Location must be at most {SiteFieldLimits.LocationMaxLength} characters.");
        }

        if (!string.IsNullOrWhiteSpace(row.NumberDFLinksRaw) && row.NumberDFLinks is null)
        {
            requiredFieldErrors.Add("Invalid NumberDFLinks value.");
        }
        else if (row.NumberDFLinks is <= 0)
        {
            requiredFieldErrors.Add("Number DF Links must be greater than 0.");
        }

        var trimmedLanguage = row.Language?.Trim();
        if (trimmedLanguage is { Length: > SiteFieldLimits.LanguageMaxLength })
        {
            requiredFieldErrors.Add($"Language must be at most {SiteFieldLimits.LanguageMaxLength} characters.");
        }

        var normalizedLanguage = LanguageNormalizer.Normalize(row.Language);
        if (!string.IsNullOrWhiteSpace(row.Language) && normalizedLanguage is null)
        {
            requiredFieldErrors.Add("Language must be a two-letter code, UNKNOWN, or MULTI.");
        }

        var sponsoredTag = TrimToNull(row.SponsoredTag);
        if (sponsoredTag is not null && sponsoredTag.Length > SiteFieldLimits.SponsoredTagMaxLength)
        {
            requiredFieldErrors.Add($"Sponsored tag must be at most {SiteFieldLimits.SponsoredTagMaxLength} characters.");
        }

        if (requiredFieldErrors.Count > 0)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Message = string.Join("; ", requiredFieldErrors)
            }, null);
        }

        var priceOptions = new List<ValidatedSitesPriceOption>();
        foreach (var priceCell in row.PriceCells)
        {
            if (string.IsNullOrWhiteSpace(priceCell.RawValue))
            {
                continue;
            }

            var rawValue = priceCell.RawValue.Trim();
            if (!DecimalParsingHelper.TryParseDecimalFlexible(rawValue, out var amount))
            {
                return RowError(row, domain, priceCell.Header, priceCell.RawValue, $"Invalid {priceCell.Header} value.");
            }

            if (amount <= 0)
            {
                return RowError(row, domain, priceCell.Header, priceCell.RawValue, $"{priceCell.Header} must be greater than 0.");
            }

            priceOptions.Add(new ValidatedSitesPriceOption(
                priceCell.PriceType,
                priceCell.TermKey,
                priceCell.TermType,
                priceCell.TermValue,
                priceCell.TermUnit,
                amount));
        }

        var explicitAvailabilityStatuses = new Dictionary<PriceType, ServiceAvailabilityStatus>();
        foreach (var availabilityCell in row.AvailabilityCells)
        {
            if (!TryParseAvailability(availabilityCell.RawValue, out var status))
            {
                return RowError(
                    row,
                    domain,
                    availabilityCell.Header,
                    availabilityCell.RawValue,
                    $"{availabilityCell.Header} must be empty, YES, or NO.");
            }

            explicitAvailabilityStatuses[availabilityCell.ServiceType] = status;
        }

        var serviceAvailabilities = new List<ValidatedSitesServiceAvailability>();
        foreach (var serviceType in OptionalServiceTypes)
        {
            var hasPrice = priceOptions.Any(priceOption => priceOption.PriceType == serviceType);
            var explicitStatus = explicitAvailabilityStatuses.TryGetValue(serviceType, out var status)
                ? status
                : ServiceAvailabilityStatus.Unknown;

            if (hasPrice && explicitStatus is ServiceAvailabilityStatus.NotAvailable or ServiceAvailabilityStatus.AvailableWithUnknownPrice)
            {
                return (false, new SitesImportError
                {
                    RowNumber = row.RowNumber,
                    Domain = domain,
                    Message = $"{FormatPriceType(serviceType)} availability cannot be YES or NO when a price is provided."
                }, null);
            }

            serviceAvailabilities.Add(new ValidatedSitesServiceAvailability(
                serviceType,
                hasPrice ? ServiceAvailabilityStatus.Available : explicitStatus));
        }

        var data = new ValidatedSitesRow(
            domain,
            dr,
            traffic,
            location,
            row.NumberDFLinks,
            normalizedLanguage,
            TrimToNull(row.Niche),
            TrimToNull(row.Categories),
            sponsoredTag,
            priceOptions,
            serviceAvailabilities);

        return (false, null, data);
    }

    public static bool IsEmptyRow(SitesImportRowDto row)
    {
        return string.IsNullOrWhiteSpace(row.Domain)
               && string.IsNullOrWhiteSpace(row.DRRaw)
               && string.IsNullOrWhiteSpace(row.TrafficRaw)
               && string.IsNullOrWhiteSpace(row.Location)
               && string.IsNullOrWhiteSpace(row.NumberDFLinksRaw)
               && string.IsNullOrWhiteSpace(row.Language)
               && string.IsNullOrWhiteSpace(row.Niche)
               && string.IsNullOrWhiteSpace(row.Categories)
               && string.IsNullOrWhiteSpace(row.SponsoredTag)
               && row.PriceCells.All(cell => string.IsNullOrWhiteSpace(cell.RawValue))
               && row.AvailabilityCells.All(cell => string.IsNullOrWhiteSpace(cell.RawValue));
    }

    private static bool TryParseAvailability(string? rawValue, out ServiceAvailabilityStatus status)
    {
        status = ServiceAvailabilityStatus.Unknown;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        var trimmed = rawValue.Trim();
        if (string.Equals(trimmed, "YES", StringComparison.OrdinalIgnoreCase))
        {
            status = ServiceAvailabilityStatus.AvailableWithUnknownPrice;
            return true;
        }

        if (string.Equals(trimmed, "NO", StringComparison.OrdinalIgnoreCase))
        {
            status = ServiceAvailabilityStatus.NotAvailable;
            return true;
        }

        return false;
    }

    private static (bool IsEmpty, SitesImportError? Error, ValidatedSitesRow? Data) RowError(
        SitesImportRowDto row,
        string domain,
        string field,
        string? rawValue,
        string message)
    {
        return (false, new SitesImportError
        {
            RowNumber = row.RowNumber,
            Domain = domain,
            Field = field,
            RawValue = rawValue,
            Message = message
        }, null);
    }

    private static bool TryParseRequiredDouble(string? rawValue, double? parsedValue, out double value)
    {
        value = parsedValue ?? 0;
        return !string.IsNullOrWhiteSpace(rawValue) && parsedValue.HasValue;
    }

    private static bool TryParseRequiredLong(string? rawValue, long? parsedValue, out long value)
    {
        value = parsedValue ?? 0;
        return !string.IsNullOrWhiteSpace(rawValue) && parsedValue.HasValue;
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatPriceType(PriceType priceType)
        => priceType switch
        {
            PriceType.Casino => ImportConstants.SitesImportColumns.PriceCasino,
            PriceType.Crypto => ImportConstants.SitesImportColumns.PriceCrypto,
            PriceType.LinkInsertion => ImportConstants.SitesImportColumns.PriceLinkInsert,
            PriceType.LinkInsertionCasino => ImportConstants.SitesImportColumns.PriceLinkInsertCasino,
            PriceType.Dating => ImportConstants.SitesImportColumns.PriceDating,
            _ => priceType.ToString()
        };
}
