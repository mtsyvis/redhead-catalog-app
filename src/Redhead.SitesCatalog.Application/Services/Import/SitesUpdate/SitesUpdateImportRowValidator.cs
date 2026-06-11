using System.Globalization;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services.Import.ValueParsers;
using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Services.Import.SitesUpdate;

internal static class SitesUpdateImportRowValidator
{
    public static (bool IsEmpty, SitesImportError? Error, SitesUpdateImportRow? Update) Validate(
        SitesImportRowDto row,
        IReadOnlySet<string> presentColumns)
    {
        if (IsEmptyPartialUpdateRow(row, presentColumns))
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

        var update = new SitesUpdateImportRow(domain, presentColumns);
        var requiredFieldErrors = new List<string>();

        if (presentColumns.Contains(ImportConstants.SitesImportColumns.DR))
        {
            if (!TryParseRequiredDouble(row.DRRaw, out var dr))
            {
                requiredFieldErrors.Add("DR is required.");
            }
            else if (dr < SiteWriteValidator.DrMin || dr > SiteWriteValidator.DrMax)
            {
                requiredFieldErrors.Add($"DR must be between {SiteWriteValidator.DrMin} and {SiteWriteValidator.DrMax}.");
            }
            else
            {
                update = update with { DR = dr };
            }
        }

        if (presentColumns.Contains(ImportConstants.SitesImportColumns.Traffic))
        {
            if (!TryParseRequiredLong(row.TrafficRaw, row.Traffic, out var traffic))
            {
                requiredFieldErrors.Add("Traffic is required.");
            }
            else if (traffic < 0)
            {
                requiredFieldErrors.Add("Traffic must be 0 or greater.");
            }
            else
            {
                update = update with { Traffic = traffic };
            }
        }

        if (presentColumns.Contains(ImportConstants.SitesImportColumns.Location))
        {
            var location = row.Location?.Trim() ?? string.Empty;
            if (location.Length > SiteFieldLimits.LocationMaxLength)
            {
                requiredFieldErrors.Add($"Location must be at most {SiteFieldLimits.LocationMaxLength} characters.");
            }
            else
            {
                update = update with { Location = location };
            }
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

        var priceOperations = new List<SitesUpdatePriceOperation>();
        foreach (var priceCell in row.PriceCells)
        {
            if (string.IsNullOrWhiteSpace(priceCell.RawValue))
            {
                priceOperations.Add(new SitesUpdatePriceOperation(
                    priceCell.Header,
                    priceCell.PriceType,
                    priceCell.TermKey,
                    priceCell.TermType,
                    priceCell.TermValue,
                    priceCell.TermUnit,
                    priceCell.RawValue,
                    null));
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

            priceOperations.Add(new SitesUpdatePriceOperation(
                priceCell.Header,
                priceCell.PriceType,
                priceCell.TermKey,
                priceCell.TermType,
                priceCell.TermValue,
                priceCell.TermUnit,
                priceCell.RawValue,
                amount));
        }

        var availabilityOperations = new List<SitesUpdateAvailabilityOperation>();
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

            availabilityOperations.Add(new SitesUpdateAvailabilityOperation(
                availabilityCell.Header,
                availabilityCell.ServiceType,
                availabilityCell.RawValue,
                status));
        }

        foreach (var availabilityOperation in availabilityOperations)
        {
            if (priceOperations.Any(operation =>
                    operation.PriceType == availabilityOperation.ServiceType
                    && operation.AmountUsd.HasValue))
            {
                return (false, new SitesImportError
                {
                    RowNumber = row.RowNumber,
                    Domain = domain,
                    Message = $"{FormatPriceType(availabilityOperation.ServiceType)} availability cannot be empty, YES, or NO when a price is provided."
                }, null);
            }
        }

        update = update with
        {
            PriceOperations = priceOperations,
            AvailabilityOperations = availabilityOperations
        };

        if (presentColumns.Contains(ImportConstants.SitesImportColumns.NumberDFLinks))
        {
            if (!string.IsNullOrWhiteSpace(row.NumberDFLinksRaw))
            {
                if (!int.TryParse(row.NumberDFLinksRaw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numberDFLinks))
                {
                    return RowError(row, domain, ImportConstants.SitesImportColumns.NumberDFLinks, row.NumberDFLinksRaw, "Invalid NumberDFLinks value.");
                }

                if (numberDFLinks <= 0)
                {
                    return RowError(row, domain, ImportConstants.SitesImportColumns.NumberDFLinks, row.NumberDFLinksRaw, "Number DF Links must be greater than 0.");
                }

                update = update with { NumberDFLinks = numberDFLinks };
            }
            else
            {
                update = update with { NumberDFLinks = null };
            }
        }

        if (presentColumns.Contains(ImportConstants.SitesImportColumns.Language))
        {
            var trimmedLanguage = row.Language?.Trim();
            if (trimmedLanguage is { Length: > SiteFieldLimits.LanguageMaxLength })
            {
                return RowError(row, domain, ImportConstants.SitesImportColumns.Language, row.Language, $"Language must be at most {SiteFieldLimits.LanguageMaxLength} characters.");
            }

            var normalizedLanguage = LanguageNormalizer.Normalize(row.Language);
            if (!string.IsNullOrWhiteSpace(row.Language) && normalizedLanguage is null)
            {
                return RowError(row, domain, ImportConstants.SitesImportColumns.Language, row.Language, "Language must be a two-letter code, UNKNOWN, or MULTI.");
            }

            update = update with { Language = normalizedLanguage };
        }

        if (presentColumns.Contains(ImportConstants.SitesImportColumns.Niche))
        {
            update = update with { Niche = TrimToNull(row.Niche) };
        }

        if (presentColumns.Contains(ImportConstants.SitesImportColumns.Categories))
        {
            update = update with { Categories = TrimToNull(row.Categories) };
        }

        if (presentColumns.Contains(ImportConstants.SitesImportColumns.SponsoredTag))
        {
            var sponsoredTag = TrimToNull(row.SponsoredTag);
            if (sponsoredTag is not null && sponsoredTag.Length > SiteFieldLimits.SponsoredTagMaxLength)
            {
                return RowError(row, domain, ImportConstants.SitesImportColumns.SponsoredTag, row.SponsoredTag, $"Sponsored tag must be at most {SiteFieldLimits.SponsoredTagMaxLength} characters.");
            }

            update = update with { SponsoredTag = sponsoredTag };
        }

        return (false, null, update);
    }

    private static (bool IsEmpty, SitesImportError? Error, SitesUpdateImportRow? Update) RowError(
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

    private static bool IsEmptyPartialUpdateRow(SitesImportRowDto row, IReadOnlySet<string> presentColumns)
    {
        if (!string.IsNullOrWhiteSpace(row.Domain))
        {
            return false;
        }

        return (!presentColumns.Contains(ImportConstants.SitesImportColumns.DR) || string.IsNullOrWhiteSpace(row.DRRaw))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.Traffic) || string.IsNullOrWhiteSpace(row.TrafficRaw))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.Location) || string.IsNullOrWhiteSpace(row.Location))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.NumberDFLinks) || string.IsNullOrWhiteSpace(row.NumberDFLinksRaw))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.Language) || string.IsNullOrWhiteSpace(row.Language))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.Niche) || string.IsNullOrWhiteSpace(row.Niche))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.Categories) || string.IsNullOrWhiteSpace(row.Categories))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.SponsoredTag) || string.IsNullOrWhiteSpace(row.SponsoredTag))
               && row.PriceCells.All(cell => string.IsNullOrWhiteSpace(cell.RawValue))
               && row.AvailabilityCells.All(cell => string.IsNullOrWhiteSpace(cell.RawValue));
    }

    private static bool TryParseRequiredDouble(string? rawValue, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(rawValue)
            || !DecimalParsingHelper.TryParseDecimalFlexible(rawValue, out var parsed))
        {
            return false;
        }

        value = (double)parsed;
        return true;
    }

    private static bool TryParseRequiredLong(string? rawValue, long? parsedValue, out long value)
    {
        value = parsedValue ?? 0;
        return !string.IsNullOrWhiteSpace(rawValue) && parsedValue.HasValue;
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
