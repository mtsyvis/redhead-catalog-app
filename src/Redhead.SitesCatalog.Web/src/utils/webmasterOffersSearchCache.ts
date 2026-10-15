import type { WebmasterOffersSummarySearchResult } from '../types/webmasterOffers.types';

const LEGACY_SEARCH_CACHE_KEY = 'webmaster-offers:last-search:v1';
const SEARCH_CACHE_KEY = 'webmaster-offers:last-search:v2';
const SEARCH_CACHE_TTL_MS = 60 * 60 * 1000;

export interface WebmasterOffersCacheScope {
  readonly userId: string;
  readonly access: 'full' | 'summary';
}

export interface CachedWebmasterOffersSearch {
  readonly cachedAt: number;
  readonly query: string;
  readonly scope: WebmasterOffersCacheScope;
  readonly result: WebmasterOffersSummarySearchResult;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function isNullableText(value: unknown) {
  return value === null || typeof value === 'string';
}

function isStringOrNumber(value: unknown) {
  return typeof value === 'string' || (typeof value === 'number' && Number.isFinite(value));
}

function hasTermFields(value: Record<string, unknown>) {
  return typeof value.termLabel === 'string' &&
    (value.termType === null || isStringOrNumber(value.termType)) &&
    (value.termValue === null || (typeof value.termValue === 'number' && Number.isFinite(value.termValue))) &&
    (value.termUnit === null || isStringOrNumber(value.termUnit));
}

function isMailbox(value: unknown) {
  return isRecord(value) &&
    typeof value.id === 'string' && typeof value.email === 'string' &&
    typeof value.displayName === 'string' &&
    (value.source === 1 || value.source === 2 || value.source === 'Import' || value.source === 'Manual');
}

function isPrice(value: unknown) {
  return isRecord(value) &&
    typeof value.id === 'string' &&
    isStringOrNumber(value.priceType) &&
    isStringOrNumber(value.availabilityStatus) &&
    (value.webmasterPriceUsd === null ||
      (typeof value.webmasterPriceUsd === 'number' && Number.isFinite(value.webmasterPriceUsd))) &&
    isNullableText(value.webmasterPriceDetails) && hasTermFields(value);
}

function isOffer(value: unknown, access: WebmasterOffersCacheScope['access']) {
  if (!isRecord(value)) return false;
  const textFields = [
    'outreachSenderRawText', 'linkbuilderMailboxRawText', 'linkPolicyText', 'dfLinksRawText',
    'sponsoredTagRawText', 'commentText', 'clientRawText', 'termRawText', 'updatedBy',
  ];
  return typeof value.id === 'string' && hasTermFields(value) &&
    textFields.every(field => isNullableText(value[field])) &&
    (value.status === 1 || value.status === 2 || value.status === 'Active' || value.status === 'Inactive') &&
    typeof value.createdAtUtc === 'string' && Number.isFinite(Date.parse(value.createdAtUtc)) &&
    typeof value.updatedAtUtc === 'string' && Number.isFinite(Date.parse(value.updatedAtUtc)) &&
    typeof value.version === 'number' && Number.isInteger(value.version) && value.version >= 0 &&
    Array.isArray(value.linkbuilderMailboxes) && value.linkbuilderMailboxes.every(isMailbox) &&
    (access === 'summary'
      ? !('primaryEmail' in value || 'contactRawText' in value || 'prices' in value)
      : isNullableText(value.primaryEmail) && typeof value.contactRawText === 'string' &&
        Array.isArray(value.prices) && value.prices.every(isPrice));
}

function isSearchResult(
  value: unknown,
  access: WebmasterOffersCacheScope['access']
): value is WebmasterOffersSummarySearchResult {
  if (!isRecord(value)) return false;
  return (
    typeof value.domain === 'string' &&
    typeof value.siteFound === 'boolean' &&
    Array.isArray(value.offers) && value.offers.every(offer => isOffer(offer, access))
  );
}

export function clearCachedWebmasterOffersSearch() {
  try {
    globalThis.sessionStorage?.removeItem(LEGACY_SEARCH_CACHE_KEY);
    globalThis.sessionStorage?.removeItem(SEARCH_CACHE_KEY);
  } catch {
    // Browser storage can be unavailable in restricted browsing modes.
  }
}

export function readCachedWebmasterOffersSearch(
  scope: WebmasterOffersCacheScope
): CachedWebmasterOffersSearch | null {
  try {
    // The old shared cache cannot be reused safely without account/access information.
    globalThis.sessionStorage?.removeItem(LEGACY_SEARCH_CACHE_KEY);
    const raw = globalThis.sessionStorage?.getItem(SEARCH_CACHE_KEY);
    if (!raw) return null;

    const cached = JSON.parse(raw) as Partial<CachedWebmasterOffersSearch> | null;
    const isValid = cached &&
      cached.scope?.userId === scope.userId &&
      cached.scope?.access === scope.access &&
      typeof cached.cachedAt === 'number' &&
      typeof cached.query === 'string' &&
      isSearchResult(cached.result, scope.access);
    const cacheAge = typeof cached?.cachedAt === 'number' ? Date.now() - cached.cachedAt : -1;
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
  result: WebmasterOffersSummarySearchResult,
  scope: WebmasterOffersCacheScope
) {
  try {
    if (!isSearchResult(result, scope.access)) {
      clearCachedWebmasterOffersSearch();
      return;
    }
    const cached: CachedWebmasterOffersSearch = {
      cachedAt: Date.now(),
      query,
      scope,
      result,
    };
    globalThis.sessionStorage?.removeItem(LEGACY_SEARCH_CACHE_KEY);
    globalThis.sessionStorage?.setItem(SEARCH_CACHE_KEY, JSON.stringify(cached));
  } catch {
    // Search remains usable when browser storage is unavailable or full.
  }
}
