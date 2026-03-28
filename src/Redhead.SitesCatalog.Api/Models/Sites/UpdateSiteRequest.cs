using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Models.Sites;

/// <summary>
/// Request to update site fields (Admin/SuperAdmin). Domain is read-only (route parameter).
/// </summary>
public class UpdateSiteRequest
{
    public double? DR { get; set; }
    public long? Traffic { get; set; }
    public string? Location { get; set; }
    public string? LinkType { get; set; }
    public string? SponsoredTag { get; set; }
    public decimal? PriceUsd { get; set; }
    public decimal? PriceCasino { get; set; }
    public ServiceAvailabilityStatus? PriceCasinoStatus { get; set; }
    public decimal? PriceCrypto { get; set; }
    public ServiceAvailabilityStatus? PriceCryptoStatus { get; set; }
    public decimal? PriceLinkInsert { get; set; }
    public ServiceAvailabilityStatus? PriceLinkInsertStatus { get; set; }
    public string? Niche { get; set; }
    public string? Categories { get; set; }
    public bool IsQuarantined { get; set; }
    public string? QuarantineReason { get; set; }
}
