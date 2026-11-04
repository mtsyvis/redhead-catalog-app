export const SYNC_POLL_INTERVAL_MS = 15_000;
export const SYNC_IDLE_POLL_INTERVAL_MS = 60_000;
// Values match the explicit API enum values; labels and colors stay together.
export const SYNC_RUN_STATUSES = [
  { value: 0, label: 'Running', color: 'info' },
  { value: 1, label: 'Waiting', color: 'warning' },
  { value: 2, label: 'Completed', color: 'success' },
  { value: 3, label: 'Completed with errors', color: 'warning' },
] as const;
export const SYNC_ITEM_STATUSES = [
  { value: 0, label: 'Pending', color: 'warning' },
  { value: 1, label: 'In progress', color: 'warning' },
  { value: 2, label: 'Prices found', color: 'success' },
  { value: 3, label: 'No prices found', color: 'default' },
  { value: 4, label: 'Error', color: 'error' },
  { value: 5, label: 'Fresh cache reused', color: 'success' },
  { value: 6, label: 'Site removed', color: 'default' },
] as const;

export function syncStatusPresentation(status: number, item: boolean) {
  return (
    (item ? SYNC_ITEM_STATUSES : SYNC_RUN_STATUSES).find((value) => value.value === status) ??
    ({ label: 'Unknown', color: 'default' } as const)
  );
}

const dateTimeFormatter = new Intl.DateTimeFormat('en-US', {
  dateStyle: 'medium',
  timeStyle: 'short',
});

export function syncDate(value: string | null | undefined) {
  return value ? dateTimeFormatter.format(new Date(value)) : '—';
}
