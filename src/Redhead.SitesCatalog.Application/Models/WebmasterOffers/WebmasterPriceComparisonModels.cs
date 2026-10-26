using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Models.WebmasterOffers;

public sealed record WebmasterPriceComparisonResult(
    string Domain,
    bool SiteFound,
    string? DefaultTermKey,
    IReadOnlyList<WebmasterComparisonTermDto> Terms,
    IReadOnlyList<WebmasterComparisonOfferDto> Offers);

public sealed record WebmasterComparisonTermDto(
    string TermKey,
    string Label,
    int ComparableOfferCount,
    IReadOnlyList<WebmasterComparisonCatalogPriceDto> CatalogPrices);

public sealed record WebmasterComparisonCatalogPriceDto(WebmasterOfferPriceType PriceType, decimal? AmountUsd);

public sealed record WebmasterComparisonOfferDto(
    Guid Id,
    string? PrimaryEmail,
    SiteWebmasterOfferStatus Status,
    string TermLabel,
    IReadOnlyList<WebmasterComparisonPriceDto> Prices);

public sealed record WebmasterComparisonPriceDto(
    WebmasterOfferPriceType PriceType,
    ServiceAvailabilityStatus AvailabilityStatus,
    decimal? WebmasterPriceUsd,
    string? TermKey,
    string TermLabel,
    IReadOnlyList<WebmasterComparisonMarginDto> Margins);

public sealed record WebmasterComparisonMarginDto(string TermKey, decimal? AmountUsd, string? UnavailableReason);
