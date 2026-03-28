import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { Box, Paper, Typography, Tooltip, Alert, Snackbar, Popover, List, ListItem, ListItemText } from '@mui/material';
import { DataGrid } from '@mui/x-data-grid';
import type { GridColDef, GridSortModel, GridPaginationModel } from '@mui/x-data-grid';
import DownloadIcon from '@mui/icons-material/Download';
import { PageShell } from '../components/layout/PageShell';
import { SitesFilters } from '../components/sites/SitesFilters';
import { EditSiteDialog } from '../components/sites/EditSiteDialog';
import { useUserRoles } from '../hooks/useUserRoles';
import type {
  Site,
  SitesFilters as FiltersType,
  SitesQueryParams,
  MultiSearchResponse,
} from '../types/sites.types';
import { sitesService } from '../services/sites.service';
import { BrandButton } from '../components/common/BrandButton';
import { formatOptionalServicePrice, matchesAvailabilityFilter } from '../utils/serviceAvailability';

/** Row type for grid: normal site or not-found placeholder (domain only). */
type NotFoundRow = { domain: string; _isNotFound: true };
type GridRow = Site | NotFoundRow;

function isNotFoundRow(row: GridRow): row is NotFoundRow {
  return '_isNotFound' in row && row._isNotFound === true;
}

function getQuarantineReason(row: GridRow): string | null {
  return isNotFoundRow(row) ? null : row.quarantineReason;
}

function formatCell<T>(row: GridRow, value: T, format: (v: T) => string): string {
  return isNotFoundRow(row) ? '—' : format(value);
}

function formatPrice(row: GridRow, value: number | null): string {
  return formatCell(row, value, (v) => (v == null ? '—' : `$${v}`));
}

function formatOptionalServiceCell(
  row: GridRow,
  price: number | null,
  status: Site['priceCasinoStatus']
): string {
  return formatCell(row, price, (v) => formatOptionalServicePrice(status, v));
}

/** Client-side filter for multi-search found rows (same logic as server filters, excluding search). */
function filterSites(sites: Site[], f: FiltersType): Site[] {
  return sites.filter((s) => {
    if (f.drMin !== '' && s.dr < Number(f.drMin)) return false;
    if (f.drMax !== '' && s.dr > Number(f.drMax)) return false;
    if (f.trafficMin !== '' && s.traffic < Number(f.trafficMin)) return false;
    if (f.trafficMax !== '' && s.traffic > Number(f.trafficMax)) return false;
    if (f.priceMin !== '' && (s.priceUsd ?? 0) < Number(f.priceMin)) return false;
    if (f.priceMax !== '' && (s.priceUsd ?? 0) > Number(f.priceMax)) return false;
    if (f.location.length > 0 && !f.location.includes(s.location)) return false;
    if (!matchesAvailabilityFilter(s.priceCasinoStatus, f.casinoAvailability)) return false;
    if (!matchesAvailabilityFilter(s.priceCryptoStatus, f.cryptoAvailability)) return false;
    if (!matchesAvailabilityFilter(s.priceLinkInsertStatus, f.linkInsertAvailability)) return false;
    if (f.quarantine === 'only' && !s.isQuarantined) return false;
    if (f.quarantine === 'exclude' && s.isQuarantined) return false;
    return true;
  });
}

const INITIAL_FILTERS: FiltersType = {
  search: '',
  drMin: '',
  drMax: '',
  trafficMin: '',
  trafficMax: '',
  priceMin: '',
  priceMax: '',
  location: [],
  casinoAvailability: 'all',
  cryptoAvailability: 'all',
  linkInsertAvailability: 'all',
  quarantine: 'all',
};

export function Sites() {
  const [sites, setSites] = useState<Site[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [filters, setFilters] = useState<FiltersType>(INITIAL_FILTERS);
  const [multiSearchMode, setMultiSearchMode] = useState(false);
  const [multiSearchResult, setMultiSearchResult] = useState<MultiSearchResponse | null>(null);
  const [multiSearchLoading, setMultiSearchLoading] = useState(false);
  const [duplicatesAnchor, setDuplicatesAnchor] = useState<HTMLElement | null>(null);
  const [editSite, setEditSite] = useState<Site | null>(null);
  const [snackbar, setSnackbar] = useState<{ open: boolean; message: string; severity: 'success' | 'error' }>({
    open: false,
    message: '',
    severity: 'success',
  });

  const { isAdmin, isClient } = useUserRoles();

  const [paginationModel, setPaginationModel] = useState<GridPaginationModel>({
    page: 0,
    pageSize: 25,
  });

  const [sortModel, setSortModel] = useState<GridSortModel>([
    { field: 'domain', sort: 'asc' },
  ]);
  const skipLoadAfterUncheckRef = useRef(false);

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
      filters.casinoAvailability !== INITIAL_FILTERS.casinoAvailability ||
      filters.cryptoAvailability !== INITIAL_FILTERS.cryptoAvailability ||
      filters.linkInsertAvailability !== INITIAL_FILTERS.linkInsertAvailability ||
      filters.quarantine !== INITIAL_FILTERS.quarantine,
    [filters]
  );

  const buildSitesQueryParams = useCallback(
    (page: number, pageSize: number): SitesQueryParams => ({
      page,
      pageSize,
      sortBy: sortModel[0]?.field || 'domain',
      sortDir: sortModel[0]?.sort || 'asc',
      search: filters.search || undefined,
      drMin: filters.drMin ? Number(filters.drMin) : undefined,
      drMax: filters.drMax ? Number(filters.drMax) : undefined,
      trafficMin: filters.trafficMin ? Number(filters.trafficMin) : undefined,
      trafficMax: filters.trafficMax ? Number(filters.trafficMax) : undefined,
      priceMin: filters.priceMin ? Number(filters.priceMin) : undefined,
      priceMax: filters.priceMax ? Number(filters.priceMax) : undefined,
      location: filters.location.length > 0 ? filters.location : undefined,
      casinoAvailability: filters.casinoAvailability,
      cryptoAvailability: filters.cryptoAvailability,
      linkInsertAvailability: filters.linkInsertAvailability,
      quarantine: filters.quarantine,
    }),
    [filters, sortModel]
  );

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
    if (skipLoadAfterUncheckRef.current) {
      skipLoadAfterUncheckRef.current = false;
      return;
    }
    loadSites();
  }, [loadSites, multiSearchMode]);

  const handleFiltersApply = () => {
    setPaginationModel((prev) => ({ ...prev, page: 0 }));
    if (multiSearchMode) {
      const query = filters.search.trim();
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
      return;
    }
  };

  const handleMultiSearchModeChange = (enabled: boolean) => {
    setMultiSearchMode(enabled);
    if (!enabled) {
      setMultiSearchResult(null);
      if (filters.search.trim() === '') skipLoadAfterUncheckRef.current = true;
    }
  };

  const handleClearFilters = () => {
    setFilters(INITIAL_FILTERS);
    setPaginationModel((prev) => ({ ...prev, page: 0 }));
  };

  const handleExport = async () => {
    setExporting(true);
    try {
      const params = buildSitesQueryParams(1, 1000000);

      if (multiSearchResult !== null) {
        await sitesService.exportSitesMultiSearch({
          queryText: filters.search.trim(),
          filters: params,
          sortBy: params.sortBy,
          sortDir: params.sortDir,
        });
      } else {
        await sitesService.exportSites(params);
      }
      setSnackbar({ open: true, message: 'Export completed successfully', severity: 'success' });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Export failed';
      setSnackbar({ open: true, message, severity: 'error' });
    } finally {
      setExporting(false);
    }
  };

  const handleCloseSnackbar = () => {
    setSnackbar((s) => ({ ...s, open: false }));
  };

  const handleOpenEdit = (site: Site) => {
    setEditSite(site);
  };

  const handleCloseEdit = () => {
    setEditSite(null);
  };

  const handleEditSaved = (updated: Site) => {
    setEditSite(null);
    setSnackbar({ open: true, message: 'Site updated', severity: 'success' });
    if (multiSearchResult) {
      const newFound = multiSearchResult.found.map((s) => (s.domain === updated.domain ? updated : s));
      setMultiSearchResult({ ...multiSearchResult, found: newFound });
    } else {
      loadSites();
    }
  };

  const isMultiSearchView = multiSearchResult !== null;
  const gridRows: GridRow[] = useMemo(() => {
    if (!multiSearchResult) {
      return sites;
    }
    const filtered = filterSites(multiSearchResult.found, filters);
    const field = sortModel[0]?.field ?? 'domain';
    const dir = sortModel[0]?.sort ?? 'asc';
    const sorted = [...filtered].sort((a, b) => {
      const av = a[field as keyof Site];
      const bv = b[field as keyof Site];
      if (av == null && bv == null) return 0;
      if (av == null) return dir === 'asc' ? 1 : -1;
      if (bv == null) return dir === 'asc' ? -1 : 1;
      const cmp = typeof av === 'string' ? (av as string).localeCompare(bv as string) : (av as number) - (bv as number);
      return dir === 'asc' ? cmp : -cmp;
    });
    const notFoundRows: NotFoundRow[] = gridFiltersActive
      ? []
      : multiSearchResult.notFound.map((d) => ({ domain: d, _isNotFound: true as const }));
    return [...sorted, ...notFoundRows];
  }, [multiSearchResult, filters, sortModel, sites, gridFiltersActive]);

  const gridRowCount = isMultiSearchView ? gridRows.length : total;
  const gridLoading = loading || multiSearchLoading;

  const columns: GridColDef<GridRow>[] = [
    {
      field: 'domain',
      headerName: 'Domain',
      flex: 1,
      minWidth: 200,
    },
    {
      field: 'dr',
      headerName: 'DR',
      width: 80,
      type: 'number',
      valueFormatter: (value, row) =>
        formatCell(row, value as number | null, (v) => (v == null ? '' : String(v))),
    },
    {
      field: 'traffic',
      headerName: 'Traffic',
      width: 120,
      type: 'number',
      valueFormatter: (value, row) => {
        if (isNotFoundRow(row)) return '—';
        if (value == null) return '';
        return new Intl.NumberFormat('en-US').format(value as number);
      },
    },
    {
      field: 'location',
      headerName: 'Location',
      width: 120,
      valueFormatter: (value, row) => formatCell(row, value as string, (v) => v ?? '—'),
    },
    {
      field: 'priceUsd',
      headerName: 'Price USD',
      width: 100,
      type: 'number',
      valueFormatter: (value, row) => formatPrice(row, value as number | null),
    },
    {
      field: 'priceCasino',
      headerName: 'Casino',
      width: 100,
      type: 'number',
      valueFormatter: (value, row) =>
        formatOptionalServiceCell(row, value as number | null, (row as Site).priceCasinoStatus),
    },
    {
      field: 'priceCrypto',
      headerName: 'Crypto',
      width: 100,
      type: 'number',
      valueFormatter: (value, row) =>
        formatOptionalServiceCell(row, value as number | null, (row as Site).priceCryptoStatus),
    },
    {
      field: 'priceLinkInsert',
      headerName: 'Link Insert',
      width: 100,
      type: 'number',
      valueFormatter: (value, row) =>
        formatOptionalServiceCell(row, value as number | null, (row as Site).priceLinkInsertStatus),
    },
    {
      field: 'niche',
      headerName: 'Niche',
      width: 150,
      sortable: false,
      valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
    },
    {
      field: 'categories',
      headerName: 'Categories',
      width: 150,
      sortable: false,
      valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
    },
    {
      field: 'linkType',
      headerName: 'Link Type',
      width: 140,
      sortable: false,
      valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
    },
    {
      field: 'sponsoredTag',
      headerName: 'Sponsored Tag',
      width: 150,
      sortable: false,
      valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
    },
    {
      field: 'isQuarantined',
      headerName: 'Status',
      width: 100,
      sortable: false,
      align: 'center',
      headerAlign: 'center',
      renderCell: (params) => {
        if (isNotFoundRow(params.row)) return '—';
        const isQuarantined = params.value as boolean;
        if (isQuarantined) {
          const reason = getQuarantineReason(params.row);
          const tooltipText = reason ? `Unavailable: ${reason}` : 'Unavailable';
          return (
            <Tooltip title={tooltipText} arrow>
              <span>Unavailable</span>
            </Tooltip>
          );
        }
        return (
          <Tooltip title="Available" arrow>
            <span>Available</span>
          </Tooltip>
        );
      },
    },
    {
      field: 'lastPublishedDate',
      headerName: 'Last Published',
      width: 150,
      valueFormatter: (_value, row) => {
        if (isNotFoundRow(row)) return '—';
        const site = row as Site;
        if (site.lastPublishedDate == null) {
          return 'Before January 2026';
        }
        const d = new Date(site.lastPublishedDate);
        if (site.lastPublishedDateIsMonthOnly) {
          return d.toLocaleDateString('en-GB', { month: 'long', year: 'numeric' });
        }
        const day = String(d.getUTCDate()).padStart(2, '0');
        const month = String(d.getUTCMonth() + 1).padStart(2, '0');
        const year = d.getUTCFullYear();
        return `${day}.${month}.${year}`;
      },
    },
    ...(!isClient
      ? [
          {
            field: 'quarantineReason',
            headerName: 'Quarantine reason',
            width: 160,
            sortable: false,
            valueFormatter: (_value, row) => {
              if (isNotFoundRow(row)) return '—';
              const site = row as Site;
              return site.isQuarantined ? (site.quarantineReason || '—') : '—';
            },
          } as GridColDef<GridRow>,
        ]
      : []),
    ...(isAdmin
      ? [
          {
            field: 'actions',
            headerName: 'Actions',
            width: 90,
            sortable: false,
            renderCell: (params: { row: GridRow }) => {
              if (isNotFoundRow(params.row)) return null;
              return (
                <BrandButton
                  kind="outline"
                  size="small"
                  onClick={() => handleOpenEdit(params.row as Site)}
                >
                  Edit
                </BrandButton>
              );
            },
          } as GridColDef<GridRow>,
        ]
      : []),
  ];

  return (
    <PageShell maxWidth="xl">
      <Box>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
          <Typography variant="h4">
            Sites Catalog
          </Typography>
          <BrandButton
            kind="outline"
            startIcon={<DownloadIcon />}
            onClick={handleExport}
            disabled={exporting || loading}
          >
            {exporting ? 'Exporting...' : 'Export CSV'}
          </BrandButton>
        </Box>

        <SitesFilters
          filters={filters}
          onFiltersChange={setFilters}
          onApply={handleFiltersApply}
          multiSearchMode={multiSearchMode}
          onMultiSearchModeChange={handleMultiSearchModeChange}
          canFilterQuarantine={!isClient}
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

        {multiSearchResult &&
          multiSearchResult.notFound.length > 0 &&
          gridFiltersActive && (
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
            disableRowSelectionOnClick
            disableColumnMenu
            autoHeight
            sx={{
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

        <Snackbar
          open={snackbar.open}
          autoHideDuration={6000}
          onClose={handleCloseSnackbar}
          anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
        >
          <Alert onClose={handleCloseSnackbar} severity={snackbar.severity} sx={{ width: '100%' }}>
            {snackbar.message}
          </Alert>
        </Snackbar>

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
