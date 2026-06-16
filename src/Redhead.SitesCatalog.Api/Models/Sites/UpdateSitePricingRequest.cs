using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Models.Sites;

public sealed class UpdateSitePricingRequest
{
    public IReadOnlyList<UpdateSitePriceOptionRequest> Prices { get; set; } = [];

    public IReadOnlyList<UpdateSiteServiceAvailabilityRequest> ServiceAvailabilities { get; set; } = [];
}

public sealed class UpdateSitePriceOptionRequest
{
    public PriceType PriceType { get; set; }

    public string TermKey { get; set; } = string.Empty;

    public TermType? TermType { get; set; }

    public int? TermValue { get; set; }

    public TermUnit? TermUnit { get; set; }

    public decimal AmountUsd { get; set; }
}

public sealed class UpdateSiteServiceAvailabilityRequest
{
    public PriceType ServiceType { get; set; }

    public ServiceAvailabilityStatus Status { get; set; }
}
