using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Domain.Entities;

public class SitePriceOption
{
    public long Id { get; set; }

    public string SiteDomain { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    public PriceType PriceType { get; set; }

    public string TermKey { get; set; } = string.Empty;

    public TermType? TermType { get; set; }
    public int? TermValue { get; set; }
    public TermUnit? TermUnit { get; set; }

    public decimal AmountUsd { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
