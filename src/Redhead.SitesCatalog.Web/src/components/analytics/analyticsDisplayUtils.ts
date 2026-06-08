export function formatDateTime(value: string | null | undefined): string {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString();
}

export function formatDestination(value: string | null | undefined): string {
  if (value === 'GoogleDrive') return 'Google Drive';
  if (value === 'Download') return 'Download';
  return value?.trim() || '—';
}

export function formatExportMode(value: string | null | undefined): string {
  if (value === 'MultiSearch') return 'Multi-search';
  if (value === 'Sites') return 'Sites';
  return value?.trim() || '—';
}

export function formatNullableText(value: string | null | undefined): string {
  return value?.trim() || '—';
}

export function formatClientName(email: string, displayName?: string | null): string {
  const name = displayName?.trim();
  if (!name || name === email) return email;
  return `${name} (${email})`;
}
