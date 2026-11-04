import { useCallback, useEffect, useMemo, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import RefreshIcon from '@mui/icons-material/Refresh';
import { DataGrid, type GridColDef, type GridPaginationModel } from '@mui/x-data-grid';
import { PageShell } from '../components/layout/PageShell';
import { BrandButton } from '../components/common/BrandButton';
import {
  SyncRunSummary,
  SyncStatusChip,
} from '../components/linkdetective/LinkDetectiveSyncDisplay';
import { SYNC_ITEM_STATUSES, syncDate } from '../utils/linkDetectiveSyncDisplay';
import { useLinkDetectiveSyncMonitoring } from '../hooks/useLinkDetectiveSyncMonitoring';
import { useUserRoles } from '../hooks/useUserRoles';
import { linkDetectiveSyncService as service } from '../services/linkDetectiveSync.service';
import type { LinkDetectiveSyncItem } from '../types/linkDetectiveSync.types';
import { dataGridLocaleText } from '../utils/numberFormat';

export function LinkDetectiveSyncRunDetails({ id }: { id: string }) {
  const { canReadLinkDetectiveSync } = useUserRoles();
  const navigate = useNavigate();
  const [pagination, setPagination] = useState<GridPaginationModel>({ page: 0, pageSize: 100 });
  const [search, setSearch] = useState('');
  const [appliedSearch, setAppliedSearch] = useState('');
  const [resultStatus, setResultStatus] = useState('');
  const loadDetails = useCallback(
    () => service.getRun(id, pagination.page + 1, pagination.pageSize, appliedSearch, resultStatus),
    [id, pagination.page, pagination.pageSize, appliedSearch, resultStatus]
  );
  const {
    status,
    data: details,
    error,
    loading,
    refresh,
  } = useLinkDetectiveSyncMonitoring(canReadLinkDetectiveSync, loadDetails);
  const run = details?.run;

  useEffect(() => {
    const normalized = search.trim();
    if (normalized === appliedSearch) return;
    const timer = window.setTimeout(() => {
      setAppliedSearch(normalized);
      setPagination((value) => ({ ...value, page: 0 }));
    }, 300);
    return () => window.clearTimeout(timer);
  }, [search, appliedSearch]);

  const itemColumns = useMemo<GridColDef<LinkDetectiveSyncItem>[]>(
    () =>
      (
        [
          { field: 'domain', headerName: 'Domain', minWidth: 220, flex: 1 },
          {
            field: 'status',
            headerName: 'Result',
            minWidth: 190,
            renderCell: ({ row }) => <SyncStatusChip status={row.status} item />,
          },
          { field: 'attempts', headerName: 'Attempts', width: 90 },
          {
            field: 'checkedAtUtc',
            headerName: 'Checked',
            minWidth: 180,
            valueFormatter: (value) => syncDate(value),
          },
          {
            field: 'error',
            headerName: 'Error',
            minWidth: 250,
            flex: 1.3,
            renderCell: ({ row }) =>
              row.error ? (
                <Tooltip title={row.error}>
                  <Typography variant="body2" noWrap>
                    {row.error}
                  </Typography>
                </Tooltip>
              ) : (
                '—'
              ),
          },
        ] satisfies GridColDef<LinkDetectiveSyncItem>[]
      ).map((column) => ({ ...column, sortable: false })),
    []
  );

  if (!canReadLinkDetectiveSync) return <Navigate to="/sites" replace />;
  return (
    <PageShell
      title="LinkDetective Sync"
      maxWidth="xl"
      actions={
        <BrandButton startIcon={<RefreshIcon />} onClick={refresh} disabled={loading}>
          Refresh status
        </BrandButton>
      }
    >
      <Stack spacing={2.5}>
        {error && <Alert severity="error">{error}</Alert>}
        {!status && loading && (
          <Box sx={{ textAlign: 'center', py: 5 }}>
            <CircularProgress />
          </Box>
        )}
        {status && (!status.enabled || !status.configured) && (
          <Alert severity="info">
            {!status.configured
              ? 'LinkDetective API access is not configured.'
              : 'Automatic background checks are disabled.'}{' '}
            Saved market data remains available.
          </Alert>
        )}

        <Box>
          <Button onClick={() => navigate('/admin/linkdetective-sync')}>
            ← Back to sync overview
          </Button>
        </Box>
        {run && (
          <>
            <SyncRunSummary run={run} />
            <Paper variant="outlined" sx={{ p: 2.5 }}>
              <Typography variant="h6">Domain results</Typography>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mt: 2, mb: 2 }}>
                <TextField
                  label="Domain"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  size="small"
                  sx={{ flex: 1 }}
                  slotProps={{ htmlInput: { maxLength: 253 } }}
                />
                <TextField
                  select
                  label="Result"
                  value={resultStatus}
                  onChange={(event) => {
                    setResultStatus(event.target.value);
                    setPagination((value) => ({ ...value, page: 0 }));
                  }}
                  size="small"
                  sx={{ minWidth: 200 }}
                  slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
                >
                  <MenuItem value="">All results</MenuItem>
                  {SYNC_ITEM_STATUSES.map(({ label, value }) => (
                    <MenuItem key={value} value={String(value)}>
                      {label}
                    </MenuItem>
                  ))}
                </TextField>
              </Stack>
              <Box sx={{ overflowX: 'auto' }}>
                <DataGrid
                  autoHeight
                  rows={details?.results.items ?? []}
                  getRowId={(row) => row.domain}
                  columns={itemColumns}
                  rowCount={details?.results.totalCount ?? 0}
                  paginationMode="server"
                  paginationModel={pagination}
                  onPaginationModelChange={setPagination}
                  pageSizeOptions={[25, 50, 100]}
                  loading={loading}
                  disableRowSelectionOnClick
                  disableColumnFilter
                  localeText={dataGridLocaleText}
                  sx={{ minWidth: 950, border: 0 }}
                />
              </Box>
            </Paper>
          </>
        )}
        {!run && loading && <CircularProgress />}
      </Stack>
    </PageShell>
  );
}
