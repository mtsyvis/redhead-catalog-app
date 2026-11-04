import { Alert, Box, Chip, LinearProgress, Paper, Stack, Typography } from '@mui/material';
import type { LinkDetectiveSyncRun } from '../../types/linkDetectiveSync.types';
import { formatInteger } from '../../utils/numberFormat';
import { syncStatusPresentation, syncDate } from '../../utils/linkDetectiveSyncDisplay';

export function SyncStatusChip({ status, item = false }: { status: number; item?: boolean }) {
  const { label, color } = syncStatusPresentation(status, item);
  return <Chip size="small" label={label} color={color} />;
}

export function SyncRunSummary({ run }: { run: LinkDetectiveSyncRun }) {
  const percent = run.selectedCount
    ? Math.min(100, (run.processedCount / run.selectedCount) * 100)
    : 0;
  return (
    <Paper variant="outlined" sx={{ p: 2.5 }}>
      <Stack
        direction="row"
        alignItems="center"
        justifyContent="space-between"
        gap={1}
        flexWrap="wrap"
      >
        <Typography variant="h6">{run.finishedAtUtc ? 'Run summary' : 'Current run'}</Typography>
        <SyncStatusChip status={run.status} />
      </Stack>
      <Stack direction="row" justifyContent="space-between" sx={{ mt: 2, mb: 1 }}>
        <Typography variant="body2">
          {formatInteger(run.processedCount)} / {formatInteger(run.selectedCount)} domains processed
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {percent.toFixed(1)}%
        </Typography>
      </Stack>
      <LinearProgress variant="determinate" value={percent} aria-label="Domains processed" />
      <Stack direction="row" gap={2.5} flexWrap="wrap" sx={{ mt: 1.5 }}>
        {[
          ['Prices found', run.pricesFoundCount],
          ['No prices found', run.notFoundCount],
          ['Errors', run.failedCount],
          ['Fresh cache reused', run.reusedCount],
          ['Site removed', run.skippedCount],
          ['Pending', run.selectedCount - run.processedCount],
        ].map(([label, count]) => (
          <Typography variant="body2" key={label}>
            <Box component="span" sx={{ color: 'text.secondary', mr: 0.75 }}>
              {label}
            </Box>
            {formatInteger(count as number)}
          </Typography>
        ))}
      </Stack>
      {run.waitingReason && (
        <Alert severity="warning" sx={{ mt: 2 }}>
          {run.waitingReason}
          {run.nextAttemptAtUtc && <> Retry after {syncDate(run.nextAttemptAtUtc)}.</>}
        </Alert>
      )}
      <Typography variant="body2" color="text.secondary" sx={{ mt: 1.5 }}>
        Started: {syncDate(run.startedAtUtc)} · Finished: {syncDate(run.finishedAtUtc)}
      </Typography>
    </Paper>
  );
}
