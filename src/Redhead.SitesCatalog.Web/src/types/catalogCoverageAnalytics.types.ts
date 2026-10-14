export interface CatalogCoverageAnalytics {
  catalogSites: number;
  coveredSites: number;
  sitesWithoutOffers: number;
  coveragePercent: number;
  totalOffers: number;
  activeOffers: number;
  inactiveOffers: number;
  sitesWithOnlyInactiveOffers: number;
  quarantinedSites: number;
  quarantinedSitesWithoutOffers: number;
  generatedAtUtc: string;
}
