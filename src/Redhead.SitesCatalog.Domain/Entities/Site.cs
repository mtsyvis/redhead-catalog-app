using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Domain.Entities;

public class Site
{
    public string Domain { get; set; } = string.Empty;
    public double DR { get; set; }
    public long Traffic { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal? PriceUsd { get; set; }
    public decimal? PriceCasino { get; set; }
    public ServiceAvailabilityStatus PriceCasinoStatus { get; set; } = ServiceAvailabilityStatus.Unknown;
    public decimal? PriceCrypto { get; set; }
    public ServiceAvailabilityStatus PriceCryptoStatus { get; set; } = ServiceAvailabilityStatus.Unknown;
    public decimal? PriceLinkInsert { get; set; }
    public ServiceAvailabilityStatus PriceLinkInsertStatus { get; set; } = ServiceAvailabilityStatus.Unknown;
    public decimal? PriceLinkInsertCasino { get; set; }
    public ServiceAvailabilityStatus PriceLinkInsertCasinoStatus { get; set; } = ServiceAvailabilityStatus.Unknown;
    public decimal? PriceDating { get; set; }
    public ServiceAvailabilityStatus PriceDatingStatus { get; set; } = ServiceAvailabilityStatus.Unknown;
    public int? NumberDFLinks { get; set; }
    public TermType? TermType { get; set; }
    public int? TermValue { get; set; }
    public TermUnit? TermUnit { get; set; }
    public string? Niche { get; set; }
    public string? Categories { get; set; }
    public string? LinkType { get; set; }
    public string? SponsoredTag { get; set; }
    public bool IsQuarantined { get; set; }
    public string? QuarantineReason { get; set; }
    public DateTime? QuarantineUpdatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? LastPublishedDate { get; set; }
    public bool LastPublishedDateIsMonthOnly { get; set; }
}
