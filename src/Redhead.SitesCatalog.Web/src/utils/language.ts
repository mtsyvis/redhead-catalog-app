import type { FilterOption } from '../types/sites.types';

export const LANGUAGE_OPTIONS: readonly FilterOption[] = [
  { value: 'EN', label: 'English' },
  { value: 'DE', label: 'German' },
  { value: 'FR', label: 'French' },
  { value: 'ES', label: 'Spanish' },
  { value: 'IT', label: 'Italian' },
  { value: 'PT', label: 'Portuguese' },
  { value: 'RU', label: 'Russian' },
  { value: 'UK', label: 'Ukrainian' },
  { value: 'PL', label: 'Polish' },
  { value: 'NL', label: 'Dutch' },
  { value: 'TR', label: 'Turkish' },
  { value: 'ID', label: 'Indonesian' },
  { value: 'JA', label: 'Japanese' },
  { value: 'KO', label: 'Korean' },
  { value: 'ZH', label: 'Chinese' },
  { value: 'AR', label: 'Arabic' },
  { value: 'HI', label: 'Hindi' },
  { value: 'UNKNOWN', label: 'Unknown' },
  { value: 'MULTI', label: 'Multiple languages' },
];

const LANGUAGE_OPTION_BY_VALUE = new Map(
  LANGUAGE_OPTIONS.map((option) => [option.value, option])
);

export function normalizeLanguageCode(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed.toUpperCase() : null;
}

export function formatLanguageCode(value: string | null | undefined): string {
  return normalizeLanguageCode(value) ?? 'UNKNOWN';
}

export function formatLanguageTableValue(value: string | null | undefined): string {
  const normalized = normalizeLanguageCode(value);
  return normalized === null || normalized === 'UNKNOWN' ? '—' : normalized;
}

export function getLanguageOption(value: string | null | undefined): FilterOption | null {
  const normalized = normalizeLanguageCode(value);
  if (normalized === null) return null;
  return LANGUAGE_OPTION_BY_VALUE.get(normalized) ?? { value: normalized, label: normalized };
}
