import type { MultiSearchResultItem } from '../../types/sites.types';
import {
  SERVICE_AVAILABILITY_STATUS,
  normalizeServiceAvailabilityStatus,
} from '../../utils/serviceAvailability';
import {
  PRICE_FIELD_TO_TYPE,
  PRICE_TYPE,
  getLowestPriceAmount,
  getServiceStatus,
} from '../../utils/pricing';

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

export function isCopyablePriceColumn(field: string): field is CopyablePriceColumn {
  return COPYABLE_PRICE_COLUMNS.includes(field as CopyablePriceColumn);
}

function formatClipboardPrice(value: number): string {
  return Number.isFinite(value) ? String(value) : '';
}

export function formatPriceColumnClipboardValue(
  result: MultiSearchResultItem,
  field: CopyablePriceColumn
): string {
  if (!result.found) return '';

  const priceType = PRICE_FIELD_TO_TYPE[field];
  const amount = getLowestPriceAmount(result.site, priceType, null);
  if (amount !== null) return formatClipboardPrice(amount);
  if (priceType === PRICE_TYPE.Main) return '';

  const status = normalizeServiceAvailabilityStatus(getServiceStatus(result.site, priceType));
  if (status === SERVICE_AVAILABILITY_STATUS.NotAvailable) return 'NO';
  if (status === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice) return 'YES';

  return '';
}

export function buildPriceColumnClipboardText(
  results: MultiSearchResultItem[],
  field: CopyablePriceColumn
): string {
  return results.map((result) => formatPriceColumnClipboardValue(result, field)).join('\n');
}
