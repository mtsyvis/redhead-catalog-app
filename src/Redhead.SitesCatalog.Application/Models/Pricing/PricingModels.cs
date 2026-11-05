using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Application.Models.Pricing;

public sealed class PricingQuery
{
    public string? Domain { get; set; }
    public bool? IsQuarantined { get; set; }
    public string? TermKey { get; set; }
    public bool NeedsReview { get; set; }
    public PriceType PriceType { get; set; } = PriceType.Main;
    public string Margin { get; set; } = "all";
    public decimal? MinMargin { get; set; }
    public decimal? MaxMargin { get; set; }
    public string SortBy { get; set; } = "domain";
    public PriceType? SortPriceType { get; set; }
    public bool Descending { get; set; }
    public int Page { get; set; } = PaginationDefaults.DefaultPage;
    public int PageSize { get; set; } = PaginationDefaults.DefaultPageSize;
}

public sealed record PricingPage(IReadOnlyList<PricingSiteDto> Items, int TotalCount, int Page, int PageSize);
public sealed record PricingTermDto(string TermKey, string Label);
public sealed record PricingPriceDto(PriceType PriceType, string TermKey, decimal? CatalogPrice,
    decimal? OfferPrice, Guid? OfferId, decimal? Margin);
public sealed record PricingMarketDto(decimal? MinimumUsd, decimal? AverageUsd, decimal? MedianUsd,
    DateTime? CheckedAtUtc, bool IsStale, string? LastError);
public sealed record PricingSiteDto(string Domain, bool IsQuarantined, string Version,
    IReadOnlyList<PricingTermDto> Terms, bool HasInvalidTerms, UpdateSitePricingRequest Pricing,
    IReadOnlyList<PricingPriceDto> Prices, PricingMarketDto? Market);
public sealed class SavePricingRequest
{
    public string Version { get; set; } = string.Empty;
    public UpdateSitePricingRequest? Pricing { get; set; }
}

public sealed class PricingConflictException() : Exception(
    "Catalog prices changed since this dialog was opened. Reload the latest prices before saving.");
