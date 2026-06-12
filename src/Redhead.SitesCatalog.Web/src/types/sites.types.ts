/**
 * Site entity returned from the API
 */
export interface Site {
  domain: string;
  dr: number;
  traffic: number;
  location: string;
  importedLocationRaw: string | null;
  language: string | null;
  priceUsd: number | null;
  priceCasino: number | null;
  priceCasinoStatus: ServiceAvailabilityStatus;
  priceCrypto: number | null;
  priceCryptoStatus: ServiceAvailabilityStatus;
  priceLinkInsert: number | null;
  priceLinkInsertStatus: ServiceAvailabilityStatus;
  priceLinkInsertCasino: number | null;
  priceLinkInsertCasinoStatus: ServiceAvailabilityStatus;
  priceDating: number | null;
  priceDatingStatus: ServiceAvailabilityStatus;
  numberDFLinks: number | null;
  termType: TermType | null;
  termValue: number | null;
  termUnit: TermUnit | null;
  niche: string | null;
  nicheTokens?: string[];
  categories: string | null;
  sponsoredTag: string | null;
  isQuarantined: boolean;
  quarantineReason: string | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  createdBy?: string | null;
  updatedBy?: string | null;
  lastPublishedDate: string | null;
  lastPublishedDateIsMonthOnly: boolean;
  pricing?: SitePricingDto | null;
}

/**
 * Paginated response from /api/sites
 */
export interface SitesListResponse {
  items: Site[];
  total: number;
}

/**
 * Query parameters for fetching sites
 */
export interface SitesQueryParams {
  page: number;
  pageSize: number;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  search?: string;
  drMin?: number;
  drMax?: number;
  trafficMin?: number;
  trafficMax?: number;
  priceMin?: number;
  priceMax?: number;
  termKey?: string;
  stopListDomains?: string[];
  /** Legacy location display-name filter. Prefer canonical locationKeys/locationGroupKeys. */
  location?: string[];
  locationKeys?: string[];
  locationGroupKeys?: string[];
  excludedLocationKeys?: string[];
  includeUnknownLocation?: boolean;
  includeOtherLocation?: boolean;
  niches?: string[];
  categorySearchTerms?: string[];
  topicFitMode?: TopicFitMode;
  excludedNiches?: string[];
  excludedCategorySearchTerms?: string[];
  languages?: string[];
  casinoAvailability?: ServiceAvailabilityFilter;
  cryptoAvailability?: ServiceAvailabilityFilter;
  linkInsertAvailability?: ServiceAvailabilityFilter;
  linkInsertCasinoAvailability?: ServiceAvailabilityFilter;
  datingAvailability?: ServiceAvailabilityFilter;
  quarantine?: 'all' | 'only' | 'exclude';
  /** yyyy-MM format, inclusive lower bound */
  lastPublishedFromMonth?: string;
  /** yyyy-MM format, inclusive upper bound */
  lastPublishedToMonth?: string;
}

/**
 * Locations response from /api/sites/locations
 */
export interface LocationsResponse {
  locations: string[];
}

export interface FilterOption {
  value: string;
  label: string;
}

export interface LocationGroupFilterOption {
  key: string;
  displayName: string;
  groupType: string;
  locationCount: number;
  locations: LocationFilterOption[];
}

export interface LocationFilterOption {
  key: string;
  displayName: string;
}

export interface LocationSpecialFilterOptions {
  unknown: LocationFilterOption;
  other?: LocationFilterOption | null;
}

export interface LocationFilterOptions {
  groups: LocationGroupFilterOption[];
  locations: LocationFilterOption[];
  special: LocationSpecialFilterOptions;
}

export interface FilterOptionsResponse {
  niches: FilterOption[];
  locations?: LocationFilterOptions | null;
  terms?: TermFilterOptionDto[] | null;
}

export type PriceType =
  | 'Main'
  | 'Casino'
  | 'Crypto'
  | 'LinkInsertion'
  | 'LinkInsertionCasino'
  | 'Dating'
  | 0
  | 1
  | 2
  | 3
  | 4
  | 5;
export type PriceTypeValue = 0 | 1 | 2 | 3 | 4 | 5;

export interface SitePriceOptionDto {
  priceType: PriceType;
  termKey: string;
  termType?: TermType | null;
  termValue?: number | null;
  termUnit?: TermUnit | null;
  termLabel: string;
  amountUsd: number;
}

export interface SiteServiceAvailabilityDto {
  serviceType: PriceType;
  status: ServiceAvailabilityStatus;
}

export interface SitePricingDto {
  prices: SitePriceOptionDto[];
  serviceAvailabilities: SiteServiceAvailabilityDto[];
}

export interface UpdateSitePriceOptionPayload {
  priceType: PriceTypeValue;
  termKey: string;
  termType: TermTypeValue | null;
  termValue: number | null;
  termUnit: TermUnitValue | null;
  amountUsd: number;
}

export interface UpdateSiteServiceAvailabilityPayload {
  serviceType: PriceTypeValue;
  status: ServiceAvailabilityStatusValue;
}

export interface UpdateSitePricingPayload {
  prices: UpdateSitePriceOptionPayload[];
  serviceAvailabilities: UpdateSiteServiceAvailabilityPayload[];
}

export interface TermFilterOptionDto {
  termKey: string;
  label: string;
  termType?: TermType | null;
  termValue?: number | null;
  termUnit?: TermUnit | null;
}

export type LocationFilterSelection =
  | {
      kind: 'group';
      key: string;
      displayName: string;
      groupType: string;
      locationCount?: number;
      locations?: LocationFilterOption[];
    }
  | {
      kind: 'location';
      key: string;
      displayName: string;
    }
  | {
      kind: 'special';
      key: 'unknown' | 'other';
      displayName: string;
      locationKey?: string;
    };

export interface SitesLocationFilterRequestFields {
  locationKeys?: string[];
  locationGroupKeys?: string[];
  excludedLocationKeys?: string[];
  includeUnknownLocation?: boolean;
  includeOtherLocation?: boolean;
}

export type TopicFitMode = 'expand' | 'narrow';

/**
 * Filter values state
 */
export interface SitesFilters {
  search: string;
  drMin: string;
  drMax: string;
  trafficMin: string;
  trafficMax: string;
  priceMin: string;
  priceMax: string;
  termKey: string | null;
  stopListDomains: string[];
  locationSelections: LocationFilterSelection[];
  excludedLocationKeys: string[];
  niches: string[];
  categorySearchTerms: string[];
  topicFitMode: TopicFitMode;
  excludedNiches: string[];
  excludedCategorySearchTerms: string[];
  languages: string[];
  casinoAvailability: ServiceAvailabilityFilter;
  cryptoAvailability: ServiceAvailabilityFilter;
  linkInsertAvailability: ServiceAvailabilityFilter;
  linkInsertCasinoAvailability: ServiceAvailabilityFilter;
  datingAvailability: ServiceAvailabilityFilter;
  quarantine: 'all' | 'only' | 'exclude';
  /** yyyy-MM format, null means not set */
  lastPublishedFromMonth: string | null;
  /** yyyy-MM format, null means not set */
  lastPublishedToMonth: string | null;
}

/**
 * Response from POST /api/sites/multi-search
 */
export type MultiSearchResultItem =
  | { domain: string; found: true; site: Site }
  | { domain: string; found: false; site: null };

export interface MultiSearchResponse {
  results: MultiSearchResultItem[];
  found: Site[];
  notFound: string[];
  duplicates: string[];
}

/**
 * Request body for POST /api/export/sites-multi-search.xlsx
 */
export interface ExportMultiSearchPayload {
  searchText: string;
  filters: SitesQueryParams;
  visibleColumnKeys: string[];
}

/**
 * Request body for POST /api/export/sites.xlsx
 */
export interface ExportSitesPayload {
  filters: SitesQueryParams;
  visibleColumnKeys: string[];
}

/**
 * Request body for POST /api/sites/export/google-drive.
 */
export interface GoogleDriveExportPayload {
  filters: SitesQueryParams;
  searchText?: string;
  visibleColumnKeys: string[];
}

/**
 * Response from POST /api/sites/export/google-drive.
 */
export interface GoogleDriveExportResponse {
  fileId: string;
  fileName: string;
  webViewLink: string | null;
  rowsExported: number;
  wasTruncated: boolean;
  exportedAtUtc: string;
  destinationLabel: string;
  truncationReason: string | null;
}

/**
 * Payload for PUT /api/sites/{domain} (Admin/SuperAdmin). When pricing is present, it is the
 * authoritative term-aware pricing payload.
 */
export interface UpdateSitePayload {
  dr: number;
  traffic: number;
  location: string;
  language: string | null;
  pricing: UpdateSitePricingPayload;
  numberDFLinks: number | null;
  termType: TermTypeValue | null;
  termValue: number | null;
  termUnit: TermUnitValue | null;
  niche: string | null;
  categories: string | null;
  SponsoredTag: string | null;
  isQuarantined: boolean;
  quarantineReason: string | null;
}

export type ServiceAvailabilityStatus =
  | 'Unknown'
  | 'Available'
  | 'NotAvailable'
  | 'AvailableWithUnknownPrice'
  | 0
  | 1
  | 2
  | 3;
export type ServiceAvailabilityStatusValue = 0 | 1 | 2 | 3;
export type ServiceAvailabilityFilterValue =
  | 'unknown'
  | 'available'
  | 'notAvailable'
  | 'availableWithUnknownPrice';
export type ServiceAvailabilityFilter = ServiceAvailabilityFilterValue[];
export type TermType = 'Permanent' | 'Finite' | 1 | 2;
export type TermTypeValue = 1 | 2;
export type TermUnit = 'Year' | 1;
export type TermUnitValue = 1;
