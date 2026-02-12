import { useState, useEffect, useCallback } from 'react';
import { Box, Paper, Typography, Tooltip } from '@mui/material';
import { DataGrid } from '@mui/x-data-grid';
import type { GridColDef, GridSortModel, GridPaginationModel } from '@mui/x-data-grid';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import { PageShell } from '../components/layout/PageShell';
import { SitesFilters } from '../components/sites/SitesFilters';
import type { Site, SitesFilters as FiltersType, SitesQueryParams } from '../types/sites.types';
import { sitesService } from '../services/sites.service';

const INITIAL_FILTERS: FiltersType = {
  search: '',
  drMin: '',
  drMax: '',
  trafficMin: '',
  trafficMax: '',
  priceMin: '',
  priceMax: '',
  location: [],
  casinoAllowed: false,
  cryptoAllowed: false,
  linkInsertAllowed: false,
  quarantine: 'all',
};

export function Sites() {
  const [sites, setSites] = useState<Site[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [filters, setFilters] = useState<FiltersType>(INITIAL_FILTERS);
  
  const [paginationModel, setPaginationModel] = useState<GridPaginationModel>({
    page: 0,
    pageSize: 25,
  });
  
  const [sortModel, setSortModel] = useState<GridSortModel>([
    { field: 'domain', sort: 'asc' },
  ]);

  const loadSites = useCallback(async () => {
    setLoading(true);
    try {
      const params: SitesQueryParams = {
        page: paginationModel.page + 1, // API uses 1-based pagination
        pageSize: paginationModel.pageSize,
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
        casinoAllowed: filters.casinoAllowed || undefined,
        cryptoAllowed: filters.cryptoAllowed || undefined,
        linkInsertAllowed: filters.linkInsertAllowed || undefined,
        quarantine: filters.quarantine,
      };

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
  }, [paginationModel, sortModel, filters]);

  useEffect(() => {
    loadSites();
  }, [loadSites]);

  const handleFiltersApply = () => {
    // Reset to first page when filters change
    setPaginationModel((prev) => ({ ...prev, page: 0 }));
    loadSites();
  };

  const columns: GridColDef<Site>[] = [
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
    },
    {
      field: 'traffic',
      headerName: 'Traffic',
      width: 120,
      type: 'number',
      valueFormatter: (value) => {
        if (value == null) return '';
        return new Intl.NumberFormat('en-US').format(value);
      },
    },
    {
      field: 'location',
      headerName: 'Location',
      width: 120,
    },
    {
      field: 'priceUsd',
      headerName: 'Price USD',
      width: 120,
      type: 'number',
      valueFormatter: (value) => {
        if (value == null) return '—';
        return `$${value}`;
      },
    },
    {
      field: 'priceCasino',
      headerName: 'Casino',
      width: 100,
      type: 'number',
      valueFormatter: (value) => {
        if (value == null) return '—';
        return `$${value}`;
      },
    },
    {
      field: 'priceCrypto',
      headerName: 'Crypto',
      width: 100,
      type: 'number',
      valueFormatter: (value) => {
        if (value == null) return '—';
        return `$${value}`;
      },
    },
    {
      field: 'priceLinkInsert',
      headerName: 'Link Insert',
      width: 120,
      type: 'number',
      valueFormatter: (value) => {
        if (value == null) return '—';
        return `$${value}`;
      },
    },
    {
      field: 'niche',
      headerName: 'Niche',
      width: 150,
      valueFormatter: (value) => value || '—',
    },
    {
      field: 'categories',
      headerName: 'Categories',
      width: 150,
      valueFormatter: (value) => value || '—',
    },
    {
      field: 'isQuarantined',
      headerName: 'Status',
      width: 80,
      sortable: false,
      align: 'center',
      headerAlign: 'center',
      renderCell: (params) => {
        const isQuarantined = params.value as boolean;
        
        if (isQuarantined) {
          const reason = params.row.quarantineReason;
          const tooltipText = reason ? `Unavailable: ${reason}` : 'Unavailable';
          
          return (
            <Tooltip title={tooltipText} arrow>
              <WarningAmberIcon 
                sx={{ 
                  color: 'error.main',
                  fontSize: 20
                }} 
              />
            </Tooltip>
          );
        }
        
        return (
          <Tooltip title="Available" arrow>
            <CheckCircleIcon 
              sx={{ 
                color: 'success.main',
                fontSize: 20
              }} 
            />
          </Tooltip>
        );
      },
    },
  ];

  return (
    <PageShell maxWidth="xl">
      <Box>
        <Typography variant="h4" gutterBottom>
          Sites Catalog
        </Typography>
        
        <SitesFilters
          filters={filters}
          onFiltersChange={setFilters}
          onApply={handleFiltersApply}
        />

        <Paper>
          <DataGrid
            rows={sites}
            columns={columns}
            getRowId={(row) => row.domain}
            rowCount={total}
            loading={loading}
            pageSizeOptions={[10, 25, 50, 100]}
            paginationModel={paginationModel}
            paginationMode="server"
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
      </Box>
    </PageShell>
  );
}
