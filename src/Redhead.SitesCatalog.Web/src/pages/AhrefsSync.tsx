import { useCallback, useEffect, useMemo, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import RefreshIcon from '@mui/icons-material/Refresh';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Paper,
  Typography,
} from '@mui/material';
import {
  DataGrid,
  type GridColDef,
  type GridPaginationModel,
} from '@mui/x-data-grid';
import { PageShell } from '../components/layout/PageShell';
import { BrandButton } from '../components/common/BrandButton';
import {
  RunStatusChip,
} from '../components/ahrefs/AhrefsSyncDisplay';
import { useUserRoles } from '../hooks/useUserRoles';
import { ahrefsSyncService } from '../services/ahrefsSync.service';
import type {
  AhrefsSyncRun,
  AhrefsSyncRunsPage,
  AhrefsSyncStatus,
} from '../types/ahrefsSync.types';
import { dataGridLocaleText, formatInteger } from '../utils/numberFormat';
import {
  formatDateTime,
  formatRunKind,
  formatUtcDateTime,
} from '../utils/ahrefsSyncDisplay';

const EMPTY_RUNS: AhrefsSyncRunsPage = {
  items: [],
  page: 1,
  pageSize: 25,
  totalCount: 0,
  totalPages: 0,
};

export const AhrefsSync: React.FC = () => {
  const { isSuperAdmin } = useUserRoles();
  const navigate = useNavigate();
  const [status, setStatus] = useState<AhrefsSyncStatus | null>(null);
  const [runs, setRuns] = useState<AhrefsSyncRunsPage>(EMPTY_RUNS);
  const [statusLoading, setStatusLoading] = useState(true);
  const [runsLoading, setRunsLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [statusError, setStatusError] = useState<string | null>(null);
  const [runsError, setRunsError] = useState<string | null>(null);
  const [paginationModel, setPaginationModel] = useState<GridPaginationModel>({
    page: 0,
    pageSize: 25,
  });

  const loadData = useCallback(
    async (refreshLimits = false) => {
      if (!isSuperAdmin) return;
      const [statusResult, runsResult] = await Promise.allSettled([
        ahrefsSyncService.getStatus(refreshLimits),
        ahrefsSyncService.listRuns(
          paginationModel.page + 1,
          paginationModel.pageSize
        ),
      ]);

      if (statusResult.status === 'fulfilled') {
        setStatus(statusResult.value);
      } else {
        setStatusError(
          statusResult.reason instanceof Error
            ? statusResult.reason.message
            : 'Failed to load Ahrefs limits and scheduler status.'
        );
      }

      if (runsResult.status === 'fulfilled') {
        setRuns(runsResult.value);
      } else {
        setRunsError(
          runsResult.reason instanceof Error
            ? runsResult.reason.message
            : 'Failed to load Ahrefs sync runs.'
        );
      }

      setStatusLoading(false);
      setRunsLoading(false);
      setRefreshing(false);
    },
    [isSuperAdmin, paginationModel.page, paginationModel.pageSize]
  );

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadData();
    }, 0);
    return () => window.clearTimeout(timer);
  }, [loadData]);

  useEffect(() => {
    if (!status?.activeRun) return;
    const timer = window.setInterval(() => {
      void loadData();
    }, 15_000);
    return () => window.clearInterval(timer);
  }, [loadData, status?.activeRun]);

  const columns = useMemo<GridColDef<AhrefsSyncRun>[]>(
    () => [
      {
        field: 'startedAt',
        headerName: 'Started',
        minWidth: 175,
        flex: 0.9,
        valueFormatter: (value) => formatDateTime(value as string),
      },
      {
        field: 'status',
        headerName: 'Status',
        minWidth: 205,
        flex: 1,
        sortable: false,
        renderCell: (params) => <RunStatusChip status={params.row.status} />,
      },
      {
        field: 'runKind',
        headerName: 'Kind',
        minWidth: 120,
        flex: 0.65,
        valueFormatter: (value) => formatRunKind(value as number),
      },
      {
        field: 'snapshotMonth',
        headerName: 'Snapshot month',
        minWidth: 125,
        flex: 0.7,
      },
      {
        field: 'progress',
        headerName: 'Processed / selected',
        minWidth: 165,
        flex: 0.85,
        sortable: false,
        valueGetter: (_value, row) =>
          `${formatInteger(row.processedSitesCount)} / ${formatInteger(row.selectedSitesCount)}`,
      },
      {
        field: 'updatedSitesCount',
        headerName: 'Updated',
        minWidth: 95,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'failedSitesCount',
        headerName: 'Failed',
        minWidth: 85,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'actualUnits',
        headerName: 'Units',
        minWidth: 100,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'coverage',
        headerName: 'Coverage',
        minWidth: 125,
        sortable: false,
        renderCell: (params) =>
          params.row.isFullCoverage ? (
            <Chip size="small" label="Full" color="success" variant="outlined" />
          ) : params.row.wasLimitedByBudget ? (
            <Chip size="small" label="Budget limited" color="warning" variant="outlined" />
          ) : (
            <Chip size="small" label="Partial" variant="outlined" />
          ),
      },
    ],
    []
  );

  const handleRefresh = useCallback(() => {
    setRefreshing(true);
    setStatusError(null);
    setRunsError(null);
    void loadData(true);
  }, [loadData]);

  const handlePaginationChange = useCallback((model: GridPaginationModel) => {
    setRunsLoading(true);
    setRunsError(null);
    setPaginationModel(model);
  }, []);

  if (!isSuperAdmin) {
    return <Navigate to="/sites" replace />;
  }

  return (
    <PageShell
      title="Ahrefs Sync"
      maxWidth="xl"
      actions={
        <BrandButton
          startIcon={<RefreshIcon />}
          onClick={handleRefresh}
          disabled={refreshing}
        >
          Refresh
        </BrandButton>
      }
    >
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
        {statusError && (
          <Alert severity="error" onClose={() => setStatusError(null)}>
            {statusError}
          </Alert>
        )}
        {statusLoading && !status ? (
          <Paper variant="outlined" sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
            <CircularProgress size={32} />
          </Paper>
        ) : status ? (
          <StatusOverview status={status} />
        ) : null}

        {runsError && (
          <Alert severity="error" onClose={() => setRunsError(null)}>
            {runsError}
          </Alert>
        )}
        <Paper variant="outlined" sx={{ p: 2 }}>
          <Typography variant="h6" sx={{ fontWeight: 700, mb: 0.5 }}>
            Recent runs
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Monthly and manual Ahrefs Traffic/DR synchronization history. Select a row
            to inspect site-level results.
          </Typography>
          <DataGrid
            autoHeight
            rows={runs.items}
            columns={columns}
            rowCount={runs.totalCount}
            paginationMode="server"
            paginationModel={paginationModel}
            onPaginationModelChange={handlePaginationChange}
            pageSizeOptions={[10, 25, 50, 100]}
            disableRowSelectionOnClick
            loading={runsLoading || refreshing}
            onRowClick={(params) => navigate(`/admin/ahrefs-sync/runs/${params.row.id}`)}
            localeText={dataGridLocaleText}
            sx={{
              border: 0,
              '& .MuiDataGrid-row': { cursor: 'pointer' },
              '& .MuiDataGrid-cell:focus, & .MuiDataGrid-cell:focus-within': {
                outline: 'none',
              },
            }}
          />
        </Paper>
      </Box>
    </PageShell>
  );
};

function StatusOverview({ status }: { status: AhrefsSyncStatus }) {
  const scheduleValue = status.isDueNow
    ? 'Due now'
    : status.schedulerEnabled
      ? formatUtcDateTime(status.nextScheduledRunUtc)
      : 'Disabled';

  return (
    <>
      {status.activeRun && (
        <Alert severity="info">
          A sync is currently running: {formatInteger(status.activeRun.processedSitesCount)} of{' '}
          {formatInteger(status.activeRun.selectedSitesCount)} selected sites processed.
        </Alert>
      )}

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: '1fr',
            sm: 'repeat(2, minmax(0, 1fr))',
            xl: 'repeat(4, minmax(0, 1fr))',
          },
          gap: 2,
        }}
      >
        <MetricCard
          label="Effective available units"
          value={formatInteger(status.effectiveAvailableUnits)}
          helper={`Safety buffer: ${formatInteger(status.safetyBufferUnits)}`}
        />
        <MetricCard
          label="Estimated full sync"
          value={formatInteger(status.fullEstimatedUnits)}
          helper={`${formatInteger(status.eligibleSitesCount)} eligible sites`}
        />
        <MetricCard
          label="Next scheduled run"
          value={scheduleValue}
          helper={`Cron: ${status.cron} UTC`}
          warning={status.isDueNow}
        />
        <MetricCard
          label="Usage reset"
          value={formatUtcDateTime(status.usageResetDate)}
          helper={`Limits checked: ${formatDateTime(status.limitsCheckedAt)}`}
        />
      </Box>

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', lg: '1.15fr 0.85fr' },
          gap: 2,
        }}
      >
        <Paper variant="outlined" sx={{ p: 2 }}>
          <Typography variant="h6" sx={{ fontWeight: 700, mb: 2 }}>
            Budget availability
          </Typography>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, 1fr)' },
              gap: 2,
            }}
          >
            <BudgetValue label="API key remaining" value={status.apiKeyRemainingUnits} />
            <BudgetValue label="Workspace remaining" value={status.workspaceRemainingUnits} />
            <BudgetValue label="App budget remaining" value={status.appBudgetRemainingUnits} />
          </Box>
        </Paper>

        <Paper variant="outlined" sx={{ p: 2 }}>
          <Typography variant="h6" sx={{ fontWeight: 700, mb: 2 }}>
            Job configuration
          </Typography>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: 'max-content minmax(0, 1fr)',
              columnGap: 2,
              rowGap: 1,
            }}
          >
            <ConfigRow label="Scheduler" value={status.schedulerEnabled ? 'Enabled' : 'Disabled'} />
            <ConfigRow label="Target mode" value={status.targetMode} />
            <ConfigRow label="Protocol" value={status.protocol} />
            <ConfigRow label="Volume mode" value={status.volumeMode} />
            <ConfigRow label="Batch size" value={formatInteger(status.batchSize)} />
            <ConfigRow label="Max sites/run" value={formatInteger(status.maxSitesPerRun)} />
          </Box>
        </Paper>
      </Box>
    </>
  );
}

function MetricCard({
  label,
  value,
  helper,
  warning = false,
}: {
  label: string;
  value: string;
  helper: string;
  warning?: boolean;
}) {
  return (
    <Paper
      variant="outlined"
      sx={{ p: 2, borderColor: warning ? 'warning.main' : undefined }}
    >
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="h5" sx={{ fontWeight: 700, my: 0.75 }}>
        {value}
      </Typography>
      <Typography variant="body2" color="text.secondary">
        {helper}
      </Typography>
    </Paper>
  );
}

function BudgetValue({ label, value }: { label: string; value: number }) {
  return (
    <Box>
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="h5" sx={{ fontWeight: 700, mt: 0.5 }}>
        {formatInteger(value)}
      </Typography>
    </Box>
  );
}

function ConfigRow({ label, value }: { label: string; value: string }) {
  return (
    <>
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="body2" sx={{ fontWeight: 600, overflowWrap: 'anywhere' }}>
        {value}
      </Typography>
    </>
  );
}
