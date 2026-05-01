namespace Redhead.SitesCatalog.Application.Models.Import;

/// <summary>
/// One row from a sites import file (CSV)
/// </summary>
public class SitesImportRowDto
{
    public int RowNumber { get; set; }
    public string? Domain { get; set; }
    public string? DRRaw { get; set; }
    public double? DR { get; set; }
    public long? Traffic { get; set; }
    public string? Location { get; set; }
    public string? PriceUsdRaw { get; set; }
    public decimal? PriceUsd { get; set; }
    public string? PriceCasinoRaw { get; set; }
    public string? PriceCryptoRaw { get; set; }
    public string? PriceLinkInsertRaw { get; set; }
    public string? PriceLinkInsertCasinoRaw { get; set; }
    public string? PriceDatingRaw { get; set; }
    public string? NumberDFLinksRaw { get; set; }
    public int? NumberDFLinks { get; set; }
    public string? TermRaw { get; set; }
    public string? Niche { get; set; }
    public string? Categories { get; set; }
    public string? LinkType { get; set; }
    public string? SponsoredTag { get; set; }
}
