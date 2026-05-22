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
import type { GridSortModel, GridPaginationModel } from '@mui/x-data-grid';
import { PageShell } from '../components/layout/PageShell';
import { SitesFilters } from '../components/sites/SitesFilters';
import { EditSiteDialog } from '../components/sites/EditSiteDialog';
import { SitesExportMenu } from '../components/sites/SitesExportMenu';
import { GoogleDriveConnectionDialog } from '../components/sites/GoogleDriveConnectionDialog';
import { SitesTableViewToolbar } from '../components/sites/SitesTableViewToolbar';
import { SitesSnackbar } from '../components/sites/SitesSnackbar';
import type { SitesSnackbarState } from '../components/sites/SitesSnackbar';
import { insertSitesColumnsByDefaultOrder } from '../components/sites/sitesTableColumns';
import { useSitesColumns } from '../components/sites/useSitesColumns';
import { isNotFoundRow, useSitesGridRows } from '../components/sites/useSitesGridRows';
import { useSitesTableViews } from '../components/sites/useSitesTableViews';
import { useUserRoles } from '../hooks/useUserRoles';
import { useAuth } from '../contexts/AuthContext';
import { useSitesExport } from '../hooks/useSitesExport';
import type {
  Site,
  SitesFilters as FiltersType,
  SitesQueryParams,
  MultiSearchResponse,
} from '../types/sites.types';
import { sitesService } from '../services/sites.service';
import { BrandButton } from '../components/common/BrandButton';
import { loadStoredStopListDomains, persistStopListDomains } from '../utils/stopList';

const INITIAL_FILTERS: FiltersType = {
  search: '',
  drMin: '',
  drMax: '',
  trafficMin: '',
  trafficMax: '',
  priceMin: '',
  priceMax: '',
  stopListDomains: [],
  location: [],
  niches: [],
  categorySearchTerms: [],
  languages: [],
  casinoAvailability: 'all',
  cryptoAvailability: 'all',
  linkInsertAvailability: 'all',
  linkInsertCasinoAvailability: 'all',
  datingAvailability: 'all',
  quarantine: 'all',
  lastPublishedFromMonth: null,
  lastPublishedToMonth: null,
};

function createInitialFilters(): FiltersType {
  return {
    ...INITIAL_FILTERS,
    stopListDomains: loadStoredStopListDomains(),
  };
}

export function Sites() {
  const [sites, setSites] = useState<Site[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [filters, setFilters] = useState<FiltersType>(() => createInitialFilters());
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

  useEffect(() => {
    persistStopListDomains(filters.stopListDomains);
  }, [filters.stopListDomains]);

  useEffect(() => {
    if (!tableViews.loadError) return;
    setSnackbar({
      open: true,
      message: 'Table views could not be loaded',
      detail: tableViews.loadError,
      severity: 'error',
    });
  }, [tableViews.loadError]);

  const [paginationModel, setPaginationModel] = useState<GridPaginationModel>({
    page: 0,
    pageSize: 25,
  });

  const [sortModel, setSortModel] = useState<GridSortModel>([{ field: 'domain', sort: 'asc' }]);

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
      filters.location.length !== 0 ||
      filters.niches.length !== 0 ||
      filters.categorySearchTerms.length !== 0 ||
      filters.languages.length !== 0 ||
      filters.casinoAvailability !== INITIAL_FILTERS.casinoAvailability ||
      filters.cryptoAvailability !== INITIAL_FILTERS.cryptoAvailability ||
      filters.linkInsertAvailability !== INITIAL_FILTERS.linkInsertAvailability ||
      filters.linkInsertCasinoAvailability !== INITIAL_FILTERS.linkInsertCasinoAvailability ||
      filters.datingAvailability !== INITIAL_FILTERS.datingAvailability ||
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
      location: filters.location.length > 0 ? filters.location : undefined,
      niches: filters.niches.length > 0 ? filters.niches : undefined,
      categorySearchTerms:
        filters.categorySearchTerms.length > 0 ? filters.categorySearchTerms : undefined,
      languages: filters.languages.length > 0 ? filters.languages : undefined,
      casinoAvailability: filters.casinoAvailability,
      cryptoAvailability: filters.cryptoAvailability,
      linkInsertAvailability: filters.linkInsertAvailability,
      linkInsertCasinoAvailability: filters.linkInsertCasinoAvailability,
      datingAvailability: filters.datingAvailability,
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
      filters.location,
      filters.niches,
      filters.categorySearchTerms,
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
    onEdit: handleOpenEdit,
  });

  const hiddenFilteredColumnIds = useMemo(() => {
    const activeColumnIds = new Set<string>();

    if (filters.drMin || filters.drMax) activeColumnIds.add('dr');
    if (filters.trafficMin || filters.trafficMax) activeColumnIds.add('traffic');
    if (filters.priceMin || filters.priceMax) activeColumnIds.add('priceUsd');
    if (filters.location.length > 0) activeColumnIds.add('location');
    if (filters.niches.length > 0) activeColumnIds.add('niche');
    if (filters.categorySearchTerms.length > 0) activeColumnIds.add('categories');
    if (filters.languages.length > 0) activeColumnIds.add('language');
    if (filters.casinoAvailability !== INITIAL_FILTERS.casinoAvailability)
      activeColumnIds.add('priceCasino');
    if (filters.cryptoAvailability !== INITIAL_FILTERS.cryptoAvailability)
      activeColumnIds.add('priceCrypto');
    if (filters.linkInsertAvailability !== INITIAL_FILTERS.linkInsertAvailability) {
      activeColumnIds.add('priceLinkInsert');
    }
    if (filters.linkInsertCasinoAvailability !== INITIAL_FILTERS.linkInsertCasinoAvailability) {
      activeColumnIds.add('priceLinkInsertCasino');
    }
    if (filters.datingAvailability !== INITIAL_FILTERS.datingAvailability)
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
    filters.location,
    filters.niches,
    filters.categorySearchTerms,
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
      location: hidden.has('location') ? INITIAL_FILTERS.location : current.location,
      niches: hidden.has('niche') ? INITIAL_FILTERS.niches : current.niches,
      categorySearchTerms: hidden.has('categories')
        ? INITIAL_FILTERS.categorySearchTerms
        : current.categorySearchTerms,
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
      <Box>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
          <Typography variant="h4">Sites Catalog</Typography>
          {canExport && (
            <SitesExportMenu
              exporting={exporting}
              loading={loading}
              onDownloadExcel={handleDownloadExport}
              onSaveToGoogleDrive={handleSaveToGoogleDrive}
            />
          )}
        </Box>

        <SitesFilters
          filters={filters}
          onFiltersChange={setFilters}
          onApply={handleFiltersApply}
          multiSearchMode={multiSearchMode}
          onMultiSearchModeChange={handleMultiSearchModeChange}
          filterOptionsRefreshKey={filterOptionsRefreshKey}
        />

        {multiSearchResult && multiSearchResult.duplicates.length > 0 && (
          <Alert
            severity="warning"
            sx={{ mb: 2 }}
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
            sx={{ mb: 2 }}
            action={
              <BrandButton size="small" onClick={handleClearFilters}>
                Clear filters
              </BrandButton>
            }
          >
            Not found ({multiSearchResult.notFound.length}) hidden while filters are active
          </Alert>
        )}

        <Paper>
          <SitesTableViewToolbar
            tableViews={tableViews}
            hiddenFilteredColumns={hiddenFilteredColumns}
            onShowFilteredColumns={handleShowFilteredColumns}
            onClearHiddenFilters={handleClearHiddenFilters}
            onSuccess={(message) => setSnackbar({ open: true, message, severity: 'success' })}
            onError={(message) => setSnackbar({ open: true, message, severity: 'error' })}
          />

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
            density={tableViews.density}
            columnVisibilityModel={tableViews.columnVisibilityModel}
            disableRowSelectionOnClick
            disableColumnMenu
            autoHeight
            sx={{
              borderTopLeftRadius: 0,
              borderTopRightRadius: 0,
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
              '& .MuiDataGrid-columnHeader': {
                backgroundColor: 'action.hover',
              },
              '& .MuiDataGrid-columnHeader:focus': {
                outline: 'none',
              },
              '& .MuiDataGrid-columnHeader:focus-within': {
                outline: 'none',
              },
            }}
          />
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
