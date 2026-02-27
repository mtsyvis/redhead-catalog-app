namespace Redhead.SitesCatalog.Api.Models.Sites;

/// <summary>
/// Request to update site fields (Admin/SuperAdmin). Domain is read-only (route parameter).
/// </summary>
public class UpdateSiteRequest
{
    public double? DR { get; set; }
    public long? Traffic { get; set; }
    public string? Location { get; set; }
    public decimal? PriceUsd { get; set; }
    public decimal? PriceCasino { get; set; }
    public decimal? PriceCrypto { get; set; }
    public decimal? PriceLinkInsert { get; set; }
    public string? Niche { get; set; }
    public string? Categories { get; set; }
    public bool IsQuarantined { get; set; }
    public string? QuarantineReason { get; set; }
}
