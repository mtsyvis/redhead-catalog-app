import { useCallback, useState } from 'react';
import { Navigate, useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, CircularProgress, Paper, Stack, Typography } from '@mui/material';
import RefreshIcon from '@mui/icons-material/Refresh';
import type { GridPaginationModel } from '@mui/x-data-grid';
import { PageShell } from '../components/layout/PageShell';
import { BrandButton } from '../components/common/BrandButton';
import { SyncRunSummary } from '../components/linkdetective/LinkDetectiveSyncDisplay';
import { syncDate } from '../utils/linkDetectiveSyncDisplay';
import { useUserRoles } from '../hooks/useUserRoles';
import { linkDetectiveSyncService as service } from '../services/linkDetectiveSync.service';
import { useLinkDetectiveSyncMonitoring } from '../hooks/useLinkDetectiveSyncMonitoring';
import { LinkDetectiveSyncRunHistory } from '../components/linkdetective/LinkDetectiveSyncRunHistory';
import { LinkDetectiveSyncStatusPanels } from '../components/linkdetective/LinkDetectiveSyncStatusPanels';
import { LinkDetectiveSyncConfiguration } from '../components/linkdetective/LinkDetectiveSyncConfiguration';
import { LinkDetectiveSyncRunDetails } from './LinkDetectiveSyncRunDetails';

const path = '/admin/linkdetective-sync';

export function LinkDetectiveSync() {
  const { id } = useParams<{ id: string }>();
  // Keep the history page when opening details and returning to the overview.
  const [pagination, setPagination] = useState<GridPaginationModel>({ page: 0, pageSize: 25 });
  return id ? (
    <LinkDetectiveSyncRunDetails key={id} id={id} />
  ) : (
    <SyncOverview pagination={pagination} onPaginationChange={setPagination} />
  );
}

function SyncOverview({
  pagination,
  onPaginationChange,
}: {
  pagination: GridPaginationModel;
  onPaginationChange: (value: GridPaginationModel) => void;
}) {
  const { canReadLinkDetectiveSync } = useUserRoles();
  const navigate = useNavigate();
  const loadRuns = useCallback(
    () => service.getRuns(pagination.page + 1, pagination.pageSize),
    [pagination.page, pagination.pageSize]
  );
  const {
    status,
    data: runs,
    error,
    loading,
    refresh,
  } = useLinkDetectiveSyncMonitoring(canReadLinkDetectiveSync, loadRuns);

  const openRun = useCallback((runId: string) => navigate(`${path}/runs/${runId}`), [navigate]);

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

        {status?.activeRun && (
          <>
            <SyncRunSummary run={status.activeRun} />
            <Box>
              <Button onClick={() => openRun(status.activeRun!.id)}>View run details →</Button>
            </Box>
          </>
        )}
        {status && !status.activeRun && (
          <Paper variant="outlined" sx={{ p: 2.5 }}>
            <Typography variant="h6">
              {status.enabled && status.configured ? 'No active run' : 'Background sync'}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
              {status.enabled && status.configured
                ? `Next daily selection: ${status.nextSelectionAtUtc ? syncDate(status.nextSelectionAtUtc) : 'On scheduler startup'}`
                : 'Automatic checks resume after the server configuration is enabled.'}
            </Typography>
          </Paper>
        )}
        {status && <LinkDetectiveSyncStatusPanels status={status} />}
        <LinkDetectiveSyncRunHistory
          runs={runs}
          pagination={pagination}
          onPaginationChange={onPaginationChange}
          loading={loading}
          openRun={openRun}
        />
        {status && <LinkDetectiveSyncConfiguration status={status} />}
      </Stack>
    </PageShell>
  );
}
