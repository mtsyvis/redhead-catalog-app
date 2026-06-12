import type {
  ServiceAvailabilityStatusValue,
  Site,
  UpdateSitePayload,
} from '../../../types/sites.types';
import {
  SERVICE_AVAILABILITY_STATUS,
  normalizeServiceAvailabilityStatus,
} from '../../../utils/serviceAvailability';
import { normalizeLanguageCode } from '../../../utils/language';
import {
  OPTIONAL_PRICE_TYPES,
  PRICE_TYPE,
  PRICE_TYPE_LABELS,
  PRICING_SECTION_ORDER,
  TERM_KEY_OPTIONS,
  type PriceTypeValue,
  buildTermPayloadFromKey,
  getPrices,
  getServiceStatus,
} from '../../../utils/pricing';

export type PricingPriceRow = {
  id: string;
  priceType: PriceTypeValue;
  termKey: string;
  amountUsd: string;
};

export type PricingStatusState = Partial<Record<PriceTypeValue, ServiceAvailabilityStatusValue>>;

export type EditSiteFormState = {
  dr: string;
  traffic: string;
  location: string;
  language: string;
  pricingRows: PricingPriceRow[];
  pricingStatuses: PricingStatusState;
  numberDFLinks: string;
  niche: string;
  categories: string;
  sponsoredTag: string;
  isQuarantined: boolean;
  quarantineReason: string;
};

export const OPTIONAL_PRICING_SECTIONS = OPTIONAL_PRICE_TYPES.map((priceType) => ({
  priceType,
  label: PRICE_TYPE_LABELS[priceType],
}));

export const PRICING_SECTIONS = PRICING_SECTION_ORDER.map((priceType) => ({
  priceType,
  label: PRICE_TYPE_LABELS[priceType],
  isOptional: priceType !== PRICE_TYPE.Main,
}));

export const CONFIRM_CLEAR_SERVICE_PRICES_MESSAGE =
  'Changing availability will remove all prices for this service. Continue?';

export function parseNumberOrNull(input: string): number | null {
  const t = input.trim();
  if (t === '') return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

export function createPricingRowId(priceType: PriceTypeValue, termKey: string): string {
  return `${priceType}:${termKey}:${Math.random().toString(36).slice(2)}`;
}

export function createEmptyPricingRow(priceType: PriceTypeValue): PricingPriceRow {
  return {
    id: createPricingRowId(priceType, TERM_KEY_OPTIONS[0].termKey),
    priceType,
    termKey: TERM_KEY_OPTIONS[0].termKey,
    amountUsd: '',
  };
}

export function clearFieldError(
  errors: Record<string, string[]>,
  key: string
): Record<string, string[]> {
  if (errors[key]) {
    const next = { ...errors };
    delete next[key];
    return next;
  }
  return errors;
}

export const EMPTY_FORM_STATE: EditSiteFormState = {
  dr: '',
  traffic: '',
  location: '',
  language: '',
  pricingRows: [],
  pricingStatuses: Object.fromEntries(
    OPTIONAL_PRICE_TYPES.map((priceType) => [priceType, SERVICE_AVAILABILITY_STATUS.Unknown])
  ) as PricingStatusState,
  numberDFLinks: '',
  niche: '',
  categories: '',
  sponsoredTag: '',
  isQuarantined: false,
  quarantineReason: '',
};

export function createInitialFormState(site: Site): EditSiteFormState {
  const pricingRows = PRICING_SECTION_ORDER.flatMap((priceType) =>
    getPrices(site, priceType).map((price) => ({
      id: createPricingRowId(priceType, price.termKey),
      priceType,
      termKey: price.termKey,
      amountUsd: String(price.amountUsd),
    }))
  );

  const pricingStatuses = Object.fromEntries(
    OPTIONAL_PRICE_TYPES.map((priceType) => {
      const hasPrices = pricingRows.some((row) => row.priceType === priceType);
      const status = hasPrices
        ? SERVICE_AVAILABILITY_STATUS.Available
        : normalizeServiceAvailabilityStatus(getServiceStatus(site, priceType));
      return [priceType, status];
    })
  ) as PricingStatusState;

  return {
    dr: String(site.dr ?? ''),
    traffic: String(site.traffic ?? ''),
    location: site.location ?? '',
    language: normalizeLanguageCode(site.language) ?? '',
    pricingRows,
    pricingStatuses,
    numberDFLinks: site.numberDFLinks == null ? '' : String(site.numberDFLinks),
    niche: site.niche ?? '',
    categories: site.categories ?? '',
    sponsoredTag: site.sponsoredTag ?? '',
    isQuarantined: Boolean(site.isQuarantined),
    quarantineReason: site.quarantineReason ?? '',
  };
}

export function getPriceRowsForType(
  form: EditSiteFormState,
  priceType: PriceTypeValue
): PricingPriceRow[] {
  return form.pricingRows.filter((row) => row.priceType === priceType);
}

function validatePricingRows(
  form: EditSiteFormState,
  errors: Record<string, string[]>
): void {
  const seenTerms = new Set<string>();

  for (const row of form.pricingRows) {
    const amountKey = pricingAmountErrorKey(row.id);
    const termKey = pricingTermErrorKey(row.id);
    const amount = parseNumberOrNull(row.amountUsd);

    if (row.amountUsd.trim() === '') {
      errors[amountKey] = ['Amount is required.'];
    } else if (amount === null) {
      errors[amountKey] = ['Amount must be a valid number.'];
    } else if (amount <= 0) {
      errors[amountKey] = ['Amount must be greater than 0.'];
    }

    const duplicateKey = `${row.priceType}:${row.termKey}`;
    if (seenTerms.has(duplicateKey)) {
      errors[termKey] = [`Duplicate ${PRICE_TYPE_LABELS[row.priceType]} term.`];
    }
    seenTerms.add(duplicateKey);
  }

  for (const priceType of OPTIONAL_PRICE_TYPES) {
    const status = form.pricingStatuses[priceType] ?? SERVICE_AVAILABILITY_STATUS.Unknown;
    const rows = getPriceRowsForType(form, priceType);
    const statusKey = pricingStatusErrorKey(priceType);

    if (status === SERVICE_AVAILABILITY_STATUS.Available && rows.length === 0) {
      errors[statusKey] = ['Add at least one price or choose YES, NO, or Unknown.'];
    }

    if (status !== SERVICE_AVAILABILITY_STATUS.Available && rows.length > 0) {
      errors[statusKey] = ['Remove prices before saving this service status.'];
    }
  }
}

export function validateEditSiteForm(form: EditSiteFormState): Record<string, string[]> {
  const errors: Record<string, string[]> = {};

  const parsedDr = parseNumberOrNull(form.dr);
  if (parsedDr === null || parsedDr < 0 || parsedDr > 100) {
    errors.dr = ['DR must be between 0 and 100.'];
  }

  const parsedTraffic = parseNumberOrNull(form.traffic);
  if (parsedTraffic === null || parsedTraffic < 0) {
    errors.traffic = ['Traffic must be 0 or greater.'];
  } else if (!Number.isInteger(parsedTraffic)) {
    errors.traffic = ['Traffic must be a whole number.'];
  }

  if (!form.location.trim()) {
    errors.location = ['Location is required.'];
  }

  validatePricingRows(form, errors);

  const parsedNumberDFLinks = parseNumberOrNull(form.numberDFLinks);
  if (form.numberDFLinks.trim() !== '') {
    if (parsedNumberDFLinks === null || parsedNumberDFLinks <= 0) {
      errors.numberDFLinks = ['Number DF Links must be greater than 0.'];
    } else if (!Number.isInteger(parsedNumberDFLinks)) {
      errors.numberDFLinks = ['Number DF Links must be a whole number.'];
    }
  }

  return errors;
}

export function pricingAmountErrorKey(rowId: string): string {
  return `pricing.rows.${rowId}.amountUsd`;
}

export function pricingTermErrorKey(rowId: string): string {
  return `pricing.rows.${rowId}.termKey`;
}

export function pricingStatusErrorKey(priceType: PriceTypeValue): string {
  return `pricing.status.${priceType}`;
}

export function buildUpdateSitePayload(form: EditSiteFormState): UpdateSitePayload {
  const prices = form.pricingRows.map((row) => {
    const term = buildTermPayloadFromKey(row.termKey);
    return {
      priceType: row.priceType,
      ...term,
      amountUsd: parseNumberOrNull(row.amountUsd)!,
    };
  });

  const serviceAvailabilities = OPTIONAL_PRICE_TYPES.map((priceType) => ({
    serviceType: priceType,
    status: getPriceRowsForType(form, priceType).length > 0
      ? SERVICE_AVAILABILITY_STATUS.Available
      : form.pricingStatuses[priceType] ?? SERVICE_AVAILABILITY_STATUS.Unknown,
  }));

  return {
    dr: parseNumberOrNull(form.dr)!,
    traffic: parseNumberOrNull(form.traffic)!,
    location: form.location.trim(),
    language: normalizeLanguageCode(form.language),
    pricing: {
      prices,
      serviceAvailabilities,
    },
    numberDFLinks: parseNumberOrNull(form.numberDFLinks),
    termType: null,
    termValue: null,
    termUnit: null,
    niche: form.niche.trim() || null,
    categories: form.categories.trim() || null,
    SponsoredTag: form.sponsoredTag.trim() || null,
    isQuarantined: form.isQuarantined,
    quarantineReason: form.isQuarantined ? (form.quarantineReason.trim() || null) : null,
  };
}
