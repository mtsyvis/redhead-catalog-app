import { useState, useEffect, useCallback, useMemo } from 'react';
import {
  Alert,
  Box,
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
import { SitesTableViewToolbar } from '../components/sites/table-view-toolbar/SitesTableViewToolbar';
import { SitesSnackbar } from '../components/sites/feedback/SitesSnackbar';
import type { SitesSnackbarState } from '../components/sites/feedback/SitesSnackbar';
import { insertSitesColumnsByDefaultOrder } from '../components/sites/table-views/sitesTableColumns';
import { useSitesColumns } from '../components/sites/hooks/useSitesColumns';
import { isNotFoundRow, useSitesGridRows } from '../components/sites/hooks/useSitesGridRows';
import { useSitesTableViews } from '../components/sites/table-views/useSitesTableViews';
import { useSitesSavedFilters } from '../components/sites/saved-filters/useSitesSavedFilters';
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
import { BrandButton } from '../components/common/BrandButton';
import { normalizeServiceAvailabilityFilter } from '../utils/serviceAvailability';

const INITIAL_FILTERS: FiltersType = {
  search: '',
  drMin: '',
  drMax: '',
  trafficMin: '',
  trafficMax: '',
  priceMin: '',
  priceMax: '',
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

const LEGACY_STOP_LIST_STORAGE_KEY = 'redhead.sitesCatalog.stopListDomains';

function hasLocationFilters(filters: FiltersType): boolean {
  return filters.locationSelections.length > 0 || filters.excludedLocationKeys.length > 0;
}

function hasAvailabilityFilter(filter: FiltersType['casinoAvailability']): boolean {
  return normalizeServiceAvailabilityFilter(filter).length > 0;
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
  const [debouncedSearch, setDebouncedSearch] = useState(INITIAL_FILTERS.search);
  const [multiSearchMode, setMultiSearchMode] = useState(false);
  const [multiSearchResult, setMultiSearchResult] = useState<MultiSearchResponse | null>(null);
  const [multiSearchLoading, setMultiSearchLoading] = useState(false);
  const [filterOptionsRefreshKey, setFilterOptionsRefreshKey] = useState(0);
  const [duplicatesAnchor, setDuplicatesAnchor] = useState<HTMLElement | null>(null);
  const [editSite, setEditSite] = useState<Site | null>(null);
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
      setDebouncedSearch(filters.search);
      return;
    }

    const timeoutId = globalThis.setTimeout(() => {
      setDebouncedSearch(filters.search);
    }, 500);

    return () => globalThis.clearTimeout(timeoutId);
  }, [filters.search, multiSearchMode]);

  const effectiveSearch = multiSearchMode ? filters.search : debouncedSearch;

  /** Grid filters active = any filter differs from default (excluding search text). */
  const gridFiltersActive = useMemo(
    () =>
      filters.drMin !== INITIAL_FILTERS.drMin ||
      filters.drMax !== INITIAL_FILTERS.drMax ||
      filters.trafficMin !== INITIAL_FILTERS.trafficMin ||
      filters.trafficMax !== INITIAL_FILTERS.trafficMax ||
      filters.priceMin !== INITIAL_FILTERS.priceMin ||
      filters.priceMax !== INITIAL_FILTERS.priceMax ||
      hasLocationFilters(filters) ||
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
      filters.quarantine !== INITIAL_FILTERS.quarantine ||
      filters.lastPublishedFromMonth !== null ||
      filters.lastPublishedToMonth !== null,
    [filters]
  );

  const buildSitesQueryParams = useCallback(
    (page: number, pageSize: number): SitesQueryParams => ({
      page,
      pageSize,
      sortBy: sortModel[0]?.field || 'domain',
      sortDir: sortModel[0]?.sort || 'asc',
      search: effectiveSearch || undefined,
      drMin: filters.drMin ? Number(filters.drMin) : undefined,
      drMax: filters.drMax ? Number(filters.drMax) : undefined,
      trafficMin: filters.trafficMin ? Number(filters.trafficMin) : undefined,
      trafficMax: filters.trafficMax ? Number(filters.trafficMax) : undefined,
      priceMin: filters.priceMin ? Number(filters.priceMin) : undefined,
      priceMax: filters.priceMax ? Number(filters.priceMax) : undefined,
      stopListDomains:
        !multiSearchMode && filters.stopListDomains.length > 0
          ? filters.stopListDomains
          : undefined,
      ...buildLocationFilterRequestFields(
        filters.locationSelections,
        filters.excludedLocationKeys
      ),
      niches: filters.niches.length > 0 ? filters.niches : undefined,
      categorySearchTerms:
        filters.categorySearchTerms.length > 0 ? filters.categorySearchTerms : undefined,
      topicFitMode: filters.topicFitMode,
      excludedNiches: filters.excludedNiches.length > 0 ? filters.excludedNiches : undefined,
      excludedCategorySearchTerms:
        filters.excludedCategorySearchTerms.length > 0
          ? filters.excludedCategorySearchTerms
          : undefined,
      languages: filters.languages.length > 0 ? filters.languages : undefined,
      casinoAvailability: buildAvailabilityRequestField(filters.casinoAvailability),
      cryptoAvailability: buildAvailabilityRequestField(filters.cryptoAvailability),
      linkInsertAvailability: buildAvailabilityRequestField(filters.linkInsertAvailability),
      linkInsertCasinoAvailability: buildAvailabilityRequestField(
        filters.linkInsertCasinoAvailability
      ),
      datingAvailability: buildAvailabilityRequestField(filters.datingAvailability),
      quarantine: filters.quarantine,
      lastPublishedFromMonth: filters.lastPublishedFromMonth ?? undefined,
      lastPublishedToMonth: filters.lastPublishedToMonth ?? undefined,
    }),
    [
      sortModel,
      effectiveSearch,
      filters.drMin,
      filters.drMax,
      filters.trafficMin,
      filters.trafficMax,
      filters.priceMin,
      filters.priceMax,
      filters.stopListDomains,
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
      multiSearchMode,
    ]
  );

  const {
    exporting,
    googleDriveStatus,
    googleDriveDialog,
    connectingGoogleDrive,
    handleDownloadExport,
    handleSaveToGoogleDrive,
    handleConnectGoogleDrive,
    closeGoogleDriveDialog,
  } = useSitesExport({
    buildSitesQueryParams,
    multiSearchResult,
    searchText: filters.search,
    visibleColumnKeys: tableViews.visibleColumnIds,
    showSnackbar: setSnackbar,
  });

  const loadSites = useCallback(async () => {
    setLoading(true);
    try {
      const params = buildSitesQueryParams(
        paginationModel.page + 1, // API uses 1-based pagination
        paginationModel.pageSize
      );

      const response = await sitesService.getSites(params);
      setSites(response.items);
      setTotal(response.total);
    } catch (error) {
      console.error('Failed to load sites:', error);
      setSites([]);
      setTotal(0);
    } finally {
      setLoading(false);
    }
  }, [paginationModel, buildSitesQueryParams]);

  useEffect(() => {
    if (multiSearchMode) return;
    loadSites();
  }, [loadSites, multiSearchMode]);

  const handleFiltersApply = (appliedFilters: FiltersType = filters) => {
    setPaginationModel((prev) => ({ ...prev, page: 0 }));
    if (!multiSearchMode) {
      setDebouncedSearch(appliedFilters.search);
    }
    if (multiSearchMode) {
      const query = appliedFilters.search.trim();
      if (!query) return;
      setMultiSearchLoading(true);
      sitesService
        .multiSearch(query)
        .then((res) => setMultiSearchResult(res))
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
    if (!enabled) {
      setMultiSearchResult(null);
      setDebouncedSearch(filters.search);
    }
  };

  const handleClearFilters = () => {
    setMultiSearchMode(false);
    setMultiSearchResult(null);
    setFilters(INITIAL_FILTERS);
    setDebouncedSearch(INITIAL_FILTERS.search);
    savedFilters.setActiveFilterSetId(null);
    setPaginationModel((prev) => ({ ...prev, page: 0 }));
  };

  const handleCloseSnackbar = () => {
    setSnackbar((s) => ({ ...s, open: false }));
  };

  const handleOpenEdit = useCallback((site: Site) => {
    setEditSite(site);
  }, []);

  const handleCloseEdit = () => {
    setEditSite(null);
  };

  const handleEditSaved = (updated: Site) => {
    setEditSite(null);
    setFilterOptionsRefreshKey((key) => key + 1);
    setSnackbar({ open: true, message: 'Site updated', severity: 'success' });
    if (multiSearchResult) {
      const newFound = multiSearchResult.found.map((s) =>
        s.domain === updated.domain ? updated : s
      );
      setMultiSearchResult({ ...multiSearchResult, found: newFound });
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

  const columns = useSitesColumns({
    isAdmin,
    isClient,
    visibleColumnIds: tableViews.visibleColumnIds,
    columnWidths: tableViews.columnWidths,
    onEdit: handleOpenEdit,
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
    if (filters.quarantine !== INITIAL_FILTERS.quarantine) activeColumnIds.add('isQuarantined');
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

    setFilters((current) => ({
      ...current,
      drMin: hidden.has('dr') ? INITIAL_FILTERS.drMin : current.drMin,
      drMax: hidden.has('dr') ? INITIAL_FILTERS.drMax : current.drMax,
      trafficMin: hidden.has('traffic') ? INITIAL_FILTERS.trafficMin : current.trafficMin,
      trafficMax: hidden.has('traffic') ? INITIAL_FILTERS.trafficMax : current.trafficMax,
      priceMin: hidden.has('priceUsd') ? INITIAL_FILTERS.priceMin : current.priceMin,
      priceMax: hidden.has('priceUsd') ? INITIAL_FILTERS.priceMax : current.priceMax,
      locationSelections: hidden.has('location')
        ? INITIAL_FILTERS.locationSelections
        : current.locationSelections,
      excludedLocationKeys: hidden.has('location')
        ? INITIAL_FILTERS.excludedLocationKeys
        : current.excludedLocationKeys,
      niches: hidden.has('niche') ? INITIAL_FILTERS.niches : current.niches,
      excludedNiches: hidden.has('niche')
        ? INITIAL_FILTERS.excludedNiches
        : current.excludedNiches,
      categorySearchTerms: hidden.has('categories')
        ? INITIAL_FILTERS.categorySearchTerms
        : current.categorySearchTerms,
      excludedCategorySearchTerms: hidden.has('categories')
        ? INITIAL_FILTERS.excludedCategorySearchTerms
        : current.excludedCategorySearchTerms,
      languages: hidden.has('language') ? INITIAL_FILTERS.languages : current.languages,
      casinoAvailability: hidden.has('priceCasino')
        ? INITIAL_FILTERS.casinoAvailability
        : current.casinoAvailability,
      cryptoAvailability: hidden.has('priceCrypto')
        ? INITIAL_FILTERS.cryptoAvailability
        : current.cryptoAvailability,
      linkInsertAvailability: hidden.has('priceLinkInsert')
        ? INITIAL_FILTERS.linkInsertAvailability
        : current.linkInsertAvailability,
      linkInsertCasinoAvailability: hidden.has('priceLinkInsertCasino')
        ? INITIAL_FILTERS.linkInsertCasinoAvailability
        : current.linkInsertCasinoAvailability,
      datingAvailability: hidden.has('priceDating')
        ? INITIAL_FILTERS.datingAvailability
        : current.datingAvailability,
      quarantine: hidden.has('isQuarantined') ? INITIAL_FILTERS.quarantine : current.quarantine,
      lastPublishedFromMonth: hidden.has('lastPublishedDate')
        ? INITIAL_FILTERS.lastPublishedFromMonth
        : current.lastPublishedFromMonth,
      lastPublishedToMonth: hidden.has('lastPublishedDate')
        ? INITIAL_FILTERS.lastPublishedToMonth
        : current.lastPublishedToMonth,
    }));
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
              <BrandButton size="small" onClick={(e) => setDuplicatesAnchor(e.currentTarget)}>
                View list
              </BrandButton>
            }
          >
            Duplicates removed: {multiSearchResult.duplicates.length}
          </Alert>
        )}

        {multiSearchResult && multiSearchResult.notFound.length > 0 && gridFiltersActive && (
          <Alert
            severity="info"
            sx={{ mb: 1.5 }}
            action={
              <BrandButton size="small" onClick={handleClearFilters}>
                Clear filters
              </BrandButton>
            }
          >
            Not found ({multiSearchResult.notFound.length}) hidden while filters are active
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
            resultCount={gridRowCount}
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
              rows={gridRows}
              columns={columns}
              getRowId={(row) => (isNotFoundRow(row) ? `notfound:${row.domain}` : row.domain)}
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
                  backgroundClip: 'padding-box',
                },
                '& .SitesGrid-domainCell': {
                  zIndex: 30,
                  backgroundColor: 'background.paper',
                },
                '& .SitesGrid-domainHeader': {
                  zIndex: 45,
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
      </Box>
    </PageShell>
  );
}
