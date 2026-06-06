export type BusinessDemandDestinationFilter = 'Download' | 'GoogleDrive';
export type BusinessDemandStatusFilter = 'successful' | 'partial' | 'blocked';

export interface BusinessDemandAnalyticsQueryParams {
  from: string;
  to: string;
  clientId?: string;
  destination?: BusinessDemandDestinationFilter;
  status?: BusinessDemandStatusFilter;
}

export interface BusinessDemandSummary {
  exportRequests: number;
  clientsWithExportActivity: number;
  requestedRows: number;
  exportedDomains: number;
}

export interface BusinessDemandCount {
  name: string;
  exportRequests: number;
}

export interface ServiceDemand {
  service: string;
  wantedOrAvailableRequests: number;
  explicitlyNoRequests: number;
}

export interface QualityDemand {
  drRanges: BusinessDemandCount[];
  trafficRanges: BusinessDemandCount[];
  priceRanges: BusinessDemandCount[];
}

export interface FilterStrictness {
  noFilters: number;
  broadExports: number;
  filteredExports: number;
  broadExportThreshold: number;
}

export interface BusinessDemandAnalytics {
  summary: BusinessDemandSummary;
  topLocations: BusinessDemandCount[];
  topNiches: BusinessDemandCount[];
  topCategories: BusinessDemandCount[];
  topLanguages: BusinessDemandCount[];
  serviceDemand: ServiceDemand[];
  qualityDemand: QualityDemand;
  filterStrictness: FilterStrictness;
}

export interface AnalyticsClientOption {
  id: string;
  email: string;
  displayName: string;
}
