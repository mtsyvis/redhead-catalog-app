import type { PricingSite, PricingTerm } from "../../types/pricing.types";
import type {
  ServiceAvailabilityStatusValue,
  UpdateSitePricingPayload,
} from "../../types/sites.types";
import {
  buildTermPayloadFromKey,
  OPTIONAL_PRICE_TYPES,
  PRICE_TYPE,
  PRICE_TYPE_LABELS,
  PRICING_SECTION_ORDER,
  type PriceTypeValue,
} from "../../utils/pricing";
import { parseCatalogPriceAmount } from "../../utils/catalogPriceAmount";
import { SERVICE_AVAILABILITY_STATUS } from "../../utils/serviceAvailability";

export interface CatalogPricingDraft {
  amounts: Record<string, string>;
  statuses: Partial<Record<PriceTypeValue, ServiceAvailabilityStatusValue>>;
}

export const priceCellKey = (type: PriceTypeValue, termKey: string) => `${type}:${termKey}`;

export function getPricingTerms(site: PricingSite | null): PricingTerm[] {
  return site?.terms.length ? site.terms : [{ termKey: "unknown", label: "No term" }];
}

export function createPricingDraft(site: PricingSite): CatalogPricingDraft {
  return {
    amounts: Object.fromEntries(
      site.pricing.prices.map((price) => [
        priceCellKey(price.priceType, price.termKey),
        String(price.amountUsd),
      ])
    ),
    statuses: Object.fromEntries(
      OPTIONAL_PRICE_TYPES.map((type) => [
        type,
        site.pricing.serviceAvailabilities.find((row) => row.serviceType === type)?.status ??
          (site.pricing.prices.some((price) => price.priceType === type)
            ? SERVICE_AVAILABILITY_STATUS.Available
            : SERVICE_AVAILABILITY_STATUS.Unknown),
      ])
    ),
  };
}

export function getPricingChangeSummary(site: PricingSite, draft: CatalogPricingDraft) {
  const original = createPricingDraft(site);
  const changedPrices = Object.keys({ ...original.amounts, ...draft.amounts }).filter(
    (key) => (original.amounts[key] ?? "") !== (draft.amounts[key] ?? "")
  );
  return {
    priceCount: changedPrices.length,
    termCount: new Set(changedPrices.map((key) => key.slice(key.indexOf(":") + 1))).size,
    availabilityCount: OPTIONAL_PRICE_TYPES.filter(
      (type) => original.statuses[type] !== draft.statuses[type]
    ).length,
  };
}

export function hasServicePrices(draft: CatalogPricingDraft, type: PriceTypeValue): boolean {
  return Object.entries(draft.amounts).some(
    ([key, amount]) => key.startsWith(`${type}:`) && amount.trim() !== ""
  );
}

export function changePriceAmount(
  draft: CatalogPricingDraft,
  type: PriceTypeValue,
  termKey: string,
  amount: string
): CatalogPricingDraft {
  const next = { ...draft, amounts: { ...draft.amounts, [priceCellKey(type, termKey)]: amount } };
  if (type === PRICE_TYPE.Main) return next;
  return {
    ...next,
    statuses: {
      ...next.statuses,
      [type]: hasServicePrices(next, type)
        ? SERVICE_AVAILABILITY_STATUS.Available
        : SERVICE_AVAILABILITY_STATUS.Unknown,
    },
  };
}

export function changeServiceStatus(
  draft: CatalogPricingDraft,
  type: PriceTypeValue,
  status: ServiceAvailabilityStatusValue
): CatalogPricingDraft {
  return {
    amounts:
      status === SERVICE_AVAILABILITY_STATUS.Available
        ? draft.amounts
        : Object.fromEntries(
            Object.entries(draft.amounts).map(([key, amount]) => [
              key,
              key.startsWith(`${type}:`) ? "" : amount,
            ])
          ),
    statuses: { ...draft.statuses, [type]: status },
  };
}

export function buildPricingPayload(
  terms: PricingTerm[],
  draft: CatalogPricingDraft
): { pricing: UpdateSitePricingPayload; errors: string[] } {
  const errors: string[] = [];
  const prices: UpdateSitePricingPayload["prices"] = [];
  for (const term of terms) {
    for (const type of PRICING_SECTION_ORDER) {
      const { amount, error } = parseCatalogPriceAmount(
        draft.amounts[priceCellKey(type, term.termKey)] ?? ""
      );
      if (error) errors.push(`${PRICE_TYPE_LABELS[type]} (${term.label}): ${error}`);
      if (amount != null)
        prices.push({
          priceType: type,
          ...buildTermPayloadFromKey(term.termKey),
          amountUsd: amount,
        });
    }
  }
  const serviceAvailabilities = OPTIONAL_PRICE_TYPES.map((type) => {
    const status = draft.statuses[type] ?? SERVICE_AVAILABILITY_STATUS.Unknown;
    if (
      status === SERVICE_AVAILABILITY_STATUS.Available &&
      !prices.some((price) => price.priceType === type)
    ) {
      errors.push(`${PRICE_TYPE_LABELS[type]}: enter a price or select YES, NO, or Unknown.`);
    }
    return { serviceType: type, status };
  });
  return { pricing: { prices, serviceAvailabilities }, errors };
}
