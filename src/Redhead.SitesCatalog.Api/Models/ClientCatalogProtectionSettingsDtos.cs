namespace Redhead.SitesCatalog.Api.Models;

public sealed record ClientCatalogProtectionSettingsRequest(
    bool AutoBanEnabled,
    int AutoBanUniqueSitesPer24Hours);

public sealed record ClientCatalogProtectionSettingsResponse(
    bool AutoBanEnabled,
    int AutoBanUniqueSitesPer24Hours,
    DateTime? UpdatedAtUtc,
    string? UpdatedByUserId,
    string? UpdatedByDisplayName);
