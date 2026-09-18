export interface MissingDomainAnalyticsRow {
  domain: string;
  searches: number;
  uniqueUsers: number;
  firstSearchedAtUtc: string;
  lastSearchedAtUtc: string;
  isInCatalog: boolean;
}

export interface MissingDomainsAnalyticsResponse {
  uniqueDomains: number;
  searches: number;
  uniqueUsers: number;
  items: MissingDomainAnalyticsRow[];
  page: number;
  pageSize: number;
}
