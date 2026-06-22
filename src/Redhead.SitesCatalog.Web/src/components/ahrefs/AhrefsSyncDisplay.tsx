import { Chip } from '@mui/material';
import type { AhrefsSyncRun } from '../../types/ahrefsSync.types';
import {
  formatItemStatus,
  formatRunOutcome,
} from '../../utils/ahrefsSyncDisplay';

export function RunOutcomeChip({ run }: { run: AhrefsSyncRun }) {
  const hasIssues =
    run.status === 3 && (run.failedSitesCount > 0 || run.skippedSitesCount > 0);
  const color =
    run.status === 2 || (run.status === 3 && !hasIssues)
      ? 'success'
      : run.status === 1
        ? 'info'
        : run.status === 3 ||
            run.status === 5 ||
            run.status === 6 ||
            run.status === 7
          ? 'warning'
          : 'error';
  return (
    <Chip
      size="small"
      label={formatRunOutcome(run)}
      color={color}
      variant="outlined"
    />
  );
}

export function RunScopeChip({ run }: { run: AhrefsSyncRun }) {
  if (run.isFullCoverage) {
    return <Chip size="small" label="Full catalog" color="success" variant="outlined" />;
  }

  const configuredSiteLimit = Math.min(run.eligibleSitesCount, run.maxSitesPerRun);
  const wasActuallyLimitedByBudget =
    run.wasLimitedByBudget && run.selectedSitesCount < configuredSiteLimit;

  if (wasActuallyLimitedByBudget) {
    return <Chip size="small" label="Budget limited" color="warning" variant="outlined" />;
  }

  return (
    <Chip
      size="small"
      label={run.runKind === 3 ? 'Limited run' : 'Partial coverage'}
      variant="outlined"
    />
  );
}

export function ItemStatusChip({ status }: { status: number }) {
  const color = status === 1 ? 'success' : status === 2 ? 'error' : 'warning';
  return (
    <Chip
      size="small"
      label={formatItemStatus(status)}
      color={color}
      variant="outlined"
    />
  );
}
