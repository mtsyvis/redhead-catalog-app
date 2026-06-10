using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Models;

public sealed class SitePriceOptionDto
{
    public PriceType PriceType { get; init; }
    public string TermKey { get; init; } = string.Empty;
    public TermType? TermType { get; init; }
    public int? TermValue { get; init; }
    public TermUnit? TermUnit { get; init; }
    public string TermLabel { get; init; } = string.Empty;
    public decimal AmountUsd { get; init; }
}

public sealed class SiteServiceAvailabilityDto
{
    public PriceType ServiceType { get; init; }
    public ServiceAvailabilityStatus Status { get; init; }
}

public sealed class SitePricingDto
{
    public IReadOnlyList<SitePriceOptionDto> Prices { get; init; } = [];
    public IReadOnlyList<SiteServiceAvailabilityDto> ServiceAvailabilities { get; init; } = [];
}
