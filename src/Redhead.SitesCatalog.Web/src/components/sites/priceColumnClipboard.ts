import type { MultiSearchResultItem, ServiceAvailabilityStatus, Site } from '../../types/sites.types';
import {
  SERVICE_AVAILABILITY_STATUS,
  normalizeServiceAvailabilityStatus,
} from '../../utils/serviceAvailability';

export const COPYABLE_PRICE_COLUMNS = [
  'priceUsd',
  'priceCasino',
  'priceCrypto',
  'priceLinkInsert',
  'priceLinkInsertCasino',
  'priceDating',
] as const;

export type CopyablePriceColumn = (typeof COPYABLE_PRICE_COLUMNS)[number];

export const COPYABLE_PRICE_COLUMN_LABELS: Record<CopyablePriceColumn, string> = {
  priceUsd: 'Price USD',
  priceCasino: 'Casino',
  priceCrypto: 'Crypto',
  priceLinkInsert: 'Link Insert',
  priceLinkInsertCasino: 'Link Insert Casino',
  priceDating: 'Dating',
};

type OptionalPriceColumn = Exclude<CopyablePriceColumn, 'priceUsd'>;

const OPTIONAL_PRICE_STATUS_FIELDS = {
  priceCasino: 'priceCasinoStatus',
  priceCrypto: 'priceCryptoStatus',
  priceLinkInsert: 'priceLinkInsertStatus',
  priceLinkInsertCasino: 'priceLinkInsertCasinoStatus',
  priceDating: 'priceDatingStatus',
} satisfies Record<OptionalPriceColumn, keyof Site>;

export function isCopyablePriceColumn(field: string): field is CopyablePriceColumn {
  return COPYABLE_PRICE_COLUMNS.includes(field as CopyablePriceColumn);
}

function formatClipboardPrice(value: number): string {
  return Number.isFinite(value) ? String(value) : '';
}

function formatOptionalPriceColumnValue(site: Site, field: OptionalPriceColumn): string {
  const status = normalizeServiceAvailabilityStatus(
    site[OPTIONAL_PRICE_STATUS_FIELDS[field]] as ServiceAvailabilityStatus
  );

  if (status === SERVICE_AVAILABILITY_STATUS.NotAvailable) return 'NO';
  if (status === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice) return 'YES';

  if (status === SERVICE_AVAILABILITY_STATUS.Available) {
    const value = site[field] as number | null;
    return value == null ? '' : formatClipboardPrice(value);
  }

  return '';
}

export function formatPriceColumnClipboardValue(
  result: MultiSearchResultItem,
  field: CopyablePriceColumn
): string {
  if (!result.found) return '';

  if (field === 'priceUsd') {
    return result.site.priceUsd == null ? 'NO' : formatClipboardPrice(result.site.priceUsd);
  }

  return formatOptionalPriceColumnValue(result.site, field);
}

export function buildPriceColumnClipboardText(
  results: MultiSearchResultItem[],
  field: CopyablePriceColumn
): string {
  return results.map((result) => formatPriceColumnClipboardValue(result, field)).join('\n');
}
