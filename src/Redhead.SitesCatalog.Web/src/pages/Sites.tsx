import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import {
  Alert,
  Box,
  Button,
  List,
  ListItem,
  ListItemText,
  Paper,
  Popover,
  Typography,
} from '@mui/material';
import { DataGrid } from '@mui/x-data-grid';
import type {
  GridColumnResizeParams,
  GridPaginationModel,
  GridSortModel,
} from '@mui/x-data-grid';
import { PageShell } from '../components/layout/PageShell';
import { SitesFilters } from '../components/sites/filters/SitesFilters';
import { EditSiteDialog } from '../components/sites/dialogs/EditSiteDialog';
import { GoogleDriveConnectionDialog } from '../components/sites/dialogs/GoogleDriveConnectionDialog';
import { PricingDetailsDrawer } from '../components/sites/dialogs/PricingDetailsDrawer';
import { SitesTableViewToolbar } from '../components/sites/table-view-toolbar/SitesTableViewToolbar';
import { SitesSnackbar } from '../components/sites/feedback/SitesSnackbar';
import type { SitesSnackbarState } from '../components/sites/feedback/SitesSnackbar';
import { insertSitesColumnsByDefaultOrder } from '../components/sites/table-views/sitesTableColumns';
import { useSitesColumns } from '../components/sites/hooks/useSitesColumns';
import { isNotFoundRow, useSitesGridRows } from '../components/sites/hooks/useSitesGridRows';
import { useSitesTableViews } from '../components/sites/table-views/useSitesTableViews';
import { useSitesSavedFilters } from '../components/sites/saved-filters/useSitesSavedFilters';
import {
  COPYABLE_PRICE_COLUMN_LABELS,
  buildPriceColumnClipboardText,
} from '../components/sites/priceColumnClipboard';
import type { CopyablePriceColumn } from '../components/sites/priceColumnClipboard';
import {
  applySavedFilterSettings,
  areSavedFilterSettingsEqual,
  buildSavedFilterSettings,
} from '../components/sites/saved-filters/savedFilters.helpers';
import { useUserRoles } from '../hooks/useUserRoles';
import { useAuth } from '../contexts/AuthContext';
import { useSitesExport } from '../hooks/useSitesExport';
import { dataGridLocaleText } from '../utils/numberFormat';
import type { SavedFilterSet, SavedFilterSettings } from '../types/savedFilters.types';
import type {
  LocationFilterSelection,
  Site,
  SitesFilters as FiltersType,
  SitesQueryParams,
  MultiSearchResponse,
} from '../types/sites.types';
import { sitesService } from '../services/sites.service';
import { normalizeServiceAvailabilityFilter } from '../utils/serviceAvailability';

const INITIAL_FILTERS: FiltersType = {
  search: '',
  drMin: '',
  drMax: '',
  trafficMin: '',
  trafficMax: '',
  priceMin: '',
  priceMax: '',
  termKey: null,
  stopListDomains: [],
  locationSelections: [],
  excludedLocationKeys: [],
  niches: [],
  categorySearchTerms: [],
  topicFitMode: 'expand',
  excludedNiches: [],
  excludedCategorySearchTerms: [],
  languages: [],
  casinoAvailability: [],
  cryptoAvailability: [],
  linkInsertAvailability: [],
  linkInsertCasinoAvailability: [],
  datingAvailability: [],
  quarantine: 'exclude',
  lastPublishedFromMonth: null,
  lastPublishedToMonth: null,
};

const MULTI_SEARCH_DEFAULT_QUARANTINE: FiltersType['quarantine'] = 'all';

const LEGACY_STOP_LIST_STORAGE_KEY = 'redhead.sitesCatalog.stopListDomains';
const FILTER_REQUEST_DEBOUNCE_MS = 300;

function hasAvailabilityFilter(filter: FiltersType['casinoAvailability']): boolean {
  return normalizeServiceAvailabilityFilter(filter).length > 0;
}

function getDefaultQuarantineFilter(multiSearchMode: boolean): FiltersType['quarantine'] {
  return multiSearchMode ? MULTI_SEARCH_DEFAULT_QUARANTINE : INITIAL_FILTERS.quarantine;
}

function hasGridFiltersActive(filters: FiltersType, multiSearchMode: boolean): boolean {
  return (
    filters.drMin !== INITIAL_FILTERS.drMin ||
    filters.drMax !== INITIAL_FILTERS.drMax ||
    filters.trafficMin !== INITIAL_FILTERS.trafficMin ||
    filters.trafficMax !== INITIAL_FILTERS.trafficMax ||
    filters.priceMin !== INITIAL_FILTERS.priceMin ||
    filters.priceMax !== INITIAL_FILTERS.priceMax ||
    filters.termKey !== INITIAL_FILTERS.termKey ||
    filters.locationSelections.length > 0 ||
    filters.excludedLocationKeys.length > 0 ||
    filters.niches.length !== 0 ||
    filters.categorySearchTerms.length !== 0 ||
    filters.excludedNiches.length !== 0 ||
    filters.excludedCategorySearchTerms.length !== 0 ||
    filters.languages.length !== 0 ||
    hasAvailabilityFilter(filters.casinoAvailability) ||
    hasAvailabilityFilter(filters.cryptoAvailability) ||
    hasAvailabilityFilter(filters.linkInsertAvailability) ||
    hasAvailabilityFilter(filters.linkInsertCasinoAvailability) ||
    hasAvailabilityFilter(filters.datingAvailability) ||
    filters.quarantine !== getDefaultQuarantineFilter(multiSearchMode) ||
    filters.lastPublishedFromMonth !== null ||
    filters.lastPublishedToMonth !== null
  );
}

function buildAvailabilityRequestField(filter: FiltersType['casinoAvailability']) {
  const normalizedFilter = normalizeServiceAvailabilityFilter(filter);
  return normalizedFilter.length > 0 ? normalizedFilter : undefined;
}

function buildLocationFilterRequestFields(
  selections: LocationFilterSelection[],
  excludedLocationKeys: string[]
): Pick<
  SitesQueryParams,
  | 'locationKeys'
  | 'locationGroupKeys'
  | 'excludedLocationKeys'
  | 'includeUnknownLocation'
  | 'includeOtherLocation'
> {
  const locationKeys = selections
    .filter((selection): selection is Extract<LocationFilterSelection, { kind: 'location' }> =>
      selection.kind === 'location'
    )
    .map((selection) => selection.key);
  const locationGroupKeys = selections
    .filter((selection): selection is Extract<LocationFilterSelection, { kind: 'group' }> =>
      selection.kind === 'group'
    )
    .map((selection) => selection.key);

  return {
    locationKeys: locationKeys.length > 0 ? locationKeys : undefined,
    locationGroupKeys: locationGroupKeys.length > 0 ? locationGroupKeys : undefined,
    excludedLocationKeys:
      selections.length > 0 && excludedLocationKeys.length > 0 ? excludedLocationKeys : undefined,
    includeUnknownLocation: selections.some(
      (selection) => selection.kind === 'special' && selection.key === 'unknown'
    )
      ? true
      : undefined,
    includeOtherLocation: selections.some(
      (selection) => selection.kind === 'special' && selection.key === 'other'
    )
      ? true
      : undefined,
  };
}

export function Sites() {
  const [sites, setSites] = useState<Site[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [filters, setFilters] = useState<FiltersType>(INITIAL_FILTERS);
  const [debouncedFilters, setDebouncedFilters] = useState<FiltersType>(INITIAL_FILTERS);
  const [multiSearchMode, setMultiSearchMode] = useState(false);
  const [multiSearchResult, setMultiSearchResult] = useState<MultiSearchResponse | null>(null);
  const [multiSearchAppliedText, setMultiSearchAppliedText] = useState('');
  const [multiSearchLoading, setMultiSearchLoading] = useState(false);
  const [multiSearchRunId, setMultiSearchRunId] = useState(0);
  const [filterOptionsRefreshKey, setFilterOptionsRefreshKey] = useState(0);
  const [duplicatesAnchor, setDuplicatesAnchor] = useState<HTMLElement | null>(null);
  const [editSite, setEditSite] = useState<Site | null>(null);
  const [pricingDetailsSite, setPricingDetailsSite] = useState<Site | null>(null);
  const quarantineBeforeMultiSearchRef = useRef<FiltersType['quarantine'] | null>(null);
  const loadSitesRequestIdRef = useRef(0);
  const hasInitializedNormalFilterDebounceRef = useRef(false);
  const [snackbar, setSnackbar] = useState<SitesSnackbarState>({
    open: false,
    message: '',
    severity: 'success',
  });

  const { isAdmin, isClient } = useUserRoles();
  const { user } = useAuth();
  const canExport = !user?.isExportDisabled;
  const tableViews = useSitesTableViews({ isClient });
  const savedFilters = useSitesSavedFilters();
  const { updateDraftColumnWidth } = tableViews;

  useEffect(() => {
    try {
      globalThis.localStorage?.removeItem(LEGACY_STOP_LIST_STORAGE_KEY);
    } catch {
      // Ignore unavailable browser storage; stop-list state is no longer persisted.
    }
  }, []);

  useEffect(() => {
    if (!tableViews.loadError) return;
    setSnackbar({
      open: true,
      message: 'Table views could not be loaded',
      detail: tableViews.loadError,
      severity: 'error',
    });
  }, [tableViews.loadError]);

  useEffect(() => {
    if (!savedFilters.loadError) return;
    setSnackbar({
      open: true,
      message: 'Saved filter sets could not be loaded',
      detail: savedFilters.loadError,
      severity: 'error',
    });
  }, [savedFilters.loadError]);

  const [paginationModel, setPaginationModel] = useState<GridPaginationModel>({
    page: 0,
    pageSize: 25,
  });

  const [sortModel, setSortModel] = useState<GridSortModel>([{ field: 'domain', sort: 'asc' }]);

  const handleFiltersChange = useCallback((nextFilters: FiltersType) => {
    setFilters(nextFilters);
    setPaginationModel((prev) => (prev.page === 0 ? prev : { ...prev, page: 0 }));
  }, []);

  useEffect(() => {
    if (multiSearchMode) {
      setDebouncedFilters(filters);
      return;
    }

    if (!hasInitializedNormalFilterDebounceRef.current) {
      hasInitializedNormalFilterDebounceRef.current = true;
      setDebouncedFilters(filters);
      return;
    }

    const timeoutId = globalThis.setTimeout(() => {
      setDebouncedFilters(filters);
    }, FILTER_REQUEST_DEBOUNCE_MS);

    return () => globalThis.clearTimeout(timeoutId);
  }, [filters, multiSearchMode]);

  const gridFiltersActive = useMemo(
    () => hasGridFiltersActive(filters, multiSearchMode),
    [filters, multiSearchMode]
  );
  const filtersDebouncePending = !multiSearchMode && filters !== debouncedFilters;
  const appliedQueryFilters = multiSearchMode ? filters : debouncedFilters;

  const buildSitesQueryParams = useCallback(
    (page: number, pageSize: number): SitesQueryParams => ({
      page,
      pageSize,
      sortBy: sortModel[0]?.field || 'domain',
      sortDir: sortModel[0]?.sort || 'asc',
      search:
        !multiSearchMode && appliedQueryFilters.search ? appliedQueryFilters.search : undefined,
      drMin: appliedQueryFilters.drMin ? Number(appliedQueryFilters.drMin) : undefined,
      drMax: appliedQueryFilters.drMax ? Number(appliedQueryFilters.drMax) : undefined,
      trafficMin: appliedQueryFilters.trafficMin
        ? Number(appliedQueryFilters.trafficMin)
        : undefined,
      trafficMax: appliedQueryFilters.trafficMax
        ? Number(appliedQueryFilters.trafficMax)
        : undefined,
      priceMin: appliedQueryFilters.priceMin ? Number(appliedQueryFilters.priceMin) : undefined,
      priceMax: appliedQueryFilters.priceMax ? Number(appliedQueryFilters.priceMax) : undefined,
      termKey: appliedQueryFilters.termKey ?? undefined,
      stopListDomains:
        !multiSearchMode && appliedQueryFilters.stopListDomains.length > 0
          ? appliedQueryFilters.stopListDomains
          : undefined,
      ...buildLocationFilterRequestFields(
        appliedQueryFilters.locationSelections,
        appliedQueryFilters.excludedLocationKeys
      ),
      niches: appliedQueryFilters.niches.length > 0 ? appliedQueryFilters.niches : undefined,
      categorySearchTerms:
        appliedQueryFilters.categorySearchTerms.length > 0
          ? appliedQueryFilters.categorySearchTerms
          : undefined,
      topicFitMode: appliedQueryFilters.topicFitMode,
      excludedNiches:
        appliedQueryFilters.excludedNiches.length > 0
          ? appliedQueryFilters.excludedNiches
          : undefined,
      excludedCategorySearchTerms:
        appliedQueryFilters.excludedCategorySearchTerms.length > 0
          ? appliedQueryFilters.excludedCategorySearchTerms
          : undefined,
      languages:
        appliedQueryFilters.languages.length > 0 ? appliedQueryFilters.languages : undefined,
      casinoAvailability: buildAvailabilityRequestField(appliedQueryFilters.casinoAvailability),
      cryptoAvailability: buildAvailabilityRequestField(appliedQueryFilters.cryptoAvailability),
      linkInsertAvailability: buildAvailabilityRequestField(
        appliedQueryFilters.linkInsertAvailability
      ),
      linkInsertCasinoAvailability: buildAvailabilityRequestField(
        appliedQueryFilters.linkInsertCasinoAvailability
      ),
      datingAvailability: buildAvailabilityRequestField(appliedQueryFilters.datingAvailability),
      quarantine: appliedQueryFilters.quarantine,
      lastPublishedFromMonth: appliedQueryFilters.lastPublishedFromMonth ?? undefined,
      lastPublishedToMonth: appliedQueryFilters.lastPublishedToMonth ?? undefined,
    }),
    [
      sortModel,
      appliedQueryFilters,
      multiSearchMode,
    ]
  );

  const {
    exporting,
    googleDriveStatus,
    exportUsageLimits,
    googleDriveDialog,
    connectingGoogleDrive,
    handleDownloadExport,
    handleSaveToGoogleDrive,
    handleConnectGoogleDrive,
    closeGoogleDriveDialog,
  } = useSitesExport({
    buildSitesQueryParams,
    isClient,
    multiSearchResult,
    searchText: multiSearchAppliedText,
    visibleColumnKeys: tableViews.visibleColumnIds,
    showSnackbar: setSnackbar,
  });

  const loadSites = useCallback(async () => {
    const requestId = loadSitesRequestIdRef.current + 1;
    loadSitesRequestIdRef.current = requestId;
    setLoading(true);
    try {
      const params = buildSitesQueryParams(
        paginationModel.page + 1, // API uses 1-based pagination
        paginationModel.pageSize
      );

      const response = await sitesService.getSites(params);
      if (requestId !== loadSitesRequestIdRef.current) {
        return;
      }

      setSites(response.items);
      setTotal(response.total);
    } catch (error) {
      if (requestId !== loadSitesRequestIdRef.current) {
        return;
      }

      console.error('Failed to load sites:', error);
      setSites([]);
      setTotal(0);
    } finally {
      if (requestId === loadSitesRequestIdRef.current) {
        setLoading(false);
      }
    }
  }, [paginationModel, buildSitesQueryParams]);

  useEffect(() => {
    if (multiSearchMode || filtersDebouncePending) return;
    loadSites();
  }, [filtersDebouncePending, loadSites, multiSearchMode]);

  const handleFiltersApply = (appliedFilters: FiltersType = filters) => {
    setPaginationModel((prev) => ({ ...prev, page: 0 }));
    if (!multiSearchMode) {
      setDebouncedFilters(appliedFilters);
    }
    if (multiSearchMode) {
      const query = appliedFilters.search.trim();
      if (!query) return;
      setMultiSearchLoading(true);
      sitesService
        .multiSearch(query)
        .then((res) => {
          setPaginationModel((prev) => ({ ...prev, page: 0 }));
          setMultiSearchResult(res);
          setMultiSearchAppliedText(query);
          setMultiSearchRunId((current) => current + 1);
        })
        .catch((err) => {
          console.error('Multi-search failed:', err);
          setSnackbar({
            open: true,
            message: err instanceof Error ? err.message : 'Multi-search failed',
            severity: 'error',
          });
        })
        .finally(() => setMultiSearchLoading(false));
    }
  };

  const handleApplySavedFilterSet = (filterSet: SavedFilterSet) => {
    const nextFilters = applySavedFilterSettings(filters, filterSet.settings, {
      multiSearchMode,
    });
    savedFilters.setActiveFilterSetId(filterSet.id);
    setFilters(nextFilters);
    setPaginationModel((prev) => ({ ...prev, page: 0 }));
    handleFiltersApply(nextFilters);
  };

  const handleCreateSavedFilterSet = async (
    name: string,
    settings: SavedFilterSettings
  ) => {
    const created = await savedFilters.createFilterSet(name, settings);
    const currentSettings = buildSavedFilterSettings(filters, {
      includeStopListDomains: !multiSearchMode && filters.stopListDomains.length > 0,
    });

    if (areSavedFilterSettingsEqual(currentSettings, created.settings)) {
      savedFilters.setActiveFilterSetId(created.id);
    } else {
      savedFilters.setActiveFilterSetId(null);
    }

    setSnackbar({
      open: true,
      message: `Saved filter set: ${created.name}`,
      severity: 'success',
    });
  };

  const handleUpdateSavedFilterSet = async (
    id: string,
    settings: SavedFilterSettings
  ) => {
    const updated = await savedFilters.updateFilterSet(id, undefined, settings);
    setSnackbar({
      open: true,
      message: `Updated filter set: ${updated.name}`,
      severity: 'success',
    });
  };

  const handleRenameSavedFilterSet = async (id: string, name: string) => {
    const updated = await savedFilters.updateFilterSet(id, name);
    setSnackbar({
      open: true,
      message: `Renamed filter set: ${updated.name}`,
      severity: 'success',
    });
  };

  const handleDeleteSavedFilterSet = async (id: string) => {
    await savedFilters.deleteFilterSet(id);
    setSnackbar({
      open: true,
      message: 'Saved filter set deleted',
      severity: 'success',
    });
  };

  const handleMultiSearchModeChange = (enabled: boolean) => {
    setMultiSearchMode(enabled);
    setPaginationModel((prev) => ({ ...prev, page: 0 }));
    if (enabled) {
      quarantineBeforeMultiSearchRef.current = filters.quarantine;
      if (filters.quarantine === INITIAL_FILTERS.quarantine) {
        setFilters((current) => ({
          ...current,
          quarantine:
            current.quarantine === INITIAL_FILTERS.quarantine
              ? MULTI_SEARCH_DEFAULT_QUARANTINE
              : current.quarantine,
        }));
      }
      return;
    }

    const previousQuarantine = quarantineBeforeMultiSearchRef.current;
    quarantineBeforeMultiSearchRef.current = null;
    if (
      previousQuarantine !== null &&
      previousQuarantine !== MULTI_SEARCH_DEFAULT_QUARANTINE &&
      filters.quarantine === MULTI_SEARCH_DEFAULT_QUARANTINE
    ) {
      setFilters((current) => ({
        ...current,
        quarantine:
          current.quarantine === MULTI_SEARCH_DEFAULT_QUARANTINE
            ? previousQuarantine
            : current.quarantine,
      }));
    }
    if (!enabled) {
      setMultiSearchResult(null);
      setMultiSearchAppliedText('');
      setDebouncedFilters(filters);
      setMultiSearchRunId(0);
    }
  };

  const handleCloseSnackbar = () => {
    setSnackbar((s) => ({ ...s, open: false }));
  };

  const handleOpenEdit = useCallback((site: Site) => {
    setEditSite(site);
  }, []);

  const handleOpenPricingDetails = useCallback((site: Site) => {
    setPricingDetailsSite(site);
  }, []);

  const handleCloseEdit = () => {
    setEditSite(null);
  };

  const handleEditSaved = (updated: Site) => {
    setEditSite(null);
    setFilterOptionsRefreshKey((key) => key + 1);
    setSnackbar({ open: true, message: 'Site updated', severity: 'success' });
    setPricingDetailsSite((current) => (current?.domain === updated.domain ? updated : current));
    if (multiSearchResult) {
      const newFound = multiSearchResult.found.map((s) =>
        s.domain === updated.domain ? updated : s
      );
      const newResults = multiSearchResult.results.map((result) =>
        result.found && result.site.domain === updated.domain
          ? { ...result, site: updated }
          : result
      );
      setMultiSearchResult({ ...multiSearchResult, results: newResults, found: newFound });
    } else {
      loadSites();
    }
  };

  const { gridRows, gridRowCount, gridLoading, isMultiSearchView } = useSitesGridRows({
    sites,
    total,
    loading,
    multiSearchLoading,
    multiSearchResult,
    filters,
    sortModel,
    gridFiltersActive,
  });
  const gridNotFoundRowCount = useMemo(
    () => (isMultiSearchView ? gridRows.filter(isNotFoundRow).length : 0),
    [gridRows, isMultiSearchView]
  );
  const hiddenNotFoundRowCount =
    multiSearchResult !== null && gridFiltersActive ? multiSearchResult.notFound.length : 0;
  const gridSearchedRowCount = multiSearchResult?.results.length ?? 0;

  const handleCopyPriceColumn = useCallback(
    async (field: CopyablePriceColumn) => {
      if (!isMultiSearchView || isClient) return;

      const label = COPYABLE_PRICE_COLUMN_LABELS[field];
      try {
        if (multiSearchResult === null) return;

        const text = buildPriceColumnClipboardText(multiSearchResult.results, field);
        if (!navigator.clipboard?.writeText) {
          throw new Error('Clipboard API is not available in this browser.');
        }

        await navigator.clipboard.writeText(text);
        setSnackbar({
          open: true,
          message: `Copied ${label} values in Multi-search input order (${multiSearchResult.results.length} rows)`,
          severity: 'success',
        });
      } catch (error) {
        setSnackbar({
          open: true,
          message: `Could not copy ${label} values`,
          detail: error instanceof Error ? error.message : undefined,
          severity: 'error',
        });
      }
    },
    [isClient, isMultiSearchView, multiSearchResult]
  );

  const columns = useSitesColumns({
    isAdmin,
    isClient,
    isMultiSearchView,
    visibleColumnIds: tableViews.visibleColumnIds,
    columnWidths: tableViews.columnWidths,
    density: tableViews.density,
    onEdit: handleOpenEdit,
    onCopyPriceColumn: isMultiSearchView && !isClient ? handleCopyPriceColumn : undefined,
    onViewPricing: handleOpenPricingDetails,
  });

  const handleColumnWidthChange = useCallback(
    (params: GridColumnResizeParams) => {
      updateDraftColumnWidth(params.colDef.field, params.width);
    },
    [updateDraftColumnWidth]
  );

  const hiddenFilteredColumnIds = useMemo(() => {
    const activeColumnIds = new Set<string>();

    if (filters.drMin || filters.drMax) activeColumnIds.add('dr');
    if (filters.trafficMin || filters.trafficMax) activeColumnIds.add('traffic');
    if (filters.priceMin || filters.priceMax) activeColumnIds.add('priceUsd');
    if (filters.termKey !== INITIAL_FILTERS.termKey) activeColumnIds.add('priceUsd');
    if (filters.locationSelections.length > 0 || filters.excludedLocationKeys.length > 0) {
      activeColumnIds.add('location');
    }
    if (filters.niches.length > 0 || filters.excludedNiches.length > 0) activeColumnIds.add('niche');
    if (filters.categorySearchTerms.length > 0 || filters.excludedCategorySearchTerms.length > 0) activeColumnIds.add('categories');
    if (filters.languages.length > 0) activeColumnIds.add('language');
    if (hasAvailabilityFilter(filters.casinoAvailability))
      activeColumnIds.add('priceCasino');
    if (hasAvailabilityFilter(filters.cryptoAvailability))
      activeColumnIds.add('priceCrypto');
    if (hasAvailabilityFilter(filters.linkInsertAvailability)) {
      activeColumnIds.add('priceLinkInsert');
    }
    if (hasAvailabilityFilter(filters.linkInsertCasinoAvailability)) {
      activeColumnIds.add('priceLinkInsertCasino');
    }
    if (hasAvailabilityFilter(filters.datingAvailability))
      activeColumnIds.add('priceDating');
    if (filters.quarantine !== getDefaultQuarantineFilter(multiSearchMode)) {
      activeColumnIds.add('isQuarantined');
    }
    if (filters.lastPublishedFromMonth !== null || filters.lastPublishedToMonth !== null) {
      activeColumnIds.add('lastPublishedDate');
    }

    const visibleColumnIds = new Set(tableViews.visibleColumnIds);
    const configurableColumnIds = new Set(tableViews.allowedViewColumns.map((column) => column.id));
    return [...activeColumnIds].filter(
      (columnId) => configurableColumnIds.has(columnId) && !visibleColumnIds.has(columnId)
    );
  }, [
    filters.drMin,
    filters.drMax,
    filters.trafficMin,
    filters.trafficMax,
    filters.priceMin,
    filters.priceMax,
    filters.termKey,
    filters.locationSelections,
    filters.excludedLocationKeys,
    filters.niches,
    filters.categorySearchTerms,
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
    tableViews.allowedViewColumns,
    tableViews.visibleColumnIds,
    multiSearchMode,
  ]);

  const hiddenFilteredColumns = useMemo(
    () =>
      tableViews.allowedViewColumns.filter((column) => hiddenFilteredColumnIds.includes(column.id)),
    [hiddenFilteredColumnIds, tableViews.allowedViewColumns]
  );

  const savedFilterSetChanged = useMemo(() => {
    const activeFilterSet = savedFilters.activeFilterSet;
    if (!activeFilterSet) return false;

    const currentSettings = buildSavedFilterSettings(filters, {
      includeStopListDomains:
        !multiSearchMode &&
        (Array.isArray(activeFilterSet.settings.stopListDomains) ||
          filters.stopListDomains.length > 0),
    });
    const activeSettings = {
      ...activeFilterSet.settings,
      stopListDomains: !multiSearchMode
        ? (activeFilterSet.settings.stopListDomains ?? null)
        : null,
    };

    return !areSavedFilterSettingsEqual(currentSettings, activeSettings);
  }, [filters, multiSearchMode, savedFilters.activeFilterSet]);

  const handleShowFilteredColumns = () => {
    tableViews.updateDraftVisibleColumns(
      insertSitesColumnsByDefaultOrder(
        tableViews.visibleColumnIds,
        hiddenFilteredColumnIds,
        tableViews.allowedViewColumns
      )
    );
  };

  const handleClearHiddenFilters = () => {
    const hidden = new Set(hiddenFilteredColumnIds);
    if (hidden.size === 0) return;

    const nextFilters = {
      ...filters,
      drMin: hidden.has('dr') ? INITIAL_FILTERS.drMin : filters.drMin,
      drMax: hidden.has('dr') ? INITIAL_FILTERS.drMax : filters.drMax,
      trafficMin: hidden.has('traffic') ? INITIAL_FILTERS.trafficMin : filters.trafficMin,
      trafficMax: hidden.has('traffic') ? INITIAL_FILTERS.trafficMax : filters.trafficMax,
      priceMin: hidden.has('priceUsd') ? INITIAL_FILTERS.priceMin : filters.priceMin,
      priceMax: hidden.has('priceUsd') ? INITIAL_FILTERS.priceMax : filters.priceMax,
      termKey: hidden.has('priceUsd') ? INITIAL_FILTERS.termKey : filters.termKey,
      locationSelections: hidden.has('location')
        ? INITIAL_FILTERS.locationSelections
        : filters.locationSelections,
      excludedLocationKeys: hidden.has('location')
        ? INITIAL_FILTERS.excludedLocationKeys
        : filters.excludedLocationKeys,
      niches: hidden.has('niche') ? INITIAL_FILTERS.niches : filters.niches,
      excludedNiches: hidden.has('niche')
        ? INITIAL_FILTERS.excludedNiches
        : filters.excludedNiches,
      categorySearchTerms: hidden.has('categories')
        ? INITIAL_FILTERS.categorySearchTerms
        : filters.categorySearchTerms,
      excludedCategorySearchTerms: hidden.has('categories')
        ? INITIAL_FILTERS.excludedCategorySearchTerms
        : filters.excludedCategorySearchTerms,
      languages: hidden.has('language') ? INITIAL_FILTERS.languages : filters.languages,
      casinoAvailability: hidden.has('priceCasino')
        ? INITIAL_FILTERS.casinoAvailability
        : filters.casinoAvailability,
      cryptoAvailability: hidden.has('priceCrypto')
        ? INITIAL_FILTERS.cryptoAvailability
        : filters.cryptoAvailability,
      linkInsertAvailability: hidden.has('priceLinkInsert')
        ? INITIAL_FILTERS.linkInsertAvailability
        : filters.linkInsertAvailability,
      linkInsertCasinoAvailability: hidden.has('priceLinkInsertCasino')
        ? INITIAL_FILTERS.linkInsertCasinoAvailability
        : filters.linkInsertCasinoAvailability,
      datingAvailability: hidden.has('priceDating')
        ? INITIAL_FILTERS.datingAvailability
        : filters.datingAvailability,
      quarantine: hidden.has('isQuarantined')
        ? getDefaultQuarantineFilter(multiSearchMode)
        : filters.quarantine,
      lastPublishedFromMonth: hidden.has('lastPublishedDate')
        ? INITIAL_FILTERS.lastPublishedFromMonth
        : filters.lastPublishedFromMonth,
      lastPublishedToMonth: hidden.has('lastPublishedDate')
        ? INITIAL_FILTERS.lastPublishedToMonth
        : filters.lastPublishedToMonth,
    };

    setFilters(nextFilters);
    if (!multiSearchMode) {
      setDebouncedFilters(nextFilters);
    }
    setPaginationModel((prev) => ({ ...prev, page: 0 }));
  };

  return (
    <PageShell maxWidth="xl">
      <Box sx={{ display: 'flex', flex: 1, minHeight: 0, flexDirection: 'column' }}>
        <Box sx={{ mb: 1 }}>
          <Typography variant="h4">Sites Catalog</Typography>
        </Box>

        <SitesFilters
          filters={filters}
          onFiltersChange={handleFiltersChange}
          onApply={handleFiltersApply}
          multiSearchMode={multiSearchMode}
          onMultiSearchModeChange={handleMultiSearchModeChange}
          filterOptionsRefreshKey={filterOptionsRefreshKey}
          savedFilterSets={savedFilters.filterSets}
          activeSavedFilterSetId={savedFilters.activeFilterSetId}
          savedFiltersLoading={savedFilters.loading}
          savedFilterSetChanged={savedFilterSetChanged}
          onClearSavedFilterSetSelection={() => savedFilters.setActiveFilterSetId(null)}
          onApplySavedFilterSet={handleApplySavedFilterSet}
          onCreateSavedFilterSet={handleCreateSavedFilterSet}
          onUpdateSavedFilterSet={handleUpdateSavedFilterSet}
          onRenameSavedFilterSet={handleRenameSavedFilterSet}
          onDeleteSavedFilterSet={handleDeleteSavedFilterSet}
        />

        {multiSearchResult && multiSearchResult.duplicates.length > 0 && (
          <Alert
            severity="warning"
            sx={{ mb: 1.5 }}
            action={
              <Button
                size="small"
                variant="outlined"
                onClick={(e) => setDuplicatesAnchor(e.currentTarget)}
                sx={{
                  borderColor: 'divider',
                  color: 'text.primary',
                  bgcolor: 'background.paper',
                  flexShrink: 0,
                }}
              >
                View list
              </Button>
            }
          >
            Duplicates removed: {multiSearchResult.duplicates.length}
          </Alert>
        )}

        <Paper
          sx={{
            display: 'flex',
            flex: 1,
            minHeight: { xs: 520, md: 420 },
            overflow: 'hidden',
            flexDirection: 'column',
          }}
        >
          <SitesTableViewToolbar
            tableViews={tableViews}
            hiddenFilteredColumns={hiddenFilteredColumns}
            canExport={canExport}
            exporting={exporting}
            loading={loading || tableViews.loading}
            exportUsageLimits={exportUsageLimits}
            resultCount={gridRowCount}
            resultSearchedCount={gridSearchedRowCount}
            resultNotFoundCount={gridNotFoundRowCount}
            resultHiddenNotFoundCount={hiddenNotFoundRowCount}
            resultLoading={gridLoading}
            onShowFilteredColumns={handleShowFilteredColumns}
            onClearHiddenFilters={handleClearHiddenFilters}
            onDownloadExcel={handleDownloadExport}
            onSaveToGoogleDrive={handleSaveToGoogleDrive}
            onSuccess={(message) => setSnackbar({ open: true, message, severity: 'success' })}
            onError={(message) => setSnackbar({ open: true, message, severity: 'error' })}
          />

          <Box
            sx={{
              flex: 1,
              minHeight: { xs: 440, md: 360 },
            }}
          >
            <DataGrid
              key={isMultiSearchView ? `multi-search-${multiSearchRunId}` : 'sites'}
              rows={gridRows}
              columns={columns}
              getRowId={(row) => (isNotFoundRow(row) ? `notfound:${row.domain}` : row.domain)}
              getRowClassName={(params) => {
                if (isNotFoundRow(params.row)) return 'SitesGrid-notFoundRow';
                return params.row.isQuarantined ? 'SitesGrid-unavailableRow' : '';
              }}
              rowCount={gridRowCount}
              loading={gridLoading}
              pageSizeOptions={[10, 25, 50, 100]}
              paginationModel={paginationModel}
              paginationMode={isMultiSearchView ? 'client' : 'server'}
              onPaginationModelChange={setPaginationModel}
              sortingMode="server"
              sortModel={sortModel}
              onSortModelChange={setSortModel}
              onColumnWidthChange={handleColumnWidthChange}
              density={tableViews.density}
              columnVisibilityModel={tableViews.columnVisibilityModel}
              localeText={dataGridLocaleText}
              disableRowSelectionOnClick
              disableColumnMenu
              // Keep manually pinned Domain cells mounted while users scroll across Full view columns.
              disableVirtualization
              sx={{
                borderTopLeftRadius: 0,
                borderTopRightRadius: 0,
                backgroundColor: 'background.paper',
                '& .MuiDataGrid-cell': {
                  display: 'flex',
                  alignItems: 'center',
                  py: 1,
                },
                '& .MuiDataGrid-cell:focus': {
                  outline: 'none',
                },
                '& .MuiDataGrid-cell:focus-within': {
                  outline: 'none',
                },
                '& .MuiDataGrid-container--top': {
                  zIndex: 40,
                  backgroundColor: 'grey.100',
                },
                '& .MuiDataGrid-columnHeaders': {
                  backgroundColor: 'grey.100',
                },
                '& .MuiDataGrid-columnHeader': {
                  backgroundColor: 'grey.100',
                },
                '& .MuiDataGrid-columnHeader:focus': {
                  outline: 'none',
                },
                '& .MuiDataGrid-columnHeader:focus-within': {
                  outline: 'none',
                },
                '& .SitesGrid-domainCell, & .SitesGrid-domainHeader': {
                  position: 'sticky',
                  left: 0,
                  boxSizing: 'border-box',
                  overflow: 'hidden',
                  borderRight: '1px solid',
                  borderRightColor: 'divider',
                  boxShadow: '6px 0 10px -12px rgba(0, 0, 0, 0.5)',
                  backgroundClip: 'border-box',
                },
                '& .SitesGrid-domainCell': {
                  zIndex: 50,
                  backgroundColor: 'background.paper',
                },
                '& .SitesGrid-domainHeader': {
                  zIndex: 60,
                  backgroundColor: 'grey.100',
                },
                '& .MuiDataGrid-row:hover .SitesGrid-domainCell': {
                  backgroundColor: 'grey.50',
                },
                '& .MuiDataGrid-row.Mui-selected .SitesGrid-domainCell': {
                  backgroundColor: 'grey.100',
                },
                '& .MuiDataGrid-row.Mui-selected:hover .SitesGrid-domainCell': {
                  backgroundColor: 'grey.100',
                },
                '& .SitesGrid-unavailableRow .MuiDataGrid-cell': {
                  bgcolor: '#fffaf2',
                },
                '& .SitesGrid-unavailableRow .SitesGrid-domainCell': {
                  bgcolor: '#fffaf2',
                  backgroundImage: 'linear-gradient(to right, #f59e0b 0 3px, transparent 3px)',
                  backgroundRepeat: 'no-repeat',
                },
                '& .SitesGrid-unavailableRow:hover .MuiDataGrid-cell': {
                  bgcolor: '#fff4e5',
                },
                '& .SitesGrid-unavailableRow:hover .SitesGrid-domainCell': {
                  bgcolor: '#fff4e5',
                  backgroundImage: 'linear-gradient(to right, #f59e0b 0 3px, transparent 3px)',
                  backgroundRepeat: 'no-repeat',
                },
                '& .SitesGrid-notFoundRow .MuiDataGrid-cell': {
                  bgcolor: '#fff4e5',
                },
                '& .SitesGrid-notFoundRow .SitesGrid-domainCell': {
                  bgcolor: '#fff4e5',
                },
                '& .SitesGrid-notFoundRow:hover .MuiDataGrid-cell': {
                  bgcolor: '#ffedda',
                },
                '& .SitesGrid-notFoundRow:hover .SitesGrid-domainCell': {
                  bgcolor: '#ffedda',
                },
              }}
            />
          </Box>
        </Paper>

        <Popover
          open={Boolean(duplicatesAnchor)}
          anchorEl={duplicatesAnchor}
          onClose={() => setDuplicatesAnchor(null)}
          anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
          transformOrigin={{ vertical: 'top', horizontal: 'left' }}
        >
          <List dense sx={{ maxHeight: 300, overflow: 'auto', minWidth: 200 }}>
            {multiSearchResult?.duplicates.map((d) => (
              <ListItem key={d}>
                <ListItemText primary={d} />
              </ListItem>
            ))}
          </List>
        </Popover>

        <SitesSnackbar snackbar={snackbar} onClose={handleCloseSnackbar} />

        <GoogleDriveConnectionDialog
          dialog={googleDriveDialog}
          status={googleDriveStatus}
          connecting={connectingGoogleDrive}
          onClose={closeGoogleDriveDialog}
          onConnect={handleConnectGoogleDrive}
        />

        <EditSiteDialog
          open={Boolean(editSite)}
          site={editSite}
          onClose={handleCloseEdit}
          onSaved={handleEditSaved}
        />

        <PricingDetailsDrawer
          open={Boolean(pricingDetailsSite)}
          site={pricingDetailsSite}
          visibleColumnIds={tableViews.visibleColumnIds}
          onClose={() => setPricingDetailsSite(null)}
        />
      </Box>
    </PageShell>
  );
}
