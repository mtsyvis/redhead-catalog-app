import type {
  PriceType,
  ServiceAvailabilityFilter,
  ServiceAvailabilityStatus,
  Site,
  SitePriceOptionDto,
  TermTypeValue,
  TermUnitValue,
  TermFilterOptionDto,
} from '../types/sites.types';
import {
  SERVICE_AVAILABILITY_STATUS,
  normalizeServiceAvailabilityFilter,
  normalizeServiceAvailabilityStatus,
} from './serviceAvailability';
import { TERM_TYPE, TERM_UNIT, normalizeTermType, normalizeTermUnit } from './term';

export const PRICE_TYPE = {
  Main: 0,
  Casino: 1,
  Crypto: 2,
  LinkInsertion: 3,
  LinkInsertionCasino: 4,
  Dating: 5,
} as const;

export type PriceTypeValue = (typeof PRICE_TYPE)[keyof typeof PRICE_TYPE];

export const ANY_TERM_KEY = '';

export const OPTIONAL_PRICE_TYPES = [
  PRICE_TYPE.Casino,
  PRICE_TYPE.Crypto,
  PRICE_TYPE.LinkInsertion,
  PRICE_TYPE.LinkInsertionCasino,
  PRICE_TYPE.Dating,
] as const;

export const PRICING_SECTION_ORDER = [
  PRICE_TYPE.Main,
  ...OPTIONAL_PRICE_TYPES,
] as const;

export const PRICE_TYPE_LABELS: Record<PriceTypeValue, string> = {
  [PRICE_TYPE.Main]: 'Main Price',
  [PRICE_TYPE.Casino]: 'Casino',
  [PRICE_TYPE.Crypto]: 'Crypto',
  [PRICE_TYPE.LinkInsertion]: 'Link Insert',
  [PRICE_TYPE.LinkInsertionCasino]: 'Link Insert Casino',
  [PRICE_TYPE.Dating]: 'Dating',
};

export const TERM_KEY_OPTIONS = [
  { termKey: 'unknown', label: 'Unknown term' },
  { termKey: 'finite:1:year', label: '1 year' },
  { termKey: 'finite:2:year', label: '2 years' },
  { termKey: 'finite:3:year', label: '3 years' },
  { termKey: 'permanent', label: 'Permanent' },
] as const;

export const PRICE_FIELD_TO_TYPE: Record<string, PriceTypeValue> = {
  priceUsd: PRICE_TYPE.Main,
  priceCasino: PRICE_TYPE.Casino,
  priceCrypto: PRICE_TYPE.Crypto,
  priceLinkInsert: PRICE_TYPE.LinkInsertion,
  priceLinkInsertCasino: PRICE_TYPE.LinkInsertionCasino,
  priceDating: PRICE_TYPE.Dating,
};

export const OPTIONAL_SERVICE_FIELDS = new Set([
  'priceCasino',
  'priceCrypto',
  'priceLinkInsert',
  'priceLinkInsertCasino',
  'priceDating',
]);

const LEGACY_PRICE_FIELDS: Partial<Record<PriceTypeValue, keyof Site>> = {
  [PRICE_TYPE.Main]: 'priceUsd',
  [PRICE_TYPE.Casino]: 'priceCasino',
  [PRICE_TYPE.Crypto]: 'priceCrypto',
  [PRICE_TYPE.LinkInsertion]: 'priceLinkInsert',
  [PRICE_TYPE.LinkInsertionCasino]: 'priceLinkInsertCasino',
  [PRICE_TYPE.Dating]: 'priceDating',
};

const LEGACY_STATUS_FIELDS: Partial<Record<PriceTypeValue, keyof Site>> = {
  [PRICE_TYPE.Casino]: 'priceCasinoStatus',
  [PRICE_TYPE.Crypto]: 'priceCryptoStatus',
  [PRICE_TYPE.LinkInsertion]: 'priceLinkInsertStatus',
  [PRICE_TYPE.LinkInsertionCasino]: 'priceLinkInsertCasinoStatus',
  [PRICE_TYPE.Dating]: 'priceDatingStatus',
};

export function normalizePriceType(priceType: PriceType | null | undefined): PriceTypeValue | null {
  if (priceType === PRICE_TYPE.Main || priceType === 'Main') return PRICE_TYPE.Main;
  if (priceType === PRICE_TYPE.Casino || priceType === 'Casino') return PRICE_TYPE.Casino;
  if (priceType === PRICE_TYPE.Crypto || priceType === 'Crypto') return PRICE_TYPE.Crypto;
  if (priceType === PRICE_TYPE.LinkInsertion || priceType === 'LinkInsertion') {
    return PRICE_TYPE.LinkInsertion;
  }
  if (priceType === PRICE_TYPE.LinkInsertionCasino || priceType === 'LinkInsertionCasino') {
    return PRICE_TYPE.LinkInsertionCasino;
  }
  if (priceType === PRICE_TYPE.Dating || priceType === 'Dating') return PRICE_TYPE.Dating;
  return null;
}

export function normalizeSelectedTermKey(termKey: string | null | undefined): string | null {
  const trimmed = termKey?.trim();
  return trimmed ? trimmed : null;
}

export function formatTermFilterLabel(termKey: string | null | undefined): string {
  const normalized = normalizeSelectedTermKey(termKey);
  if (!normalized) return 'Any term';
  if (normalized === 'unknown') return 'Unknown term';
  if (normalized === 'permanent') return 'Permanent';

  const finiteMatch = /^finite:(\d+):year$/i.exec(normalized);
  if (finiteMatch) {
    const years = Number(finiteMatch[1]);
    return years === 1 ? '1 year' : `${years} years`;
  }

  return normalized;
}

export function createTermFilterOptions(
  terms: TermFilterOptionDto[] | null | undefined
): TermFilterOptionDto[] {
  return [
    { termKey: ANY_TERM_KEY, label: 'Any term' },
    ...(terms ?? []).filter((term) => term.termKey.trim() !== ''),
  ];
}

function hasTermAwarePricing(site: Site): boolean {
  return site.pricing != null;
}

export function getPrices(site: Site, priceType: PriceTypeValue): SitePriceOptionDto[] {
  if (hasTermAwarePricing(site)) {
    return (site.pricing?.prices ?? [])
      .filter((price) => normalizePriceType(price.priceType) === priceType && price.amountUsd > 0)
      .sort(comparePriceTerms);
  }

  // LEGACY_PRICING: tolerate older API responses during rollout when pricing is absent.
  const legacyField = LEGACY_PRICE_FIELDS[priceType];
  const legacyAmount = legacyField ? site[legacyField] : null;
  return typeof legacyAmount === 'number' && legacyAmount > 0
    ? [
        {
          priceType,
          termKey: 'unknown',
          termLabel: 'Unknown term',
          amountUsd: legacyAmount,
        },
      ]
    : [];
}

export function getServiceStatus(site: Site, serviceType: PriceTypeValue): ServiceAvailabilityStatus | null {
  const availability = site.pricing?.serviceAvailabilities.find(
    (item) => normalizePriceType(item.serviceType) === serviceType
  );
  if (availability) return availability.status;

  if (hasTermAwarePricing(site)) return null;

  // LEGACY_PRICING: tolerate older API responses during rollout when pricing is absent.
  const legacyField = LEGACY_STATUS_FIELDS[serviceType];
  return legacyField ? (site[legacyField] as ServiceAvailabilityStatus) : null;
}

export function getMatchingPrices(
  site: Site,
  priceType: PriceTypeValue,
  selectedTermKey: string | null | undefined
): SitePriceOptionDto[] {
  const normalizedTermKey = normalizeSelectedTermKey(selectedTermKey);
  const prices = getPrices(site, priceType);
  return normalizedTermKey
    ? prices.filter((price) => price.termKey === normalizedTermKey)
    : prices;
}

export function getLowestPriceAmount(
  site: Site,
  priceType: PriceTypeValue,
  selectedTermKey: string | null | undefined
): number | null {
  const amounts = getMatchingPrices(site, priceType, selectedTermKey).map(
    (price) => price.amountUsd
  );
  return amounts.length === 0 ? null : Math.min(...amounts);
}

export function hasAnyPriceForTerm(site: Site, selectedTermKey: string | null | undefined): boolean {
  const normalizedTermKey = normalizeSelectedTermKey(selectedTermKey);
  if (!normalizedTermKey) return true;
  return (site.pricing?.prices ?? []).some(
    (price) => price.amountUsd > 0 && price.termKey === normalizedTermKey
  );
}

export function matchesMainPriceRange(site: Site, filters: { priceMin: string; priceMax: string; termKey: string | null }): boolean {
  if (filters.priceMin === '' && filters.priceMax === '') return true;

  const min = filters.priceMin === '' ? null : Number(filters.priceMin);
  const max = filters.priceMax === '' ? null : Number(filters.priceMax);
  const prices = getMatchingPrices(site, PRICE_TYPE.Main, filters.termKey);

  return prices.some(
    (price) =>
      (min == null || price.amountUsd >= min) &&
      (max == null || price.amountUsd <= max)
  );
}

export function matchesOptionalServiceFilter(
  site: Site,
  serviceType: PriceTypeValue,
  filter: ServiceAvailabilityFilter,
  selectedTermKey: string | null | undefined
): boolean {
  const normalizedFilter = normalizeServiceAvailabilityFilter(filter);
  if (normalizedFilter.length === 0) return true;

  const hasMatchingPrice = getMatchingPrices(site, serviceType, selectedTermKey).length > 0;
  const hasAnyServicePrice = getPrices(site, serviceType).length > 0;
  const status = normalizeServiceAvailabilityStatus(getServiceStatus(site, serviceType));

  return normalizedFilter.some((value) => {
    if (value === 'available') return hasMatchingPrice;
    if (value === 'availableWithUnknownPrice') {
      return status === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice;
    }
    if (value === 'notAvailable') {
      return status === SERVICE_AVAILABILITY_STATUS.NotAvailable;
    }
    return !hasAnyServicePrice && status === SERVICE_AVAILABILITY_STATUS.Unknown;
  });
}

export function getPriceSortAmount(
  site: Site,
  priceType: PriceTypeValue,
  selectedTermKey: string | null | undefined
): number | null {
  return getLowestPriceAmount(site, priceType, selectedTermKey);
}

export function getServiceSortRank(site: Site, serviceType: PriceTypeValue): number {
  const status = normalizeServiceAvailabilityStatus(getServiceStatus(site, serviceType));
  if (status === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice) return 1;
  if (status === SERVICE_AVAILABILITY_STATUS.NotAvailable) return 2;
  return 3;
}

export function formatMainPriceCell(site: Site): { primary: string; secondary: string | null; title: string } {
  const prices = getPrices(site, PRICE_TYPE.Main);
  return formatPriceSummary(prices, '—');
}

export function formatOptionalServicePriceCell(
  site: Site,
  serviceType: PriceTypeValue
): { primary: string; secondary: string | null; title: string } {
  const prices = getPrices(site, serviceType);
  if (prices.length > 0) {
    return formatPriceSummary(prices, '—');
  }

  const normalized = normalizeServiceAvailabilityStatus(getServiceStatus(site, serviceType));
  if (normalized === SERVICE_AVAILABILITY_STATUS.NotAvailable) {
    return { primary: 'NO', secondary: null, title: 'NO' };
  }
  if (normalized === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice) {
    return { primary: 'YES', secondary: 'Price unknown', title: 'YES - Price unknown' };
  }
  return { primary: '—', secondary: null, title: '—' };
}

function formatPriceSummary(
  prices: SitePriceOptionDto[],
  emptyValue: string
): { primary: string; secondary: string | null; title: string } {
  if (prices.length === 0) {
    return { primary: emptyValue, secondary: null, title: emptyValue };
  }

  const sortedPrices = [...prices].sort(comparePriceTerms);
  if (sortedPrices.length === 1) {
    const price = sortedPrices[0];
    const amount = formatUsd(price.amountUsd);
    const termLabel = price.termLabel || formatTermFilterLabel(price.termKey);
    return { primary: amount, secondary: termLabel, title: `${termLabel} ${amount}` };
  }

  const lowest = Math.min(...sortedPrices.map((price) => price.amountUsd));
  const visible = sortedPrices.slice(0, 3).map((price) => {
    const termLabel = getCompactTermLabel(price);
    return `${termLabel} ${formatUsd(price.amountUsd)}`;
  });
  const hiddenCount = sortedPrices.length - visible.length;
  const secondary = hiddenCount > 0 ? `${visible.join(' · ')} · +${hiddenCount}` : visible.join(' · ');
  const title = sortedPrices
    .map((price) => `${price.termLabel || formatTermFilterLabel(price.termKey)} ${formatUsd(price.amountUsd)}`)
    .join(' · ');

  return { primary: `From ${formatUsd(lowest)}`, secondary, title };
}

export function getPriceTypeLabel(priceType: PriceTypeValue): string {
  return PRICE_TYPE_LABELS[priceType] ?? String(priceType);
}

export function getCompactTermLabel(price: SitePriceOptionDto): string {
  const termKey = price.termKey;
  if (termKey === 'unknown') return 'Unknown';
  if (termKey === 'permanent') return 'Perm';
  if (
    normalizeTermType(price.termType) === TERM_TYPE.Finite &&
    normalizeTermUnit(price.termUnit) === TERM_UNIT.Year &&
    price.termValue != null &&
    price.termValue > 0
  ) {
    return `${price.termValue}y`;
  }

  const finiteMatch = /^finite:(\d+):year$/i.exec(termKey);
  if (finiteMatch) return `${finiteMatch[1]}y`;

  return price.termLabel || formatTermFilterLabel(termKey);
}

export function getFullTermLabel(price: Pick<SitePriceOptionDto, 'termKey' | 'termLabel'>): string {
  return price.termLabel || formatTermFilterLabel(price.termKey);
}

export function comparePriceTerms(left: SitePriceOptionDto, right: SitePriceOptionDto): number {
  const leftRank = getTermSortRank(left);
  const rightRank = getTermSortRank(right);
  if (leftRank.group !== rightRank.group) return leftRank.group - rightRank.group;
  if (leftRank.value !== rightRank.value) return leftRank.value - rightRank.value;
  return left.termKey.localeCompare(right.termKey);
}

function getTermSortRank(price: SitePriceOptionDto): { group: number; value: number } {
  if (price.termKey === 'unknown') return { group: 0, value: 0 };
  if (price.termKey === 'permanent') return { group: 2, value: 0 };

  if (
    normalizeTermType(price.termType) === TERM_TYPE.Finite &&
    normalizeTermUnit(price.termUnit) === TERM_UNIT.Year &&
    price.termValue != null
  ) {
    return { group: 1, value: price.termValue };
  }

  const finiteMatch = /^finite:(\d+):year$/i.exec(price.termKey);
  return finiteMatch ? { group: 1, value: Number(finiteMatch[1]) } : { group: 1, value: Number.MAX_SAFE_INTEGER };
}

export function buildTermPayloadFromKey(termKey: string): {
  termKey: string;
  termType: TermTypeValue | null;
  termValue: number | null;
  termUnit: TermUnitValue | null;
} {
  if (termKey === 'permanent') {
    return {
      termKey,
      termType: TERM_TYPE.Permanent,
      termValue: null,
      termUnit: null,
    };
  }

  const finiteMatch = /^finite:(\d+):year$/i.exec(termKey);
  if (finiteMatch) {
    return {
      termKey: `finite:${Number(finiteMatch[1])}:year`,
      termType: TERM_TYPE.Finite,
      termValue: Number(finiteMatch[1]),
      termUnit: TERM_UNIT.Year,
    };
  }

  return {
    termKey: 'unknown',
    termType: null,
    termValue: null,
    termUnit: null,
  };
}

export function formatUsd(amount: number): string {
  return `$${new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(amount)}`;
}
