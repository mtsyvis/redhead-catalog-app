import { useMemo } from 'react';
import type { GridSortModel } from '@mui/x-data-grid';
import type {
  MultiSearchResponse,
  ServiceAvailabilityStatus,
  Site,
  SitesFilters as FiltersType,
} from '../../../types/sites.types';
import { formatLanguageCode } from '../../../utils/language';
import {
  getOptionalServiceSortRank,
  matchesAvailabilityFilter,
} from '../../../utils/serviceAvailability';

export type NotFoundRow = { domain: string; _isNotFound: true };
export type GridRow = Site | NotFoundRow;

interface UseSitesGridRowsOptions {
  sites: Site[];
  total: number;
  loading: boolean;
  multiSearchLoading: boolean;
  multiSearchResult: MultiSearchResponse | null;
  filters: FiltersType;
  sortModel: GridSortModel;
  gridFiltersActive: boolean;
}

export function isNotFoundRow(row: GridRow): row is NotFoundRow {
  return '_isNotFound' in row && row._isNotFound === true;
}

/** Client-side filter for multi-search found rows (same logic as server filters, excluding search). */
function filterSites(sites: Site[], f: FiltersType): Site[] {
  const excludedLocationKeys = new Set(f.excludedLocationKeys);
  const selectedGroupLocations = f.locationSelections
    .filter((selection) => selection.kind === 'group')
    .flatMap((selection) => selection.locations ?? []);
  const hasSelectedGroupWithoutMembers = f.locationSelections.some(
    (selection) => selection.kind === 'group' && (selection.locations?.length ?? 0) === 0
  );
  const selectedLocationNames = new Set<string>();

  for (const selection of f.locationSelections) {
    if (selection.kind === 'location' && !excludedLocationKeys.has(selection.key)) {
      selectedLocationNames.add(selection.displayName);
    }

    if (selection.kind === 'special') {
      selectedLocationNames.add(selection.displayName);
    }
  }

  for (const location of selectedGroupLocations) {
    if (!excludedLocationKeys.has(location.key)) {
      selectedLocationNames.add(location.displayName);
    }
  }

  return sites.filter((s) => {
    if (f.drMin !== '' && s.dr < Number(f.drMin)) return false;
    if (f.drMax !== '' && s.dr > Number(f.drMax)) return false;
    if (f.trafficMin !== '' && s.traffic < Number(f.trafficMin)) return false;
    if (f.trafficMax !== '' && s.traffic > Number(f.trafficMax)) return false;
    if (f.priceMin !== '' && (s.priceUsd ?? 0) < Number(f.priceMin)) return false;
    if (f.priceMax !== '' && (s.priceUsd ?? 0) > Number(f.priceMax)) return false;
    if (
      selectedLocationNames.size > 0 &&
      !hasSelectedGroupWithoutMembers &&
      !selectedLocationNames.has(s.location) &&
      !(selectedLocationNames.has('Other') && s.location.startsWith('Other - '))
    ) {
      return false;
    }
    if (f.niches.length > 0 && !f.niches.some((niche) => (s.nicheTokens ?? []).includes(niche))) return false;
    if (
      f.categorySearchTerms.length > 0 &&
      !f.categorySearchTerms.some((term) =>
        (s.categories ?? '').toLowerCase().includes(term.toLowerCase())
      )
    ) {
      return false;
    }
    if (f.languages.length > 0 && !f.languages.includes(formatLanguageCode(s.language))) return false;
    if (!matchesAvailabilityFilter(s.priceCasinoStatus, f.casinoAvailability)) return false;
    if (!matchesAvailabilityFilter(s.priceCryptoStatus, f.cryptoAvailability)) return false;
    if (!matchesAvailabilityFilter(s.priceLinkInsertStatus, f.linkInsertAvailability)) return false;
    if (!matchesAvailabilityFilter(s.priceLinkInsertCasinoStatus, f.linkInsertCasinoAvailability)) return false;
    if (!matchesAvailabilityFilter(s.priceDatingStatus, f.datingAvailability)) return false;
    if (f.quarantine === 'only' && !s.isQuarantined) return false;
    if (f.quarantine === 'exclude' && s.isQuarantined) return false;
    if (f.lastPublishedFromMonth) {
      if (s.lastPublishedDate == null) return false;
      if (s.lastPublishedDate.substring(0, 7) < f.lastPublishedFromMonth) return false;
    }
    if (f.lastPublishedToMonth) {
      if (s.lastPublishedDate == null) return false;
      if (s.lastPublishedDate.substring(0, 7) > f.lastPublishedToMonth) return false;
    }
    return true;
  });
}

const OPTIONAL_SERVICE_STATUS_FIELDS: Record<string, keyof Site> = {
  priceCasino: 'priceCasinoStatus',
  priceCrypto: 'priceCryptoStatus',
  priceLinkInsert: 'priceLinkInsertStatus',
  priceLinkInsertCasino: 'priceLinkInsertCasinoStatus',
  priceDating: 'priceDatingStatus',
};

function compareOptionalServicePrice(a: Site, b: Site, field: keyof Site, dir: 'asc' | 'desc'): number {
  const statusField = OPTIONAL_SERVICE_STATUS_FIELDS[field as string];
  const leftStatus = a[statusField] as ServiceAvailabilityStatus;
  const rightStatus = b[statusField] as ServiceAvailabilityStatus;
  const rankDiff = getOptionalServiceSortRank(leftStatus) - getOptionalServiceSortRank(rightStatus);
  if (rankDiff !== 0) return rankDiff;

  const av = a[field] as number | null;
  const bv = b[field] as number | null;
  if (av == null && bv == null) return a.domain.localeCompare(b.domain);
  if (av == null) return 1;
  if (bv == null) return -1;

  const priceDiff = dir === 'asc' ? av - bv : bv - av;
  return priceDiff === 0 ? a.domain.localeCompare(b.domain) : priceDiff;
}

function getSortValue(site: Site, field: string): Site[keyof Site] | undefined {
  if (field === 'createdAt') return site.createdAtUtc;
  if (field === 'updatedAt') return site.updatedAtUtc;
  return site[field as keyof Site];
}

export function useSitesGridRows({
  sites,
  total,
  loading,
  multiSearchLoading,
  multiSearchResult,
  filters,
  sortModel,
  gridFiltersActive,
}: UseSitesGridRowsOptions) {
  const isMultiSearchView = multiSearchResult !== null;
  const gridRows: GridRow[] = useMemo(() => {
    if (multiSearchResult === null) {
      return sites;
    }
    const filtered = filterSites(multiSearchResult.found, filters);
    const field = sortModel[0]?.field ?? 'domain';
    const dir = sortModel[0]?.sort ?? 'asc';
    const sorted = [...filtered].sort((a, b) => {
      if (field in OPTIONAL_SERVICE_STATUS_FIELDS) {
        return compareOptionalServicePrice(a, b, field as keyof Site, dir);
      }

      const av = getSortValue(a, field);
      const bv = getSortValue(b, field);
      if (av == null && bv == null) return 0;
      if (av == null) return dir === 'asc' ? 1 : -1;
      if (bv == null) return dir === 'asc' ? -1 : 1;
      const cmp =
        typeof av === 'string' ? (av as string).localeCompare(bv as string) : (av as number) - (bv as number);
      return dir === 'asc' ? cmp : -cmp;
    });
    const notFoundRows: NotFoundRow[] = gridFiltersActive
      ? []
      : multiSearchResult.notFound.map((d) => ({ domain: d, _isNotFound: true as const }));
    return [...sorted, ...notFoundRows];
  }, [multiSearchResult, filters, sortModel, sites, gridFiltersActive]);

  return {
    gridRows,
    gridRowCount: isMultiSearchView ? gridRows.length : total,
    gridLoading: loading || multiSearchLoading,
    isMultiSearchView,
  };
}
