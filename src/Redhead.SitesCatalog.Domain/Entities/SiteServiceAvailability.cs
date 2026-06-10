using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Domain.Entities;

public class SiteServiceAvailability
{
    public long Id { get; set; }

    public string SiteDomain { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    public PriceType ServiceType { get; set; }

    public ServiceAvailabilityStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
