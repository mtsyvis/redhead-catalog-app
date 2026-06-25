import { useCallback, useEffect, useMemo, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import RefreshIcon from '@mui/icons-material/Refresh';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  LinearProgress,
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
  RunOutcomeChip,
  RunScopeChip,
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
  formatSnapshotMonth,
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
  const { canManageAhrefsSync } = useUserRoles();
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
      if (!canManageAhrefsSync) return;
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
    [canManageAhrefsSync, paginationModel.page, paginationModel.pageSize]
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
        field: 'outcome',
        headerName: 'Outcome',
        minWidth: 190,
        flex: 0.9,
        sortable: false,
        renderCell: (params) => <RunOutcomeChip run={params.row} />,
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
        minWidth: 135,
        flex: 0.7,
        valueFormatter: (value) => formatSnapshotMonth(value as string),
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
        field: 'results',
        headerName: 'Updated / failed',
        minWidth: 135,
        align: 'right',
        headerAlign: 'right',
        sortable: false,
        valueGetter: (_value, row) =>
          `${formatInteger(row.updatedSitesCount)} / ${formatInteger(row.failedSitesCount)}`,
      },
      {
        field: 'units',
        headerName: 'Units',
        minWidth: 135,
        align: 'right',
        headerAlign: 'right',
        sortable: false,
        valueGetter: (_value, row) =>
          `${formatInteger(row.actualUnits)} / ${formatInteger(row.selectedEstimatedUnits)}`,
      },
      {
        field: 'scope',
        headerName: 'Scope',
        minWidth: 135,
        flex: 0.7,
        sortable: false,
        renderCell: (params) => <RunScopeChip run={params.row} />,
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

  if (!canManageAhrefsSync) {
    return <Navigate to="/sites" replace />;
  }

  return (
    <PageShell
      title="Ahrefs Sync"
      maxWidth="xl"
      actions={
        <Box sx={{ width: { xs: '100%', sm: 'auto' } }}>
          <BrandButton
            startIcon={<RefreshIcon />}
            onClick={handleRefresh}
            disabled={refreshing}
          >
            Refresh
          </BrandButton>
        </Box>
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
          <StatusOverview
            status={status}
            onOpenRun={(runId) => navigate(`/admin/ahrefs-sync/runs/${runId}`)}
          />
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
          <Box sx={{ overflowX: 'auto' }}>
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
                minWidth: 1080,
                '& .MuiDataGrid-row': { cursor: 'pointer' },
                '& .MuiDataGrid-cell:focus, & .MuiDataGrid-cell:focus-within': {
                  outline: 'none',
                },
              }}
            />
          </Box>
        </Paper>
      </Box>
    </PageShell>
  );
};

function StatusOverview({
  status,
  onOpenRun,
}: {
  status: AhrefsSyncStatus;
  onOpenRun: (runId: string) => void;
}) {
  const scheduleLabel = status.schedulerEnabled
    ? 'Next scheduled run'
    : 'Next configured occurrence';
  const scheduleValue = status.isWaitingForUsageReset
    ? 'Waiting for Ahrefs reset'
    : status.hasCompletedMonthlyRun
      ? 'Completed for this month'
      : status.isDueNow
        ? 'Due now'
        : formatUtcDateTime(status.nextScheduledRunUtc);
  const capacityReasons = [
    status.configuredRunLimitedByBudget ? 'available budget' : null,
    status.configuredRunLimitedByMaxSites ? 'Max sites/run' : null,
  ].filter(Boolean);
  const limitingBudgets = new Set<string>();
  if (status.apiKeyRemainingUnits === status.effectiveAvailableUnits) {
    limitingBudgets.add('api-key');
  }
  if (status.workspaceRemainingUnits === status.effectiveAvailableUnits) {
    limitingBudgets.add('workspace');
  }
  if (status.appBudgetRemainingUnits === status.effectiveAvailableUnits) {
    limitingBudgets.add('app');
  }

  return (
    <>
      {status.activeRun && (
        <Alert
          severity="info"
          action={
            <Button color="inherit" onClick={() => onOpenRun(status.activeRun!.id)}>
              View run
            </Button>
          }
        >
          <Typography variant="body2" sx={{ fontWeight: 700 }}>
            Ahrefs sync is running
          </Typography>
          <Typography variant="body2">
            {formatInteger(status.activeRun.processedSitesCount)} of{' '}
            {formatInteger(status.activeRun.selectedSitesCount)} selected sites processed
          </Typography>
          <LinearProgress
            variant="determinate"
            value={getProgressPercent(
              status.activeRun.processedSitesCount,
              status.activeRun.selectedSitesCount
            )}
            sx={{ mt: 1, maxWidth: 480 }}
          />
        </Alert>
      )}
      {status.isWaitingForUsageReset && (
        <Alert severity="warning">
          The scheduled sync is paused because Ahrefs still reports the previous usage
          period. Limits will be checked again automatically every hour; Batch Analysis
          will not be called until the reset is confirmed.
        </Alert>
      )}

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: '1fr',
            sm: 'repeat(2, minmax(0, 1fr))',
            lg: 'repeat(4, minmax(0, 1fr))',
          },
          gap: 2,
        }}
      >
        <MetricCard
          label="Effective available units"
          value={formatInteger(status.effectiveAvailableUnits)}
          helper={`${formatInteger(status.spendableUnits)} spendable after ${formatInteger(status.safetyBufferUnits)} safety buffer`}
        />
        <MetricCard
          label="Next run capacity"
          value={`${formatInteger(status.plannedSitesCount)} of ${formatInteger(status.eligibleSitesCount)} sites`}
          helper={
            capacityReasons.length > 0
              ? `${formatInteger(status.plannedEstimatedUnits)} estimated units · limited by ${capacityReasons.join(' and ')}`
              : `${formatInteger(status.plannedEstimatedUnits)} estimated units`
          }
          warning={!status.canStartRun}
        />
        <MetricCard
          label={scheduleLabel}
          value={scheduleValue}
          helper={
            status.isWaitingForUsageReset
              ? `Last reported reset: ${formatUtcDateTime(status.usageResetDate)}`
              : status.hasCompletedMonthlyRun
                ? 'No additional automatic run will start this month'
                : status.isDueNow
              ? `Scheduled occurrence was ${formatUtcDateTime(status.dueOccurrenceUtc)}`
              : status.schedulerEnabled
                ? 'Automatic monthly run'
                : 'Automatic runs are disabled'
          }
          badge={
            <Chip
              size="small"
              label={status.schedulerEnabled ? 'Scheduler on' : 'Scheduler off'}
              color={status.schedulerEnabled ? 'success' : 'default'}
              variant="outlined"
            />
          }
          warning={status.isDueNow || status.isWaitingForUsageReset}
        />
        <MetricCard
          label="Usage reset"
          value={formatUtcDateTime(status.usageResetDate)}
          helper={`Limits checked: ${formatDateTime(status.limitsCheckedAt)}`}
        />
      </Box>

      {status.plannedSitesCount < status.eligibleSitesCount &&
        !status.isWaitingForUsageReset &&
        !status.hasCompletedMonthlyRun && (
        <Alert severity="warning">
          The next run can process {formatInteger(status.plannedSitesCount)} of{' '}
          {formatInteger(status.eligibleSitesCount)} eligible sites. The remaining sites
          will not be updated this month.
        </Alert>
        )}

      <Accordion variant="outlined" disableGutters>
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Typography sx={{ fontWeight: 700 }}>Technical details</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', lg: '1fr 1fr' },
              gap: 3,
            }}
          >
            <Box>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
                Budget breakdown
              </Typography>
              <Box
                sx={{
                  display: 'grid',
                  gridTemplateColumns: {
                    xs: 'repeat(2, minmax(0, 1fr))',
                    sm: 'repeat(5, minmax(0, 1fr))',
                  },
                  gap: 1.5,
                }}
              >
                <BudgetValue
                  label="API key"
                  value={status.apiKeyRemainingUnits}
                  limiting={limitingBudgets.has('api-key')}
                />
                <BudgetValue
                  label="Workspace"
                  value={status.workspaceRemainingUnits}
                  limiting={limitingBudgets.has('workspace')}
                />
                <BudgetValue
                  label="App budget"
                  value={status.appBudgetRemainingUnits}
                  limiting={limitingBudgets.has('app')}
                />
                <BudgetValue label="Effective" value={status.effectiveAvailableUnits} />
                <BudgetValue label="Spendable" value={status.spendableUnits} />
              </Box>
            </Box>
            <Box>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
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
            <ConfigRow label="Cron (UTC)" value={status.cron} />
            <ConfigRow label="Not before" value={formatUtcDateTime(status.notBeforeUtc)} />
            <ConfigRow label="Target mode" value={status.targetMode} />
            <ConfigRow label="Protocol" value={status.protocol} />
            <ConfigRow label="Volume mode" value={status.volumeMode} />
            <ConfigRow label="Cost/site" value="12 units" />
            <ConfigRow label="Batch size" value={formatInteger(status.batchSize)} />
            <ConfigRow label="Max sites/run" value={formatInteger(status.maxSitesPerRun)} />
          </Box>
            </Box>
          </Box>
        </AccordionDetails>
      </Accordion>
    </>
  );
}

function MetricCard({
  label,
  value,
  helper,
  badge,
  warning = false,
}: {
  label: string;
  value: string;
  helper: string;
  badge?: React.ReactNode;
  warning?: boolean;
}) {
  return (
    <Paper
      variant="outlined"
      sx={{ p: 2, borderColor: warning ? 'warning.main' : undefined }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
        <Typography variant="body2" color="text.secondary">
          {label}
        </Typography>
        {badge}
      </Box>
      <Typography variant="h5" sx={{ fontWeight: 700, my: 0.75 }}>
        {value}
      </Typography>
      <Typography variant="body2" color="text.secondary">
        {helper}
      </Typography>
    </Paper>
  );
}

function BudgetValue({
  label,
  value,
  limiting = false,
}: {
  label: string;
  value: number;
  limiting?: boolean;
}) {
  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, flexWrap: 'wrap' }}>
        <Typography variant="body2" color="text.secondary">
          {label}
        </Typography>
        {limiting && <Chip size="small" label="Limiting" color="warning" variant="outlined" />}
      </Box>
      <Typography variant="h6" sx={{ fontWeight: 700, mt: 0.25 }}>
        {formatInteger(value)}
      </Typography>
    </Box>
  );
}

function getProgressPercent(processed: number, selected: number): number {
  return selected <= 0 ? 0 : Math.min(100, (processed / selected) * 100);
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
