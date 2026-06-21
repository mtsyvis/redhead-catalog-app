namespace Redhead.SitesCatalog.Api.Models;

public sealed record AhrefsSyncDryRunRequest(int? MaxSitesOverride);

public sealed record AhrefsSyncRunRequest(
    int? MaxSitesOverride,
    bool? SaveSnapshots,
    bool Force = false);
