/**
 * Site entity returned from the API
 */
export interface Site {
  domain: string;
  dr: number;
  traffic: number;
  location: string;
  priceUsd: number | null;
  priceCasino: number | null;
  priceCasinoStatus: ServiceAvailabilityStatus;
  priceCrypto: number | null;
  priceCryptoStatus: ServiceAvailabilityStatus;
  priceLinkInsert: number | null;
  priceLinkInsertStatus: ServiceAvailabilityStatus;
  niche: string | null;
  categories: string | null;
  linkType: string | null;
  sponsoredTag: string | null;
  isQuarantined: boolean;
  quarantineReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  lastPublishedDate: string | null;
  lastPublishedDateIsMonthOnly: boolean;
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
  location?: string[];
  casinoAvailability?: ServiceAvailabilityFilter;
  cryptoAvailability?: ServiceAvailabilityFilter;
  linkInsertAvailability?: ServiceAvailabilityFilter;
  quarantine?: 'all' | 'only' | 'exclude';
}

/**
 * Locations response from /api/sites/locations
 */
export interface LocationsResponse {
  locations: string[];
}

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
  location: string[];
  casinoAvailability: ServiceAvailabilityFilter;
  cryptoAvailability: ServiceAvailabilityFilter;
  linkInsertAvailability: ServiceAvailabilityFilter;
  quarantine: 'all' | 'only' | 'exclude';
}

/**
 * Response from POST /api/sites/multi-search
 */
export interface MultiSearchResponse {
  found: Site[];
  notFound: string[];
  duplicates: string[];
}

/**
 * Request body for POST /api/export/sites-multi-search.csv
 */
export interface ExportMultiSearchPayload {
  queryText: string;
  filters: SitesQueryParams;
  sortBy?: string;
  sortDir?: string;
}

/**
 * Payload for PUT /api/sites/{domain} (Admin/SuperAdmin). Empty price = null (not allowed).
 */
export interface UpdateSitePayload {
  dr: number;
  traffic: number;
  location: string;
  priceUsd: number;
  priceCasino: number | null;
  priceCasinoStatus: ServiceAvailabilityStatusValue;
  priceCrypto: number | null;
  priceCryptoStatus: ServiceAvailabilityStatusValue;
  priceLinkInsert: number | null;
  priceLinkInsertStatus: ServiceAvailabilityStatusValue;
  niche: string | null;
  categories: string | null;
  LinkType: string | null;
  SponsoredTag: string | null;
  isQuarantined: boolean;
  quarantineReason: string | null;
}

export type ServiceAvailabilityStatus = 'Unknown' | 'Available' | 'NotAvailable' | 0 | 1 | 2;
export type ServiceAvailabilityStatusValue = 0 | 1 | 2;
export type ServiceAvailabilityFilter = 'all' | 'available' | 'notAvailable' | 'unknown';
