import type { WebmasterOffersSearchResult } from '../types/webmasterOffers.types';

const SEARCH_CACHE_KEY = 'webmaster-offers:last-search:v1';
const SEARCH_CACHE_TTL_MS = 60 * 60 * 1000;

export interface CachedWebmasterOffersSearch {
  readonly cachedAt: number;
  readonly query: string;
  readonly result: WebmasterOffersSearchResult;
}

function isSearchResult(value: unknown): value is WebmasterOffersSearchResult {
  if (!value || typeof value !== 'object') return false;

  const candidate = value as Partial<WebmasterOffersSearchResult>;
  return (
    typeof candidate.domain === 'string' &&
    typeof candidate.siteFound === 'boolean' &&
    Array.isArray(candidate.offers)
  );
}

export function clearCachedWebmasterOffersSearch() {
  try {
    globalThis.sessionStorage?.removeItem(SEARCH_CACHE_KEY);
  } catch {
    // Browser storage can be unavailable in restricted browsing modes.
  }
}

export function readCachedWebmasterOffersSearch(): CachedWebmasterOffersSearch | null {
  try {
    const raw = globalThis.sessionStorage?.getItem(SEARCH_CACHE_KEY);
    if (!raw) return null;

    const cached = JSON.parse(raw) as Partial<CachedWebmasterOffersSearch>;
    const isValid =
      typeof cached.cachedAt === 'number' &&
      typeof cached.query === 'string' &&
      isSearchResult(cached.result);
    const cacheAge = typeof cached.cachedAt === 'number' ? Date.now() - cached.cachedAt : -1;
    const isFresh = isValid && cacheAge >= 0 && cacheAge < SEARCH_CACHE_TTL_MS;

    if (!isFresh) {
      clearCachedWebmasterOffersSearch();
      return null;
    }

    return cached as CachedWebmasterOffersSearch;
  } catch {
    clearCachedWebmasterOffersSearch();
    return null;
  }
}

export function cacheWebmasterOffersSearch(
  query: string,
  result: WebmasterOffersSearchResult
) {
  try {
    const cached: CachedWebmasterOffersSearch = {
      cachedAt: Date.now(),
      query,
      result,
    };
    globalThis.sessionStorage?.setItem(SEARCH_CACHE_KEY, JSON.stringify(cached));
  } catch {
    // Search remains usable when browser storage is unavailable or full.
  }
}
