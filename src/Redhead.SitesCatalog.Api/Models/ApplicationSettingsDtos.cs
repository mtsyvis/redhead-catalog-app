namespace Redhead.SitesCatalog.Api.Models;

public sealed record ApplicationSettingsLimitsResponse(
    int DefaultClientSelectionLimit,
    int MaxClientSelectionLimit,
    int ClientRequestsPerMinute,
    int ClientUniqueSitesPerFiveMinutes,
    int ClientAlertUniqueSitesPerHour,
    int GlobalMultiSearchMaxInputs,
    int StopListMaxDomains,
    int LiteMultiSearchMaxDomainsPerRequest,
    int LiteMonthlyDomainLimit,
    int CustomTableViewsPerUserTable,
    int SavedFilterSetsPerUserTable);
