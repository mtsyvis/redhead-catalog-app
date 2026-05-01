import type { TermType, TermTypeValue, TermUnit, TermUnitValue } from '../types/sites.types';

export const TERM_TYPE = {
  Permanent: 1,
  Finite: 2,
} as const;

export const TERM_UNIT = {
  Year: 1,
} as const;

export function normalizeTermType(termType: TermType | null | undefined): TermTypeValue | null {
  if (termType === TERM_TYPE.Permanent || termType === 'Permanent') return TERM_TYPE.Permanent;
  if (termType === TERM_TYPE.Finite || termType === 'Finite') return TERM_TYPE.Finite;
  return null;
}

export function normalizeTermUnit(termUnit: TermUnit | null | undefined): TermUnitValue | null {
  if (termUnit === TERM_UNIT.Year || termUnit === 'Year') return TERM_UNIT.Year;
  return null;
}

export function formatTerm(
  termType: TermType | null | undefined,
  termValue: number | null | undefined,
  termUnit: TermUnit | null | undefined
): string {
  const normalizedType = normalizeTermType(termType);
  const normalizedUnit = normalizeTermUnit(termUnit);

  if (normalizedType === TERM_TYPE.Permanent) return 'Permanent';

  if (
    normalizedType === TERM_TYPE.Finite &&
    normalizedUnit === TERM_UNIT.Year &&
    termValue != null &&
    Number.isInteger(termValue) &&
    termValue > 0
  ) {
    return termValue === 1 ? '1 year' : `${termValue} years`;
  }

  return '—';
}
