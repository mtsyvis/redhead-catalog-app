namespace Redhead.SitesCatalog.Application.Models.Analytics;

public sealed record CatalogCoverageAnalyticsDto(
    long CatalogSites,
    long CoveredSites,
    long SitesWithoutOffers,
    decimal CoveragePercent,
    long TotalOffers,
    long ActiveOffers,
    long InactiveOffers,
    long SitesWithOnlyInactiveOffers,
    long QuarantinedSites,
    long QuarantinedSitesWithoutOffers,
    DateTime GeneratedAtUtc);
