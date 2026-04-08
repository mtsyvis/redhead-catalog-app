import dayjs, { type Dayjs } from 'dayjs';

/** Serializes a dayjs value to API format `yyyy-MM`. Returns null for empty/invalid input. */
export function toApiMonth(value: Dayjs | null): string | null {
  if (!value || !value.isValid()) return null;
  return value.format('YYYY-MM');
}

/** Parses an API `yyyy-MM` string to a dayjs value. Returns null for empty input. */
export function fromApiMonth(value: string | null): Dayjs | null {
  if (!value) return null;
  return dayjs(value, 'YYYY-MM');
}
