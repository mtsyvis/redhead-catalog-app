namespace Redhead.SitesCatalog.Domain.Entities;

public class Site
{
    public string Domain { get; set; } = string.Empty;
    public double DR { get; set; }
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
    public DateTime? LastPublishedDate { get; set; }
    public bool LastPublishedDateIsMonthOnly { get; set; }
}
