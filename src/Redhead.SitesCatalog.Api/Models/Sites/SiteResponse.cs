using System.Text.Json.Serialization;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Models.Sites;

/// <summary>
/// Response for a single site
/// </summary>
public class SiteResponse
{
    public string Domain { get; set; } = string.Empty;
    public double DR { get; set; }
    public long Traffic { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? ImportedLocationRaw { get; set; }
    public string? Language { get; set; }
    public string? SponsoredTag { get; set; }
    public decimal? PriceUsd { get; set; }
    public decimal? PriceCasino { get; set; }
    public ServiceAvailabilityStatus PriceCasinoStatus { get; set; }
    public decimal? PriceCrypto { get; set; }
    public ServiceAvailabilityStatus PriceCryptoStatus { get; set; }
    public decimal? PriceLinkInsert { get; set; }
    public ServiceAvailabilityStatus PriceLinkInsertStatus { get; set; }
    public decimal? PriceLinkInsertCasino { get; set; }
    public ServiceAvailabilityStatus PriceLinkInsertCasinoStatus { get; set; }
    public decimal? PriceDating { get; set; }
    public ServiceAvailabilityStatus PriceDatingStatus { get; set; }
    public int? NumberDFLinks { get; set; }
    public TermType? TermType { get; set; }
    public int? TermValue { get; set; }
    public TermUnit? TermUnit { get; set; }
    public string? Niche { get; set; }
    public string[] NicheTokens { get; set; } = [];
    public string? Categories { get; set; }
    public bool IsQuarantined { get; set; }
    public string? QuarantineReason { get; set; }
    public DateTime? QuarantineUpdatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTime UpdatedAtUtc { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CreatedBy { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UpdatedBy { get; set; }
    public DateTime? LastPublishedDate { get; set; }
    public bool LastPublishedDateIsMonthOnly { get; set; }
    public SitePricingResponse Pricing { get; set; } = new();
}

public sealed class SitePriceOptionResponse
{
    public PriceType PriceType { get; init; }
    public string TermKey { get; init; } = string.Empty;
    public TermType? TermType { get; init; }
    public int? TermValue { get; init; }
    public TermUnit? TermUnit { get; init; }
    public string TermLabel { get; init; } = string.Empty;
    public decimal AmountUsd { get; init; }
}

public sealed class SiteServiceAvailabilityResponse
{
    public PriceType ServiceType { get; init; }
    public ServiceAvailabilityStatus Status { get; init; }
}

public sealed class SitePricingResponse
{
    public IReadOnlyList<SitePriceOptionResponse> Prices { get; init; } = [];
    public IReadOnlyList<SiteServiceAvailabilityResponse> ServiceAvailabilities { get; init; } = [];
}
