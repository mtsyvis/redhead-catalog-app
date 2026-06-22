import type { AhrefsSyncRun } from '../types/ahrefsSync.types';

const RUN_STATUS_LABELS: Record<number, string> = {
  1: 'Running',
  2: 'Completed',
  4: 'Failed',
  5: 'Skipped: already completed',
  6: 'Skipped: insufficient units',
  7: 'Stopped: insufficient units',
  8: 'Cancelled',
};

const RUN_KIND_LABELS: Record<number, string> = {
  1: 'Scheduled',
  2: 'Manual full',
  3: 'Manual limited',
  4: 'Dry run',
};

const ITEM_STATUS_LABELS: Record<number, string> = {
  1: 'Succeeded',
  2: 'Failed',
  3: 'Not returned',
  4: 'Skipped',
};

export function formatRunOutcome(run: AhrefsSyncRun): string {
  if (run.status === 3) {
    return run.failedSitesCount > 0 || run.skippedSitesCount > 0
      ? 'Completed with issues'
      : 'Completed';
  }

  return RUN_STATUS_LABELS[run.status] ?? `Unknown (${run.status})`;
}

export function formatRunKind(kind: number): string {
  return RUN_KIND_LABELS[kind] ?? `Unknown (${kind})`;
}

export function formatItemStatus(status: number): string {
  return ITEM_STATUS_LABELS[status] ?? `Unknown (${status})`;
}

export function formatDateTime(value: string | null): string {
  if (!value) return '—';
  return new Intl.DateTimeFormat('en-US', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

export function formatUtcDateTime(value: string | null): string {
  if (!value) return '—';
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'UTC',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).formatToParts(new Date(value));
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((candidate) => candidate.type === type)?.value ?? '';
  return `${part('month')} ${part('day')}, ${part('year')} at ${part('hour')}:${part('minute')} UTC`;
}

export function formatDecimal(value: number | null): string {
  if (value === null) return '—';
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(value);
}

export function formatSnapshotMonth(value: string): string {
  return new Intl.DateTimeFormat('en-US', {
    timeZone: 'UTC',
    month: 'long',
    year: 'numeric',
  }).format(new Date(`${value}T00:00:00Z`));
}

export function formatDuration(startedAt: string, finishedAt: string | null): string {
  const start = new Date(startedAt).getTime();
  const end = finishedAt ? new Date(finishedAt).getTime() : Date.now();
  const totalSeconds = Math.max(0, Math.round((end - start) / 1000));
  if (totalSeconds < 60) return `${totalSeconds}s`;

  const totalMinutes = Math.floor(totalSeconds / 60);
  if (totalMinutes < 60) {
    return `${totalMinutes}m ${totalSeconds % 60}s`;
  }

  const hours = Math.floor(totalMinutes / 60);
  return `${hours}h ${totalMinutes % 60}m`;
}
