using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Domain.Entities;

public sealed class ClientCatalogProtectionSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public bool AutoBanEnabled { get; set; }
    public int AutoBanUniqueSitesPer24Hours { get; set; } = ClientCatalogLimits.DefaultAutoBanUniqueSitesPer24Hours;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedByUserId { get; set; }
}
