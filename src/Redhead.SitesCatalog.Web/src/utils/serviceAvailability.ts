import type {
  ServiceAvailabilityFilter,
  ServiceAvailabilityStatus,
  ServiceAvailabilityStatusValue,
} from '../types/sites.types';

export const SERVICE_AVAILABILITY_STATUS = {
  Unknown: 0,
  Available: 1,
  NotAvailable: 2,
} as const;

export const SERVICE_AVAILABILITY_FILTER_OPTIONS: Array<{
  value: ServiceAvailabilityFilter;
  label: string;
}> = [
  { value: 'all', label: 'All' },
  { value: 'available', label: 'Available' },
  { value: 'notAvailable', label: 'Not available' },
  { value: 'unknown', label: 'Unknown' },
];

export const SERVICE_AVAILABILITY_STATUS_OPTIONS: Array<{
  value: ServiceAvailabilityStatusValue;
  label: string;
}> = [
  { value: SERVICE_AVAILABILITY_STATUS.Available, label: 'Available' },
  { value: SERVICE_AVAILABILITY_STATUS.NotAvailable, label: 'Not available' },
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
  return SERVICE_AVAILABILITY_STATUS.Unknown;
}

export function isServiceAvailable(status: ServiceAvailabilityStatus | null | undefined): boolean {
  return normalizeServiceAvailabilityStatus(status) === SERVICE_AVAILABILITY_STATUS.Available;
}

export function matchesAvailabilityFilter(
  status: ServiceAvailabilityStatus | null | undefined,
  filter: ServiceAvailabilityFilter
): boolean {
  if (filter === 'all') return true;
  const normalized = normalizeServiceAvailabilityStatus(status);
  if (filter === 'available') return normalized === SERVICE_AVAILABILITY_STATUS.Available;
  if (filter === 'notAvailable') return normalized === SERVICE_AVAILABILITY_STATUS.NotAvailable;
  return normalized === SERVICE_AVAILABILITY_STATUS.Unknown;
}

export function formatOptionalServicePrice(
  status: ServiceAvailabilityStatus | null | undefined,
  price: number | null | undefined
): string {
  const normalized = normalizeServiceAvailabilityStatus(status);
  if (normalized === SERVICE_AVAILABILITY_STATUS.NotAvailable) return 'NO';
  if (normalized === SERVICE_AVAILABILITY_STATUS.Available && price != null) return `$${price}`;
  return '—';
}
