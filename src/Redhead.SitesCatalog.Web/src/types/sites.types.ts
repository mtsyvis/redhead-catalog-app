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
  priceLinkInsertCasino: number | null;
  priceLinkInsertCasinoStatus: ServiceAvailabilityStatus;
  priceDating: number | null;
  priceDatingStatus: ServiceAvailabilityStatus;
  numberDFLinks: number | null;
  termType: TermType | null;
  termValue: number | null;
  termUnit: TermUnit | null;
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
 * Payload for PUT /api/sites/{domain} (Admin/SuperAdmin). priceUsd may be null; at least one
 * numeric price among priceUsd or service-specific prices is required by the backend.
 */
export interface UpdateSitePayload {
  dr: number;
  traffic: number;
  location: string;
  priceUsd: number | null;
  priceCasino: number | null;
  priceCasinoStatus: ServiceAvailabilityStatusValue;
  priceCrypto: number | null;
  priceCryptoStatus: ServiceAvailabilityStatusValue;
  priceLinkInsert: number | null;
  priceLinkInsertStatus: ServiceAvailabilityStatusValue;
  priceLinkInsertCasino: number | null;
  priceLinkInsertCasinoStatus: ServiceAvailabilityStatusValue;
  priceDating: number | null;
  priceDatingStatus: ServiceAvailabilityStatusValue;
  numberDFLinks: number | null;
  termType: TermTypeValue | null;
  termValue: number | null;
  termUnit: TermUnitValue | null;
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
export type TermType = 'Permanent' | 'Finite' | 1 | 2;
export type TermTypeValue = 1 | 2;
export type TermUnit = 'Year' | 1;
export type TermUnitValue = 1;
