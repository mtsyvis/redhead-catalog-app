namespace Redhead.SitesCatalog.Application.Models.Import;

/// <summary>
/// One row from a sites import file (CSV)
/// </summary>
public class SitesImportRowDto
{
    public int RowNumber { get; set; }
    public string? Domain { get; set; }
    public int? DR { get; set; }
    public long? Traffic { get; set; }
    public string? Location { get; set; }
    public decimal? PriceUsd { get; set; }
    public decimal? PriceCasino { get; set; }
    public decimal? PriceCrypto { get; set; }
    public decimal? PriceLinkInsert { get; set; }
    public string? Niche { get; set; }
    public string? Categories { get; set; }
}
