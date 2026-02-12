namespace Redhead.SitesCatalog.Application.Models;

/// <summary>
/// Data transfer object for site information
/// </summary>
public class SiteDto
{
    public string Domain { get; set; } = string.Empty;
    public int DR { get; set; }
    public long Traffic { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal PriceUsd { get; set; }
    public decimal? PriceCasino { get; set; }
    public decimal? PriceCrypto { get; set; }
    public decimal? PriceLinkInsert { get; set; }
    public string? Niche { get; set; }
    public string? Categories { get; set; }
    public bool IsQuarantined { get; set; }
    public string? QuarantineReason { get; set; }
    public DateTime? QuarantineUpdatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
