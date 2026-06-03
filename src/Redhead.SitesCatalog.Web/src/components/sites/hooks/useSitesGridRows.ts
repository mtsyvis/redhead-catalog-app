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
    const nicheTokens = s.nicheTokens ?? [];
    const categories = (s.categories ?? '').toLowerCase();
    const matchesIncludedNiche =
      f.niches.length === 0 || f.niches.some((niche) => nicheTokens.includes(niche));
    const matchesIncludedCategory =
      f.categorySearchTerms.length === 0 ||
      f.categorySearchTerms.some((term) => categories.includes(term.toLowerCase()));
    const hasIncludedNicheFilter = f.niches.length > 0;
    const hasIncludedCategoryFilter = f.categorySearchTerms.length > 0;

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
    if (
      hasIncludedNicheFilter &&
      hasIncludedCategoryFilter &&
      f.topicFitMode === 'expand' &&
      !matchesIncludedNiche &&
      !matchesIncludedCategory
    ) {
      return false;
    }
    if (
      (!hasIncludedCategoryFilter || f.topicFitMode === 'narrow') &&
      hasIncludedNicheFilter &&
      !matchesIncludedNiche
    ) return false;
    if (
      (!hasIncludedNicheFilter || f.topicFitMode === 'narrow') &&
      hasIncludedCategoryFilter &&
      !matchesIncludedCategory
    ) return false;
    if (f.excludedNiches.some((niche) => nicheTokens.includes(niche))) return false;
    if (f.excludedCategorySearchTerms.some((term) => categories.includes(term.toLowerCase()))) {
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

function isMultiSearchInputOrderSort(sortModel: GridSortModel): boolean {
  const field = sortModel[0]?.field;
  return field == null || field === 'domain';
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
  const multiSearchGridFilters = useMemo<FiltersType>(
    () => ({
      search: '',
      drMin: filters.drMin,
      drMax: filters.drMax,
      trafficMin: filters.trafficMin,
      trafficMax: filters.trafficMax,
      priceMin: filters.priceMin,
      priceMax: filters.priceMax,
      stopListDomains: [],
      locationSelections: filters.locationSelections,
      excludedLocationKeys: filters.excludedLocationKeys,
      niches: filters.niches,
      categorySearchTerms: filters.categorySearchTerms,
      topicFitMode: filters.topicFitMode,
      excludedNiches: filters.excludedNiches,
      excludedCategorySearchTerms: filters.excludedCategorySearchTerms,
      languages: filters.languages,
      casinoAvailability: filters.casinoAvailability,
      cryptoAvailability: filters.cryptoAvailability,
      linkInsertAvailability: filters.linkInsertAvailability,
      linkInsertCasinoAvailability: filters.linkInsertCasinoAvailability,
      datingAvailability: filters.datingAvailability,
      quarantine: filters.quarantine,
      lastPublishedFromMonth: filters.lastPublishedFromMonth,
      lastPublishedToMonth: filters.lastPublishedToMonth,
    }),
    [
      filters.drMin,
      filters.drMax,
      filters.trafficMin,
      filters.trafficMax,
      filters.priceMin,
      filters.priceMax,
      filters.locationSelections,
      filters.excludedLocationKeys,
      filters.niches,
      filters.categorySearchTerms,
      filters.topicFitMode,
      filters.excludedNiches,
      filters.excludedCategorySearchTerms,
      filters.languages,
      filters.casinoAvailability,
      filters.cryptoAvailability,
      filters.linkInsertAvailability,
      filters.linkInsertCasinoAvailability,
      filters.datingAvailability,
      filters.quarantine,
      filters.lastPublishedFromMonth,
      filters.lastPublishedToMonth,
    ]
  );
  const gridRows: GridRow[] = useMemo(() => {
    if (multiSearchResult === null) {
      return sites;
    }

    const orderedResults = multiSearchResult.results;
    const filteredFoundDomains = new Set(
      filterSites(
        orderedResults.flatMap((result) => (result.found ? [result.site] : [])),
        multiSearchGridFilters
      ).map((site) => site.domain)
    );

    if (isMultiSearchInputOrderSort(sortModel)) {
      return orderedResults.flatMap((result): GridRow[] => {
        if (!result.found) {
          return gridFiltersActive ? [] : [{ domain: result.domain, _isNotFound: true as const }];
        }

        return filteredFoundDomains.has(result.site.domain) ? [result.site] : [];
      });
    }

    const filtered = orderedResults.flatMap((result) =>
      result.found && filteredFoundDomains.has(result.site.domain) ? [result.site] : []
    );
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
      : orderedResults
          .filter((result) => !result.found)
          .map((result) => ({ domain: result.domain, _isNotFound: true as const }));
    return [...sorted, ...notFoundRows];
  }, [multiSearchResult, multiSearchGridFilters, sortModel, sites, gridFiltersActive]);

  return {
    gridRows,
    gridRowCount: isMultiSearchView ? gridRows.length : total,
    gridLoading: loading || multiSearchLoading,
    isMultiSearchView,
  };
}
