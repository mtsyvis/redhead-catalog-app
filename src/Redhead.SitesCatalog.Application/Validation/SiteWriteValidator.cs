using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Validation;

public static class SiteWriteValidator
{
    public const double DrMin = 0;
    public const double DrMax = 100;

    private static readonly PriceType[] OptionalServiceTypes =
    [
        PriceType.Casino,
        PriceType.Crypto,
        PriceType.LinkInsertion,
        PriceType.LinkInsertionCasino,
        PriceType.Dating
    ];

    public static SiteWriteValidationResult ValidateAndNormalize(
        SiteWriteInput? input,
        SiteWriteValidationContext context = SiteWriteValidationContext.ManualForm)
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
        if (location.Length > SiteFieldLimits.LocationMaxLength)
        {
            Add(errors, "location", $"Location must be at most {SiteFieldLimits.LocationMaxLength} characters.");
        }

        var language = LanguageNormalizer.Normalize(input.Language);
        var trimmedLanguage = input.Language?.Trim();
        if (trimmedLanguage is { Length: > SiteFieldLimits.LanguageMaxLength })
        {
            Add(errors, "language", $"Language must be at most {SiteFieldLimits.LanguageMaxLength} characters.");
        }
        else if (!string.IsNullOrWhiteSpace(input.Language) && language is null)
        {
            Add(errors, "language", "Language must be a two-letter code, UNKNOWN, or MULTI.");
        }

        var sponsoredTag = TrimToNull(input.SponsoredTag);
        if (sponsoredTag is not null && sponsoredTag.Length > SiteFieldLimits.SponsoredTagMaxLength)
        {
            Add(errors, "sponsoredTag", $"Sponsored tag must be at most {SiteFieldLimits.SponsoredTagMaxLength} characters.");
        }

        decimal? normalizedPriceUsd;
        (decimal? Price, ServiceAvailabilityStatus Status) normalizedCasino;
        (decimal? Price, ServiceAvailabilityStatus Status) normalizedCrypto;
        (decimal? Price, ServiceAvailabilityStatus Status) normalizedLinkInsert;
        (decimal? Price, ServiceAvailabilityStatus Status) normalizedLinkInsertCasino;
        (decimal? Price, ServiceAvailabilityStatus Status) normalizedDating;
        UpdateSitePricingRequest? normalizedPricing = null;

        if (input.Pricing is null)
        {
            // Legacy flat pricing validation path.
            // Kept temporarily while some callers still send old pricing fields.
            normalizedPriceUsd = input.PriceUsd;
            if (input.PriceUsd is decimal priceUsd && priceUsd <= 0)
            {
                Add(errors, "priceUsd", "Price USD must be greater than 0 or empty.");
            }

            normalizedCasino = NormalizeOptionalServicePair(
                errors,
                "priceCasino",
                "priceCasinoStatus",
                input.PriceCasino,
                input.PriceCasinoStatus,
                "Price Casino");
            normalizedCrypto = NormalizeOptionalServicePair(
                errors,
                "priceCrypto",
                "priceCryptoStatus",
                input.PriceCrypto,
                input.PriceCryptoStatus,
                "Price Crypto");
            normalizedLinkInsert = NormalizeOptionalServicePair(
                errors,
                "priceLinkInsert",
                "priceLinkInsertStatus",
                input.PriceLinkInsert,
                input.PriceLinkInsertStatus,
                "Price Link Insert");
            normalizedLinkInsertCasino = NormalizeOptionalServicePair(
                errors,
                "priceLinkInsertCasino",
                "priceLinkInsertCasinoStatus",
                input.PriceLinkInsertCasino,
                input.PriceLinkInsertCasinoStatus,
                "Price Link Insert Casino");
            normalizedDating = NormalizeOptionalServicePair(
                errors,
                "priceDating",
                "priceDatingStatus",
                input.PriceDating,
                input.PriceDatingStatus,
                "Price Dating");

            if (context == SiteWriteValidationContext.ManualForm
                && !SitePriceValidationHelper.HasAnyNumericPrice(new SitePriceState(
                        normalizedPriceUsd,
                        normalizedCasino.Price,
                        normalizedCasino.Status,
                        normalizedCrypto.Price,
                        normalizedCrypto.Status,
                        normalizedLinkInsert.Price,
                        normalizedLinkInsert.Status,
                        normalizedLinkInsertCasino.Price,
                        normalizedLinkInsertCasino.Status,
                        normalizedDating.Price,
                        normalizedDating.Status)))
            {
                Add(errors, string.Empty, "At least one numeric price is required.");
            }
        }
        else
        {
            // New term-aware pricing validation path.
            // When Pricing is present, it becomes the authoritative pricing payload.
            normalizedPriceUsd = input.PriceUsd;
            normalizedCasino = (input.PriceCasino, input.PriceCasinoStatus ?? ServiceAvailabilityStatus.Unknown);
            normalizedCrypto = (input.PriceCrypto, input.PriceCryptoStatus ?? ServiceAvailabilityStatus.Unknown);
            normalizedLinkInsert = (input.PriceLinkInsert, input.PriceLinkInsertStatus ?? ServiceAvailabilityStatus.Unknown);
            normalizedLinkInsertCasino = (input.PriceLinkInsertCasino, input.PriceLinkInsertCasinoStatus ?? ServiceAvailabilityStatus.Unknown);
            normalizedDating = (input.PriceDating, input.PriceDatingStatus ?? ServiceAvailabilityStatus.Unknown);
            normalizedPricing = NormalizePricing(errors, input.Pricing);
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
                Language = language,
                SponsoredTag = sponsoredTag,
                PriceUsd = normalizedPriceUsd,
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
                Pricing = normalizedPricing,
                IsQuarantined = input.IsQuarantined,
                QuarantineReason = quarantineReason
            }
        };
    }

    private static UpdateSitePricingRequest NormalizePricing(
        Dictionary<string, List<string>> errors,
        UpdateSitePricingRequest pricing)
    {
        var normalizedPrices = new List<UpdateSitePriceOptionRequest>();
        var seenPriceTerms = new HashSet<(PriceType PriceType, string TermKey)>();

        var rawPrices = pricing.Prices ?? [];
        for (var index = 0; index < rawPrices.Count; index++)
        {
            var price = rawPrices[index];
            var prefix = $"pricing.prices[{index}]";

            if (!IsSupportedPriceType(price.PriceType))
            {
                Add(errors, $"{prefix}.priceType", "Invalid price type value.");
                continue;
            }

            if (!PricingTerm.TryCreate(
                    price.TermKey,
                    price.TermType,
                    price.TermValue,
                    price.TermUnit,
                    out var term))
            {
                Add(errors, $"{prefix}.termKey", "Term key must match term type, term value, and term unit.");
                continue;
            }

            if (price.AmountUsd <= 0)
            {
                Add(errors, $"{prefix}.amountUsd", "Price amount must be greater than 0.");
            }

            if (!seenPriceTerms.Add((price.PriceType, term.TermKey)))
            {
                Add(errors, $"{prefix}.termKey", "Duplicate price type and term key combination is not allowed.");
            }

            normalizedPrices.Add(new UpdateSitePriceOptionRequest
            {
                PriceType = price.PriceType,
                TermKey = term.TermKey,
                TermType = term.TermType,
                TermValue = term.TermValue,
                TermUnit = term.TermUnit,
                AmountUsd = price.AmountUsd
            });
        }

        var explicitStatuses = new Dictionary<PriceType, (ServiceAvailabilityStatus Status, string StatusField)>();
        var rawAvailabilities = pricing.ServiceAvailabilities ?? [];
        for (var index = 0; index < rawAvailabilities.Count; index++)
        {
            var availability = rawAvailabilities[index];
            var prefix = $"pricing.serviceAvailabilities[{index}]";

            if (availability.ServiceType == PriceType.Main)
            {
                Add(errors, $"{prefix}.serviceType", "Main pricing must not include a service availability row.");
                continue;
            }

            if (!IsOptionalServiceType(availability.ServiceType))
            {
                Add(errors, $"{prefix}.serviceType", "Invalid service type value.");
                continue;
            }

            if (!Enum.IsDefined(availability.Status))
            {
                Add(errors, $"{prefix}.status", "Invalid availability status value.");
                continue;
            }

            if (!explicitStatuses.TryAdd(availability.ServiceType, (availability.Status, $"{prefix}.status")))
            {
                Add(errors, $"{prefix}.serviceType", "Duplicate service availability rows are not allowed.");
            }
        }

        foreach (var serviceType in OptionalServiceTypes)
        {
            var hasPrices = normalizedPrices.Any(price => price.PriceType == serviceType);
            if (!explicitStatuses.TryGetValue(serviceType, out var explicitStatus))
            {
                continue;
            }

            switch (explicitStatus.Status)
            {
                case ServiceAvailabilityStatus.Available:
                    if (!hasPrices)
                    {
                        Add(errors, explicitStatus.StatusField, "Available status requires at least one price for that service.");
                    }

                    break;
                case ServiceAvailabilityStatus.Unknown:
                case ServiceAvailabilityStatus.NotAvailable:
                case ServiceAvailabilityStatus.AvailableWithUnknownPrice:
                    if (hasPrices)
                    {
                        Add(errors, explicitStatus.StatusField, $"Status {explicitStatus.Status} cannot be submitted with prices for the same service.");
                    }

                    break;
                default:
                    Add(errors, explicitStatus.StatusField, "Invalid availability status value.");
                    break;
            }
        }

        var normalizedAvailabilities = new List<UpdateSiteServiceAvailabilityRequest>();
        foreach (var serviceType in OptionalServiceTypes)
        {
            var hasPrices = normalizedPrices.Any(price => price.PriceType == serviceType);
            if (hasPrices)
            {
                normalizedAvailabilities.Add(new UpdateSiteServiceAvailabilityRequest
                {
                    ServiceType = serviceType,
                    Status = ServiceAvailabilityStatus.Available
                });

                continue;
            }

            if (explicitStatuses.TryGetValue(serviceType, out var explicitStatus))
            {
                normalizedAvailabilities.Add(new UpdateSiteServiceAvailabilityRequest
                {
                    ServiceType = serviceType,
                    Status = explicitStatus.Status
                });
            }
        }

        return new UpdateSitePricingRequest
        {
            Prices = normalizedPrices,
            ServiceAvailabilities = normalizedAvailabilities
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

            if (price.Value <= 0)
            {
                Add(errors, priceFieldKey, $"{displayName} must be greater than 0 when status is Available.");
            }

            return (price, status);
        }

        if (status == ServiceAvailabilityStatus.AvailableWithUnknownPrice)
        {
            if (price.HasValue)
            {
                Add(errors, priceFieldKey, $"{displayName} must be empty when status is AvailableWithUnknownPrice.");
            }

            return (null, status);
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

    private static bool IsSupportedPriceType(PriceType priceType)
        => priceType == PriceType.Main || IsOptionalServiceType(priceType);

    private static bool IsOptionalServiceType(PriceType priceType)
        => Array.IndexOf(OptionalServiceTypes, priceType) >= 0;

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
