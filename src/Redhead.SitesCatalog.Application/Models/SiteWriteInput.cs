using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Models;

/// <summary>
/// Input payload for site write validation and normalization.
/// Nullable fields represent raw user input before validation.
/// </summary>
public class SiteWriteInput
{
    public double? DR { get; set; }
    public long? Traffic { get; set; }
    public string? Location { get; set; }
    public string? Language { get; set; }
    public string? SponsoredTag { get; set; }

    // Legacy flat pricing fields kept temporarily while the update flow supports
    // both the old contract and the new term-aware Pricing payload.
    // Remove these once all callers have migrated to Pricing.
    public decimal? PriceUsd { get; set; }
    public decimal? PriceCasino { get; set; }
    public ServiceAvailabilityStatus? PriceCasinoStatus { get; set; }
    public decimal? PriceCrypto { get; set; }
    public ServiceAvailabilityStatus? PriceCryptoStatus { get; set; }
    public decimal? PriceLinkInsert { get; set; }
    public ServiceAvailabilityStatus? PriceLinkInsertStatus { get; set; }
    public decimal? PriceLinkInsertCasino { get; set; }
    public ServiceAvailabilityStatus? PriceLinkInsertCasinoStatus { get; set; }
    public decimal? PriceDating { get; set; }
    public ServiceAvailabilityStatus? PriceDatingStatus { get; set; }
    public int? NumberDFLinks { get; set; }

    // Legacy site-level term fields used only by the old flat pricing model.
    // Remove these after the old pricing contract is deleted.
    public TermType? TermType { get; set; }
    public int? TermValue { get; set; }
    public TermUnit? TermUnit { get; set; }
    public string? Niche { get; set; }
    public string? Categories { get; set; }

    // New term-aware pricing payload. When present, it is the authoritative
    // source for pricing validation and persistence.
    public UpdateSitePricingRequest? Pricing { get; set; }
    public bool IsQuarantined { get; set; }
    public string? QuarantineReason { get; set; }
}
