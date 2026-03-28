using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Models.Sites;

/// <summary>
/// Validates UpdateSiteRequest. Returns field-level errors for 400 response.
/// </summary>
public static class UpdateSiteRequestValidator
{
    public const double DrMin = 0;
    public const double DrMax = 100;
    public const int LocationMaxLength = 100;

    /// <summary>
    /// Validates request. Returns null if valid; otherwise dictionary of field name -> error messages.
    /// </summary>
    public static Dictionary<string, string[]>? Validate(UpdateSiteRequest request)
    {
        if (request == null)
        {
            return new Dictionary<string, string[]>
            {
                [""] = ["Request body is required."]
            };
        }

        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (request.DR is double dr)
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

        if (request.Traffic is long traffic)
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

        var location = request.Location?.Trim() ?? string.Empty;
        if (location.Length == 0)
        {
            Add(errors, "location", "Location is required.");
        }
        else if (location.Length > LocationMaxLength)
        {
            Add(errors, "location", $"Location must be at most {LocationMaxLength} characters.");
        }

        if (request.LinkType != null && request.LinkType.Trim().Length > LocationMaxLength)
        {
            Add(errors, "linkType", $"Link type must be at most {LocationMaxLength} characters.");
        }

        if (request.SponsoredTag != null && request.SponsoredTag.Trim().Length > LocationMaxLength)
        {
            Add(errors, "sponsoredTag", $"Sponsored tag must be at most {LocationMaxLength} characters.");
        }

        if (request.PriceUsd is decimal priceUsd)
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

        if (request.PriceCasino is decimal pc && pc < 0)
        {
            Add(errors, "priceCasino", "Price Casino must be 0 or greater.");
        }

        if (request.PriceCrypto is decimal pcr && pcr < 0)
        {
            Add(errors, "priceCrypto", "Price Crypto must be 0 or greater.");
        }

        if (request.PriceLinkInsert is decimal pli && pli < 0)
        {
            Add(errors, "priceLinkInsert", "Price Link Insert must be 0 or greater.");
        }

        ValidateOptionalServicePair(
            errors,
            "priceCasino",
            "priceCasinoStatus",
            request.PriceCasino,
            request.PriceCasinoStatus,
            "Price Casino");
        ValidateOptionalServicePair(
            errors,
            "priceCrypto",
            "priceCryptoStatus",
            request.PriceCrypto,
            request.PriceCryptoStatus,
            "Price Crypto");
        ValidateOptionalServicePair(
            errors,
            "priceLinkInsert",
            "priceLinkInsertStatus",
            request.PriceLinkInsert,
            request.PriceLinkInsertStatus,
            "Price Link Insert");

        if (request.QuarantineReason != null && request.QuarantineReason.Trim().Length > 1000)
        {
            Add(errors, "quarantineReason", "Quarantine reason must be at most 1000 characters.");
        }

        if (errors.Count == 0)
        {
            return null;
        }

        return errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    public static ServiceAvailabilityStatus ResolveStatusOrInfer(decimal? price, ServiceAvailabilityStatus? providedStatus)
    {
        if (providedStatus.HasValue)
        {
            return providedStatus.Value;
        }

        return price.HasValue
            ? ServiceAvailabilityStatus.Available
            : ServiceAvailabilityStatus.Unknown;
    }

    private static void ValidateOptionalServicePair(
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
            }
            else if (price.Value < 0)
            {
                Add(errors, priceFieldKey, $"{displayName} must be 0 or greater when status is Available.");
            }

            return;
        }

        if (status != ServiceAvailabilityStatus.Unknown && status != ServiceAvailabilityStatus.NotAvailable)
        {
            Add(errors, statusFieldKey, "Invalid availability status value.");
            return;
        }

        if (price.HasValue)
        {
            Add(errors, priceFieldKey, $"{displayName} must be empty when status is {status}.");
        }
    }

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
