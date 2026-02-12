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
  priceCrypto: number | null;
  priceLinkInsert: number | null;
  niche: string | null;
  categories: string | null;
  isQuarantined: boolean;
  quarantineReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
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
  casinoAllowed?: boolean;
  cryptoAllowed?: boolean;
  linkInsertAllowed?: boolean;
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
  casinoAllowed: boolean;
  cryptoAllowed: boolean;
  linkInsertAllowed: boolean;
  quarantine: 'all' | 'only' | 'exclude';
}
