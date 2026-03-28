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

        var linkType = TrimToNull(input.LinkType);
        if (linkType is not null && linkType.Length > SiteFieldLimits.LinkTypeMaxLength)
        {
            Add(errors, "linkType", $"Link type must be at most {SiteFieldLimits.LinkTypeMaxLength} characters.");
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
        else
        {
            Add(errors, "priceUsd", "Price USD is required.");
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
                LinkType = linkType,
                SponsoredTag = sponsoredTag,
                PriceUsd = input.PriceUsd!.Value,
                PriceCasino = normalizedCasino.Price,
                PriceCasinoStatus = normalizedCasino.Status,
                PriceCrypto = normalizedCrypto.Price,
                PriceCryptoStatus = normalizedCrypto.Status,
                PriceLinkInsert = normalizedLinkInsert.Price,
                PriceLinkInsertStatus = normalizedLinkInsert.Status,
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
