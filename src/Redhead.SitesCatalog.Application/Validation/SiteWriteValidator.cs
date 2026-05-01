using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Validation;

public static class SiteWriteValidator
{
    public const double DrMin = 0;
    public const double DrMax = 100;

    public static SiteWriteValidationResult ValidateAndNormalize(SiteWriteInput? input)
    {
        if (input == null)
        {
            return new SiteWriteValidationResult
            {
                FieldErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [""] = ["Request body is required."]
                }
            };
        }

        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (input.DR is double dr)
        {
            if (dr < DrMin || dr > DrMax)
            {
                Add(errors, "dr", $"DR must be between {DrMin} and {DrMax}.");
            }
        }
        else
        {
            Add(errors, "dr", "DR is required.");
        }

        if (input.Traffic is long traffic)
        {
            if (traffic < 0)
            {
                Add(errors, "traffic", "Traffic must be 0 or greater.");
            }
        }
        else
        {
            Add(errors, "traffic", "Traffic is required.");
        }

        var location = input.Location?.Trim() ?? string.Empty;
        if (location.Length == 0)
        {
            Add(errors, "location", "Location is required.");
        }
        else if (location.Length > SiteFieldLimits.LocationMaxLength)
        {
            Add(errors, "location", $"Location must be at most {SiteFieldLimits.LocationMaxLength} characters.");
        }

        var sponsoredTag = TrimToNull(input.SponsoredTag);
        if (sponsoredTag is not null && sponsoredTag.Length > SiteFieldLimits.SponsoredTagMaxLength)
        {
            Add(errors, "sponsoredTag", $"Sponsored tag must be at most {SiteFieldLimits.SponsoredTagMaxLength} characters.");
        }

        if (input.PriceUsd is decimal priceUsd)
        {
            if (priceUsd < 0)
            {
                Add(errors, "priceUsd", "Price USD must be 0 or greater.");
            }
        }

        var normalizedCasino = NormalizeOptionalServicePair(
            errors,
            "priceCasino",
            "priceCasinoStatus",
            input.PriceCasino,
            input.PriceCasinoStatus,
            "Price Casino");
        var normalizedCrypto = NormalizeOptionalServicePair(
            errors,
            "priceCrypto",
            "priceCryptoStatus",
            input.PriceCrypto,
            input.PriceCryptoStatus,
            "Price Crypto");
        var normalizedLinkInsert = NormalizeOptionalServicePair(
            errors,
            "priceLinkInsert",
            "priceLinkInsertStatus",
            input.PriceLinkInsert,
            input.PriceLinkInsertStatus,
            "Price Link Insert");
        var normalizedLinkInsertCasino = NormalizeOptionalServicePair(
            errors,
            "priceLinkInsertCasino",
            "priceLinkInsertCasinoStatus",
            input.PriceLinkInsertCasino,
            input.PriceLinkInsertCasinoStatus,
            "Price Link Insert Casino");
        var normalizedDating = NormalizeOptionalServicePair(
            errors,
            "priceDating",
            "priceDatingStatus",
            input.PriceDating,
            input.PriceDatingStatus,
            "Price Dating");

        if (!HasAnyNumericPrice(
                input.PriceUsd,
                normalizedCasino.Price,
                normalizedCasino.Status,
                normalizedCrypto.Price,
                normalizedCrypto.Status,
                normalizedLinkInsert.Price,
                normalizedLinkInsert.Status,
                normalizedLinkInsertCasino.Price,
                normalizedLinkInsertCasino.Status,
                normalizedDating.Price,
                normalizedDating.Status))
        {
            Add(errors, string.Empty, "At least one numeric price is required.");
        }

        if (input.NumberDFLinks is <= 0)
        {
            Add(errors, "numberDFLinks", "Number DF Links must be greater than 0.");
        }

        ValidateTerm(errors, input.TermType, input.TermValue, input.TermUnit);

        var niche = TrimToNull(input.Niche);
        var categories = TrimToNull(input.Categories);
        var quarantineReason = input.IsQuarantined ? TrimToNull(input.QuarantineReason) : null;
        if (quarantineReason is not null && quarantineReason.Length > SiteFieldLimits.QuarantineReasonMaxLength)
        {
            Add(
                errors,
                "quarantineReason",
                $"Quarantine reason must be at most {SiteFieldLimits.QuarantineReasonMaxLength} characters.");
        }

        if (errors.Count > 0)
        {
            return new SiteWriteValidationResult
            {
                FieldErrors = errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.OrdinalIgnoreCase)
            };
        }

        return new SiteWriteValidationResult
        {
            NormalizedRequest = new UpdateSiteRequest
            {
                DR = input.DR!.Value,
                Traffic = input.Traffic!.Value,
                Location = location,
                SponsoredTag = sponsoredTag,
                PriceUsd = input.PriceUsd,
                PriceCasino = normalizedCasino.Price,
                PriceCasinoStatus = normalizedCasino.Status,
                PriceCrypto = normalizedCrypto.Price,
                PriceCryptoStatus = normalizedCrypto.Status,
                PriceLinkInsert = normalizedLinkInsert.Price,
                PriceLinkInsertStatus = normalizedLinkInsert.Status,
                PriceLinkInsertCasino = normalizedLinkInsertCasino.Price,
                PriceLinkInsertCasinoStatus = normalizedLinkInsertCasino.Status,
                PriceDating = normalizedDating.Price,
                PriceDatingStatus = normalizedDating.Status,
                NumberDFLinks = input.NumberDFLinks,
                TermType = input.TermType,
                TermValue = input.TermValue,
                TermUnit = input.TermUnit,
                Niche = niche,
                Categories = categories,
                IsQuarantined = input.IsQuarantined,
                QuarantineReason = quarantineReason
            }
        };
    }

    private static (decimal? Price, ServiceAvailabilityStatus Status) NormalizeOptionalServicePair(
        Dictionary<string, List<string>> errors,
        string priceFieldKey,
        string statusFieldKey,
        decimal? price,
        ServiceAvailabilityStatus? providedStatus,
        string displayName)
    {
        var status = ResolveStatusOrInfer(price, providedStatus);

        if (status == ServiceAvailabilityStatus.Available)
        {
            if (!price.HasValue)
            {
                Add(errors, priceFieldKey, $"{displayName} is required when status is Available.");
                return (null, status);
            }

            if (price.Value < 0)
            {
                Add(errors, priceFieldKey, $"{displayName} must be 0 or greater when status is Available.");
            }

            return (price, status);
        }

        if (status != ServiceAvailabilityStatus.Unknown && status != ServiceAvailabilityStatus.NotAvailable)
        {
            Add(errors, statusFieldKey, "Invalid availability status value.");
            return (null, ServiceAvailabilityStatus.Unknown);
        }

        if (price.HasValue)
        {
            Add(errors, priceFieldKey, $"{displayName} must be empty when status is {status}.");
        }

        return (null, status);
    }

    private static ServiceAvailabilityStatus ResolveStatusOrInfer(decimal? price, ServiceAvailabilityStatus? providedStatus)
    {
        if (providedStatus.HasValue)
        {
            return providedStatus.Value;
        }

        return price.HasValue
            ? ServiceAvailabilityStatus.Available
            : ServiceAvailabilityStatus.Unknown;
    }

    private static bool HasAnyNumericPrice(
        decimal? priceUsd,
        decimal? casinoPrice,
        ServiceAvailabilityStatus casinoStatus,
        decimal? cryptoPrice,
        ServiceAvailabilityStatus cryptoStatus,
        decimal? linkInsertPrice,
        ServiceAvailabilityStatus linkInsertStatus,
        decimal? linkInsertCasinoPrice,
        ServiceAvailabilityStatus linkInsertCasinoStatus,
        decimal? datingPrice,
        ServiceAvailabilityStatus datingStatus)
    {
        if (priceUsd.HasValue && priceUsd.Value >= 0)
        {
            return true;
        }

        if (casinoStatus == ServiceAvailabilityStatus.Available && casinoPrice.HasValue && casinoPrice.Value >= 0)
        {
            return true;
        }

        if (cryptoStatus == ServiceAvailabilityStatus.Available && cryptoPrice.HasValue && cryptoPrice.Value >= 0)
        {
            return true;
        }

        if (linkInsertStatus == ServiceAvailabilityStatus.Available && linkInsertPrice.HasValue && linkInsertPrice.Value >= 0)
        {
            return true;
        }

        if (linkInsertCasinoStatus == ServiceAvailabilityStatus.Available && linkInsertCasinoPrice.HasValue && linkInsertCasinoPrice.Value >= 0)
        {
            return true;
        }

        if (datingStatus == ServiceAvailabilityStatus.Available && datingPrice.HasValue && datingPrice.Value >= 0)
        {
            return true;
        }

        return false;
    }

    private static void ValidateTerm(
        Dictionary<string, List<string>> errors,
        TermType? termType,
        int? termValue,
        TermUnit? termUnit)
    {
        if (termType is null)
        {
            if (termValue is not null)
            {
                Add(errors, "termValue", "Term value must be empty when term type is empty.");
            }

            if (termUnit is not null)
            {
                Add(errors, "termUnit", "Term unit must be empty when term type is empty.");
            }

            return;
        }

        if (termType == TermType.Permanent)
        {
            if (termValue is not null)
            {
                Add(errors, "termValue", "Term value must be empty when term type is Permanent.");
            }

            if (termUnit is not null)
            {
                Add(errors, "termUnit", "Term unit must be empty when term type is Permanent.");
            }

            return;
        }

        if (termType == TermType.Finite)
        {
            if (termValue is null or <= 0)
            {
                Add(errors, "termValue", "Term value must be greater than 0 when term type is Finite.");
            }

            if (termUnit != TermUnit.Year)
            {
                Add(errors, "termUnit", "Term unit must be Year when term type is Finite.");
            }

            return;
        }

        Add(errors, "termType", "Invalid term type value.");
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Add(Dictionary<string, List<string>> dict, string key, string message)
    {
        if (!dict.TryGetValue(key, out var list))
        {
            list = [];
            dict[key] = list;
        }

        list.Add(message);
    }
}
