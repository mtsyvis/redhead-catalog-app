using System.Globalization;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services.Parsers;
using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Application.Services.UpdateImport;

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
            if (location.Length == 0)
            {
                requiredFieldErrors.Add("Location is required.");
            }
            else if (location.Length > SiteFieldLimits.LocationMaxLength)
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

        if (presentColumns.Contains(ImportConstants.SitesImportColumns.PriceUsd))
        {
            if (!string.IsNullOrWhiteSpace(row.PriceUsdRaw))
            {
                if (!DecimalParsingHelper.TryParseDecimalFlexible(row.PriceUsdRaw, out var priceUsd))
                {
                    return RowError(row, domain, ImportConstants.SitesImportColumns.PriceUsd, row.PriceUsdRaw, "Invalid PriceUsd value.");
                }

                if (priceUsd <= 0)
                {
                    return RowError(row, domain, ImportConstants.SitesImportColumns.PriceUsd, row.PriceUsdRaw, "Price USD must be greater than 0 or empty.");
                }

                update = update with { PriceUsd = priceUsd };
            }
            else
            {
                update = update with { PriceUsd = null };
            }
        }

        if (!TryApplyOptionalService(row, domain, presentColumns, ImportConstants.SitesImportColumns.PriceCasino, row.PriceCasinoRaw, update, out update, out var optionalError)
            || !TryApplyOptionalService(row, domain, presentColumns, ImportConstants.SitesImportColumns.PriceCrypto, row.PriceCryptoRaw, update, out update, out optionalError)
            || !TryApplyOptionalService(row, domain, presentColumns, ImportConstants.SitesImportColumns.PriceLinkInsert, row.PriceLinkInsertRaw, update, out update, out optionalError)
            || !TryApplyOptionalService(row, domain, presentColumns, ImportConstants.SitesImportColumns.PriceLinkInsertCasino, row.PriceLinkInsertCasinoRaw, update, out update, out optionalError)
            || !TryApplyOptionalService(row, domain, presentColumns, ImportConstants.SitesImportColumns.PriceDating, row.PriceDatingRaw, update, out update, out optionalError))
        {
            return (false, optionalError, null);
        }

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

        if (presentColumns.Contains(ImportConstants.SitesImportColumns.Term))
        {
            var termParseResult = TermValueParser.Parse(row.TermRaw);
            if (!termParseResult.IsValid)
            {
                return RowError(row, domain, ImportConstants.SitesImportColumns.Term, row.TermRaw, termParseResult.ErrorMessage ?? "Invalid Term value.");
            }

            update = update with
            {
                TermType = termParseResult.TermType,
                TermValue = termParseResult.TermValue,
                TermUnit = termParseResult.TermUnit
            };
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

    private static bool TryApplyOptionalService(
        SitesImportRowDto row,
        string domain,
        IReadOnlySet<string> presentColumns,
        string columnName,
        string? rawValue,
        SitesUpdateImportRow current,
        out SitesUpdateImportRow updated,
        out SitesImportError? error)
    {
        updated = current;
        error = null;

        if (!presentColumns.Contains(columnName))
        {
            return true;
        }

        var parseResult = OptionalServiceValueParser.Parse(rawValue);
        if (!parseResult.IsValid)
        {
            error = new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Field = columnName,
                RawValue = rawValue,
                Message = parseResult.ErrorMessage ?? $"Invalid {columnName} value."
            };
            return false;
        }

        updated = columnName switch
        {
            ImportConstants.SitesImportColumns.PriceCasino => current with
            {
                PriceCasino = parseResult.Price,
                PriceCasinoStatus = parseResult.Status
            },
            ImportConstants.SitesImportColumns.PriceCrypto => current with
            {
                PriceCrypto = parseResult.Price,
                PriceCryptoStatus = parseResult.Status
            },
            ImportConstants.SitesImportColumns.PriceLinkInsert => current with
            {
                PriceLinkInsert = parseResult.Price,
                PriceLinkInsertStatus = parseResult.Status
            },
            ImportConstants.SitesImportColumns.PriceLinkInsertCasino => current with
            {
                PriceLinkInsertCasino = parseResult.Price,
                PriceLinkInsertCasinoStatus = parseResult.Status
            },
            ImportConstants.SitesImportColumns.PriceDating => current with
            {
                PriceDating = parseResult.Price,
                PriceDatingStatus = parseResult.Status
            },
            _ => current
        };

        return true;
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
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.PriceUsd) || string.IsNullOrWhiteSpace(row.PriceUsdRaw))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.PriceCasino) || string.IsNullOrWhiteSpace(row.PriceCasinoRaw))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.PriceCrypto) || string.IsNullOrWhiteSpace(row.PriceCryptoRaw))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.PriceLinkInsert) || string.IsNullOrWhiteSpace(row.PriceLinkInsertRaw))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.PriceLinkInsertCasino) || string.IsNullOrWhiteSpace(row.PriceLinkInsertCasinoRaw))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.PriceDating) || string.IsNullOrWhiteSpace(row.PriceDatingRaw))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.NumberDFLinks) || string.IsNullOrWhiteSpace(row.NumberDFLinksRaw))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.Term) || string.IsNullOrWhiteSpace(row.TermRaw))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.Language) || string.IsNullOrWhiteSpace(row.Language))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.Niche) || string.IsNullOrWhiteSpace(row.Niche))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.Categories) || string.IsNullOrWhiteSpace(row.Categories))
               && (!presentColumns.Contains(ImportConstants.SitesImportColumns.SponsoredTag) || string.IsNullOrWhiteSpace(row.SponsoredTag));
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
}
