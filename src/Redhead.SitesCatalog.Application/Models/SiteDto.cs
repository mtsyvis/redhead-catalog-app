using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Models;

/// <summary>
/// Data transfer object for site information
/// </summary>
public class SiteDto
{
    public string Domain { get; set; } = string.Empty;
    public double DR { get; set; }
    public long Traffic { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? ImportedLocationRaw { get; set; }
    public string? Language { get; set; }
    public string? SponsoredTag { get; set; }
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
    public DateTime UpdatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? LastPublishedDate { get; set; }
    public bool LastPublishedDateIsMonthOnly { get; set; }
    public SitePricingDto Pricing { get; set; } = new();
}
