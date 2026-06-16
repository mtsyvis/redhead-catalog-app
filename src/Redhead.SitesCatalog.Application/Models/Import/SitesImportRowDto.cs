using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Models.Import;

public sealed record SitesImportPriceCell(
    string Header,
    PriceType PriceType,
    string TermKey,
    TermType? TermType,
    int? TermValue,
    TermUnit? TermUnit,
    string? RawValue);

public sealed record SitesImportAvailabilityCell(
    string Header,
    PriceType ServiceType,
    string? RawValue);

/// <summary>
/// One row from a sites import file (CSV)
/// </summary>
public class SitesImportRowDto
{
    public int RowNumber { get; set; }
    public string? Domain { get; set; }
    public string? DRRaw { get; set; }
    public double? DR { get; set; }
    public string? TrafficRaw { get; set; }
    public long? Traffic { get; set; }
    public string? Location { get; set; }
    public string? NumberDFLinksRaw { get; set; }
    public int? NumberDFLinks { get; set; }
    public string? Language { get; set; }
    public string? Niche { get; set; }
    public string? Categories { get; set; }
    public string? SponsoredTag { get; set; }
    public List<SitesImportPriceCell> PriceCells { get; set; } = new();
    public List<SitesImportAvailabilityCell> AvailabilityCells { get; set; } = new();
}
