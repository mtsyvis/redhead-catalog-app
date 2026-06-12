export type BusinessDemandDestinationFilter = 'Download' | 'GoogleDrive';
export type BusinessDemandStatusFilter = 'successful' | 'partial' | 'blocked';

export interface BusinessDemandAnalyticsQueryParams {
  from: string;
  to: string;
  clientId?: string;
  destination?: BusinessDemandDestinationFilter;
  status?: BusinessDemandStatusFilter;
}

export interface ExportActivityAnalyticsQueryParams extends BusinessDemandAnalyticsQueryParams {
  page: number;
  pageSize: number;
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

export type ExportActivityStatus = 'Successful' | 'Partial' | 'Blocked';

export interface ExportActivitySummary {
  completedExports: number;
  partialExports: number;
  blockedExports: number;
  uniqueExportedDomains: number;
  requestedRows: number;
  exportedRows: number;
}

export interface ExportActivityOverTimeItem {
  date: string;
  successfulExports: number;
  partialExports: number;
  blockedExports: number;
  exportedDomains: number;
}

export interface ExportActivityClientSummaryItem {
  userId: string;
  email: string;
  displayName?: string | null;
  successfulExports: number;
  partialExports: number;
  blockedExports: number;
  requestedRows: number;
  exportedRows: number;
  lastExportAtUtc?: string | null;
}

export interface ExportActivityRecentExportItem {
  id: string;
  timestampUtc: string;
  userId: string;
  email: string;
  displayName?: string | null;
  destination?: string | null;
  status: ExportActivityStatus;
  requestedRows: number;
  exportedRows: number;
  blockedReason?: string | null;
  reason?: string | null;
  filtersSummary?: string | null;
  sortSummary?: string | null;
}

export interface ExportActivityRecentExports {
  items: ExportActivityRecentExportItem[];
  totalCount: number;
}

export interface ExportActivityAnalytics {
  summary: ExportActivitySummary;
  exportsOverTime: ExportActivityOverTimeItem[];
  clientSummaries: ExportActivityClientSummaryItem[];
  recentExports: ExportActivityRecentExports;
}

export interface ExportLogDetailsRow {
  label: string;
  value: string;
}

export interface ExportLogDetailsSection {
  title: string;
  rows: ExportLogDetailsRow[];
}

export interface ExportLogSortDetails {
  summary: string;
  items: ExportLogDetailsRow[];
}

export interface ExportLogTechnicalDetails {
  filtersSnapshotJson?: string | null;
  sortSnapshotJson?: string | null;
  searchSnapshotJson?: string | null;
}

export interface ExportLogDetails {
  id: string;
  timestampUtc: string;
  userId: string;
  email: string;
  displayName?: string | null;
  destination?: string | null;
  status: ExportActivityStatus;
  requestedRows: number;
  exportedRows: number;
  blockedReason?: string | null;
  outcomeReason?: string | null;
  exportMode?: string | null;
  appliedFilters: ExportLogDetailsSection[];
  sort: ExportLogSortDetails;
  technicalDetails?: ExportLogTechnicalDetails | null;
}
