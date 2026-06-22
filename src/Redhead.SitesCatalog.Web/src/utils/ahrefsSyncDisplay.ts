const RUN_STATUS_LABELS: Record<number, string> = {
  1: 'Running',
  2: 'Succeeded',
  3: 'Succeeded partial',
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

export function formatRunStatus(status: number): string {
  return RUN_STATUS_LABELS[status] ?? `Unknown (${status})`;
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
  return new Date(value).toISOString().replace('T', ' ').replace('.000Z', ' UTC');
}

export function formatDecimal(value: number | null): string {
  if (value === null) return '—';
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(value);
}
