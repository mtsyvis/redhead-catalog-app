export type ExportLimitMode = 'Disabled' | 'Limited' | 'Unlimited';

export function formatExportLimit(mode: ExportLimitMode, rows: number | null): string {
  if (mode === 'Unlimited') return 'Unlimited';
  if (mode === 'Disabled') return 'Disabled';
  return `${rows ?? '?'} rows`;
}
