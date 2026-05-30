import type {
  ServiceAvailabilityFilter,
  ServiceAvailabilityFilterValue,
  ServiceAvailabilityStatus,
  ServiceAvailabilityStatusValue,
} from '../types/sites.types';

export const SERVICE_AVAILABILITY_STATUS = {
  Unknown: 0,
  Available: 1,
  NotAvailable: 2,
  AvailableWithUnknownPrice: 3,
} as const;

export const SERVICE_AVAILABILITY_FILTER_OPTIONS: Array<{
  value: ServiceAvailabilityFilterValue;
  label: string;
}> = [
  { value: 'available', label: 'Has price' },
  { value: 'availableWithUnknownPrice', label: 'YES' },
  { value: 'notAvailable', label: 'NO' },
  { value: 'unknown', label: 'Unknown' },
];

export const SERVICE_AVAILABILITY_STATUS_OPTIONS: Array<{
  value: ServiceAvailabilityStatusValue;
  label: string;
}> = [
  { value: SERVICE_AVAILABILITY_STATUS.Available, label: 'Has price' },
  { value: SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice, label: 'YES' },
  { value: SERVICE_AVAILABILITY_STATUS.NotAvailable, label: 'NO' },
  { value: SERVICE_AVAILABILITY_STATUS.Unknown, label: 'Unknown' },
];

export function normalizeServiceAvailabilityStatus(
  status: ServiceAvailabilityStatus | null | undefined
): ServiceAvailabilityStatusValue {
  if (status === SERVICE_AVAILABILITY_STATUS.Available || status === 'Available') {
    return SERVICE_AVAILABILITY_STATUS.Available;
  }
  if (status === SERVICE_AVAILABILITY_STATUS.NotAvailable || status === 'NotAvailable') {
    return SERVICE_AVAILABILITY_STATUS.NotAvailable;
  }
  if (
    status === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice ||
    status === 'AvailableWithUnknownPrice'
  ) {
    return SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice;
  }
  return SERVICE_AVAILABILITY_STATUS.Unknown;
}

export function isServiceAvailable(status: ServiceAvailabilityStatus | null | undefined): boolean {
  const normalized = normalizeServiceAvailabilityStatus(status);
  return (
    normalized === SERVICE_AVAILABILITY_STATUS.Available ||
    normalized === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice
  );
}

export function matchesAvailabilityFilter(
  status: ServiceAvailabilityStatus | null | undefined,
  filter: ServiceAvailabilityFilter
): boolean {
  const normalizedFilter = normalizeServiceAvailabilityFilter(filter);
  if (normalizedFilter.length === 0) return true;
  const normalized = normalizeServiceAvailabilityStatus(status);
  return normalizedFilter.some((value) => {
    if (value === 'available') return normalized === SERVICE_AVAILABILITY_STATUS.Available;
    if (value === 'availableWithUnknownPrice') {
      return normalized === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice;
    }
    if (value === 'notAvailable') return normalized === SERVICE_AVAILABILITY_STATUS.NotAvailable;
    return normalized === SERVICE_AVAILABILITY_STATUS.Unknown;
  });
}

export function getServiceAvailabilityFilterLabel(value: ServiceAvailabilityFilterValue): string {
  return (
    SERVICE_AVAILABILITY_FILTER_OPTIONS.find((option) => option.value === value)?.label ?? value
  );
}

export function normalizeServiceAvailabilityFilter(
  value: ServiceAvailabilityFilter | ServiceAvailabilityFilterValue | 'all' | null | undefined
): ServiceAvailabilityFilter {
  if (!value || value === 'all') return [];
  const values = Array.isArray(value) ? value : [value];
  const allowedValues = new Set(SERVICE_AVAILABILITY_FILTER_OPTIONS.map((option) => option.value));
  return values.filter((item): item is ServiceAvailabilityFilterValue => allowedValues.has(item));
}

export function formatOptionalServicePrice(
  status: ServiceAvailabilityStatus | null | undefined,
  price: number | null | undefined
): string {
  const normalized = normalizeServiceAvailabilityStatus(status);
  if (normalized === SERVICE_AVAILABILITY_STATUS.NotAvailable) return 'NO';
  if (normalized === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice) return 'YES';
  if (normalized === SERVICE_AVAILABILITY_STATUS.Available && price != null) return `$${price}`;
  return '—';
}

export function getOptionalServiceSortRank(
  status: ServiceAvailabilityStatus | null | undefined
): number {
  const normalized = normalizeServiceAvailabilityStatus(status);
  if (normalized === SERVICE_AVAILABILITY_STATUS.Available) return 0;
  if (normalized === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice) return 1;
  if (normalized === SERVICE_AVAILABILITY_STATUS.NotAvailable) return 2;
  return 3;
}
