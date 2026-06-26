import { useCallback, useEffect, useMemo, useState } from 'react';
import { Navigate, useNavigate, useParams } from 'react-router-dom';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import RefreshIcon from '@mui/icons-material/Refresh';
import {
  Alert,
  Box,
  CircularProgress,
  LinearProgress,
  Paper,
  Tooltip,
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
  ItemStatusChip,
  RunOutcomeChip,
  RunScopeChip,
} from '../components/ahrefs/AhrefsSyncDisplay';
import { useUserRoles } from '../hooks/useUserRoles';
import { ahrefsSyncService } from '../services/ahrefsSync.service';
import type {
  AhrefsSyncRunDetails as RunDetails,
  AhrefsSyncRunItem,
} from '../types/ahrefsSync.types';
import { dataGridLocaleText, formatInteger } from '../utils/numberFormat';
import {
  formatDateTime,
  formatDecimal,
  formatDuration,
  formatRunKind,
  formatSnapshotMonth,
} from '../utils/ahrefsSyncDisplay';

export const AhrefsSyncRunDetails: React.FC = () => {
  const { canManageAhrefsSync } = useUserRoles();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [details, setDetails] = useState<RunDetails | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [paginationModel, setPaginationModel] = useState<GridPaginationModel>({
    page: 0,
    pageSize: 100,
  });

  const loadDetails = useCallback(
    async (background = false) => {
      if (!canManageAhrefsSync || !id) return;
      if (!background) setLoading(true);
      setError(null);
      try {
        setDetails(
          await ahrefsSyncService.getRun(
            id,
            paginationModel.page + 1,
            paginationModel.pageSize
          )
        );
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load Ahrefs sync run.');
      } finally {
        setLoading(false);
      }
    },
    [id, canManageAhrefsSync, paginationModel.page, paginationModel.pageSize]
  );

  useEffect(() => {
    void loadDetails();
  }, [loadDetails]);

  useEffect(() => {
    if (details?.run.status !== 1) return;
    const timer = window.setInterval(() => {
      void loadDetails(true);
    }, 15_000);
    return () => window.clearInterval(timer);
  }, [details?.run.status, loadDetails]);

  const columns = useMemo<GridColDef<AhrefsSyncRunItem>[]>(
    () => [
      {
        field: 'ahrefsIndex',
        headerName: 'Index',
        minWidth: 75,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => (value === null ? '—' : String(value)),
      },
      { field: 'domain', headerName: 'Domain', minWidth: 210, flex: 1 },
      {
        field: 'status',
        headerName: 'Status',
        minWidth: 135,
        renderCell: (params) => <ItemStatusChip status={params.row.status} />,
      },
      {
        field: 'oldTraffic',
        headerName: 'Old traffic',
        minWidth: 115,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'newTraffic',
        headerName: 'New traffic',
        minWidth: 115,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) =>
          value === null ? '—' : formatInteger(value as number),
      },
      {
        field: 'oldDomainRating',
        headerName: 'Old DR',
        minWidth: 85,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatDecimal(value as number),
      },
      {
        field: 'newDomainRating',
        headerName: 'New DR',
        minWidth: 85,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatDecimal(value as number | null),
      },
      {
        field: 'snapshotSaved',
        headerName: 'Snapshot',
        minWidth: 110,
        valueFormatter: (value) => ((value as boolean) ? 'Saved' : 'Not saved'),
      },
      {
        field: 'errorMessage',
        headerName: 'Error',
        minWidth: 240,
        flex: 1.2,
        sortable: false,
        renderCell: (params) => {
          const message = params.row.errorMessage;
          return message ? (
            <Tooltip title={message} placement="top-start">
              <Typography variant="body2" noWrap sx={{ maxWidth: '100%' }}>
                {message}
              </Typography>
            </Tooltip>
          ) : (
            '—'
          );
        },
      },
    ],
    []
  );

  if (!canManageAhrefsSync) {
    return <Navigate to="/sites" replace />;
  }

  return (
    <PageShell
      title="Ahrefs Sync Run"
      maxWidth="xl"
      actions={
        <Box
          sx={{
            display: 'flex',
            gap: 1,
            flexWrap: 'wrap',
            width: { xs: '100%', sm: 'auto' },
          }}
        >
          <BrandButton
            startIcon={<ArrowBackIcon />}
            onClick={() => navigate('/admin/ahrefs-sync')}
          >
            Back
          </BrandButton>
          <BrandButton
            startIcon={<RefreshIcon />}
            onClick={() => void loadDetails()}
            disabled={loading}
          >
            Refresh
          </BrandButton>
        </Box>
      }
    >
      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {loading && !details ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
          <CircularProgress size={32} />
        </Box>
      ) : details ? (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
          {details.run.errorMessage && (
            <Alert severity={details.run.status === 4 ? 'error' : 'warning'}>
              {details.run.errorMessage}
            </Alert>
          )}

          <RunSummary details={details} />

          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="h6" sx={{ fontWeight: 700, mb: 0.5 }}>
              Site results
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Ahrefs results mapped to the request target index for this run.
            </Typography>
            <Box sx={{ overflowX: 'auto' }}>
              <DataGrid
                autoHeight
                rows={details.items}
                columns={columns}
                rowCount={details.totalCount}
                paginationMode="server"
                paginationModel={paginationModel}
                onPaginationModelChange={setPaginationModel}
                pageSizeOptions={[25, 50, 100, 250, 500]}
                disableRowSelectionOnClick
                loading={loading}
                localeText={dataGridLocaleText}
                sx={{
                  border: 0,
                  minWidth: 1220,
                  '& .MuiDataGrid-cell:focus, & .MuiDataGrid-cell:focus-within': {
                    outline: 'none',
                  },
                }}
              />
            </Box>
          </Paper>
        </Box>
      ) : null}
    </PageShell>
  );
};

function RunSummary({ details }: { details: RunDetails }) {
  const { run } = details;
  const progress = getProgressPercent(run.processedSitesCount, run.selectedSitesCount);

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 2,
          flexWrap: 'wrap',
          mb: 2,
        }}
      >
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          Run summary
        </Typography>
        <Box sx={{ display: 'flex', gap: 0.75, flexWrap: 'wrap' }}>
          <RunOutcomeChip run={run} />
          <RunScopeChip run={run} />
        </Box>
      </Box>
      {run.status === 1 && (
        <Box sx={{ mb: 2 }}>
          <Box
            sx={{
              display: 'flex',
              justifyContent: 'space-between',
              gap: 2,
              mb: 0.75,
            }}
          >
            <Typography variant="body2" color="text.secondary">
              Processing sites
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {formatInteger(run.processedSitesCount)} /{' '}
              {formatInteger(run.selectedSitesCount)}
            </Typography>
          </Box>
          <LinearProgress variant="determinate" value={progress} />
        </Box>
      )}
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: '1fr',
            sm: 'repeat(2, minmax(0, 1fr))',
            lg: 'repeat(4, minmax(0, 1fr))',
          },
          gap: 1.5,
        }}
      >
        <SummarySection
          title="Outcome"
          items={[
            ['Kind', formatRunKind(run.runKind)],
            ['Started', formatDateTime(run.startedAt)],
            ['Finished', formatDateTime(run.finishedAt)],
            ['Duration', formatDuration(run.startedAt, run.finishedAt)],
          ]}
        />
        <SummarySection
          title="Coverage"
          items={[
            ['Snapshot month', formatSnapshotMonth(run.snapshotMonth)],
            ['Eligible', formatInteger(run.eligibleSitesCount)],
            ['Selected / processed', `${formatInteger(run.selectedSitesCount)} / ${formatInteger(run.processedSitesCount)}`],
            ['Updated / failed / skipped', `${formatInteger(run.updatedSitesCount)} / ${formatInteger(run.failedSitesCount)} / ${formatInteger(run.skippedSitesCount)}`],
          ]}
        />
        <SummarySection
          title="Cost"
          items={[
            ['Estimated / actual units', `${formatInteger(run.selectedEstimatedUnits)} / ${formatInteger(run.actualUnits)}`],
            ['Available before', formatInteger(run.availableUnitsBefore)],
            [
              'Available after',
              run.availableUnitsAfter === null
                ? '—'
                : formatInteger(run.availableUnitsAfter),
            ],
            ['Cost per site', `${formatInteger(run.costPerSite)} units`],
          ]}
        />
        <SummarySection
          title="Request"
          items={[
            ['Target mode', run.targetMode],
            ['Protocol', run.protocol],
            ['Volume mode', run.volumeMode],
            ['Batch / max sites', `${formatInteger(run.batchSize)} / ${formatInteger(run.maxSitesPerRun)}`],
          ]}
        />
      </Box>
    </Paper>
  );
}

function SummarySection({
  title,
  items,
}: {
  title: string;
  items: Array<[string, string]>;
}) {
  return (
    <Box
      sx={{
        border: 1,
        borderColor: 'divider',
        borderRadius: 1,
        p: 1.5,
        minWidth: 0,
      }}
    >
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.25 }}>
        {title}
      </Typography>
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
        {items.map(([label, value]) => (
          <Box key={label}>
            <Typography variant="caption" color="text.secondary">
              {label}
            </Typography>
            <Typography
              variant="body2"
              sx={{ fontWeight: 600, overflowWrap: 'anywhere' }}
            >
              {value}
            </Typography>
          </Box>
        ))}
      </Box>
    </Box>
  );
}

function getProgressPercent(processed: number, selected: number): number {
  return selected <= 0 ? 0 : Math.min(100, (processed / selected) * 100);
}
