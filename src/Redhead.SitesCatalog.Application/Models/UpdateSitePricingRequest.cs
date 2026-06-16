using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Models;

public sealed class UpdateSitePricingRequest
{
    public IReadOnlyList<UpdateSitePriceOptionRequest> Prices { get; init; } = [];

    public IReadOnlyList<UpdateSiteServiceAvailabilityRequest> ServiceAvailabilities { get; init; } = [];
}

public sealed class UpdateSitePriceOptionRequest
{
    public PriceType PriceType { get; init; }

    public string TermKey { get; init; } = string.Empty;

    public TermType? TermType { get; init; }

    public int? TermValue { get; init; }

    public TermUnit? TermUnit { get; init; }

    public decimal AmountUsd { get; init; }
}

public sealed class UpdateSiteServiceAvailabilityRequest
{
    public PriceType ServiceType { get; init; }

    public ServiceAvailabilityStatus Status { get; init; }
}
