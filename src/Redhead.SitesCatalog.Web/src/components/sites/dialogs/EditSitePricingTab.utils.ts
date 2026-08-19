import { SERVICE_AVAILABILITY_STATUS } from '../../../utils/serviceAvailability';
import { PRICE_TYPE, type PriceTypeValue } from '../../../utils/pricing';
import {
  getPriceRowsForType,
  pricingAmountErrorKey,
  pricingStatusErrorKey,
  pricingTermErrorKey,
  PRICING_SECTIONS,
  type EditSiteFormState,
  type PricingPriceRow,
} from './EditSiteDialog.helpers';

export type PricingExpansionState = Partial<Record<PriceTypeValue, boolean>>;

function getSectionErrorKeys(priceType: PriceTypeValue, rows: PricingPriceRow[]): string[] {
  const rowKeys = rows.flatMap((row) => [
    pricingAmountErrorKey(row.id),
    pricingTermErrorKey(row.id),
  ]);

  return priceType === PRICE_TYPE.Main
    ? rowKeys
    : [pricingStatusErrorKey(priceType), ...rowKeys];
}

export function hasPricingSectionErrors(
  priceType: PriceTypeValue,
  rows: PricingPriceRow[],
  errors: Record<string, string[]>
): boolean {
  return getSectionErrorKeys(priceType, rows).some((key) => Boolean(errors[key]?.length));
}

function shouldExpandPricingSection(
  form: EditSiteFormState,
  priceType: PriceTypeValue,
  errors: Record<string, string[]>
): boolean {
  const rows = getPriceRowsForType(form, priceType);
  if (priceType === PRICE_TYPE.Main) return true;
  if (hasPricingSectionErrors(priceType, rows, errors)) return true;

  const status = form.pricingStatuses[priceType] ?? SERVICE_AVAILABILITY_STATUS.Unknown;
  return rows.length > 0 || status === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice;
}

export function createDefaultPricingExpansion(
  form: EditSiteFormState,
  errors: Record<string, string[]> = {}
): PricingExpansionState {
  return Object.fromEntries(
    PRICING_SECTIONS.map((section) => [
      section.priceType,
      shouldExpandPricingSection(form, section.priceType, errors),
    ])
  ) as PricingExpansionState;
}
