using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Domain.Entities;

public class WebmasterOfferPrice
{
    public Guid Id { get; set; }

    public Guid SiteWebmasterOfferId { get; set; }
    public SiteWebmasterOffer SiteWebmasterOffer { get; set; } = null!;

    public WebmasterOfferPriceType PriceType { get; set; }
    public decimal? WebmasterPriceUsd { get; set; }
    public string? WebmasterPriceDetails { get; set; }
    public TermType? TermType { get; set; }
    public int? TermValue { get; set; }
    public TermUnit? TermUnit { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
