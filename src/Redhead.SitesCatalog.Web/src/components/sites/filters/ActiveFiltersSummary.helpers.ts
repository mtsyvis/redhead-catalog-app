import type { SitesFilters } from '../../../types/sites.types';
import {
  getServiceAvailabilityFilterLabel,
  normalizeServiceAvailabilityFilter,
} from '../../../utils/serviceAvailability';
import { pluralize } from '../../../utils/pluralize';

const FILTER_VALUE_SUMMARY_MAX_LENGTH = 22;
const FILTER_VALUE_SUMMARY_LIMIT = 5;

const QUARANTINE_FILTER_LABELS: Record<SitesFilters['quarantine'], string> = {
  all: 'All Sites',
  exclude: 'Available Only',
  only: 'Unavailable Only',
};

function formatLocationFilterValues(filters: SitesFilters): string[] {
  const excludedKeys = new Set(filters.excludedLocationKeys);

  return filters.locationSelections.map((selection) => {
    if (selection.kind !== 'group') return selection.displayName;

    const excludedCount = selection.locations?.filter((location) => excludedKeys.has(location.key))
      .length ?? 0;

    return excludedCount > 0
      ? `${selection.displayName} - ${excludedCount} excluded`
      : selection.displayName;
  });
}

export interface ActiveFilterSummary {
  label: string | null;
  value: string;
}

function formatRangeFilterSummary(
  label: string,
  min: string,
  max: string
): ActiveFilterSummary | null {
  if (min !== '' && max !== '') return { label, value: `${min}–${max}` };
  if (min !== '') return { label, value: `≥ ${min}` };
  if (max !== '') return { label, value: `≤ ${max}` };
  return null;
}

function truncateSummaryValue(value: string): string {
  if (value.length <= FILTER_VALUE_SUMMARY_MAX_LENGTH) return value;
  return `${value.slice(0, FILTER_VALUE_SUMMARY_MAX_LENGTH - 1)}…`;
}

function formatSelectionSummary(label: string, values: string[]): ActiveFilterSummary | null {
  if (values.length === 0) return null;
  const visibleValues = values
    .slice(0, FILTER_VALUE_SUMMARY_LIMIT)
    .map(truncateSummaryValue)
    .join(', ');
  const hiddenCount = values.length - FILTER_VALUE_SUMMARY_LIMIT;

  return {
    label,
    value: hiddenCount > 0 ? `${visibleValues} +${hiddenCount} more` : visibleValues,
  };
}

export function formatFilterGroupOverflow(count: number): ActiveFilterSummary {
  return { label: null, value: `+${count} ${pluralize(count, 'filter')}` };
}

export function buildAdvancedActiveFilterSummaries(
  filters: SitesFilters,
  multiSearchMode: boolean
): ActiveFilterSummary[] {
  const summaries: ActiveFilterSummary[] = [];
  const drSummary = formatRangeFilterSummary('DR', filters.drMin, filters.drMax);
  const trafficSummary = formatRangeFilterSummary(
    'Traffic',
    filters.trafficMin,
    filters.trafficMax
  );
  const priceSummary = formatRangeFilterSummary('Price', filters.priceMin, filters.priceMax);

  if (drSummary) summaries.push(drSummary);
  if (trafficSummary) summaries.push(trafficSummary);
  if (priceSummary) summaries.push(priceSummary);
  if (!multiSearchMode && filters.stopListDomains.length > 0) {
    summaries.push({
      label: 'Stop list',
      value: 'Applied',
    });
  }

  const locationSummary = formatSelectionSummary(
    'Location',
    formatLocationFilterValues(filters)
  );
  if (locationSummary) summaries.push(locationSummary);

  const nicheSummary = formatSelectionSummary('Niche', filters.niches);
  if (nicheSummary) summaries.push(nicheSummary);

  const categoriesSummary = formatSelectionSummary('Categories', filters.categorySearchTerms);
  if (categoriesSummary) summaries.push(categoriesSummary);

  const languageSummary = formatSelectionSummary('Language', filters.languages);
  if (languageSummary) summaries.push(languageSummary);

  const serviceFilters: Array<[string, SitesFilters['casinoAvailability']]> = [
    ['Casino', filters.casinoAvailability],
    ['Crypto', filters.cryptoAvailability],
    ['Link Insert', filters.linkInsertAvailability],
    ['Link Insert Casino', filters.linkInsertCasinoAvailability],
    ['Dating', filters.datingAvailability],
  ];

  for (const [label, values] of serviceFilters) {
    const serviceSummary = formatSelectionSummary(
      label,
      normalizeServiceAvailabilityFilter(values).map((value) =>
        getServiceAvailabilityFilterLabel(value)
      )
    );
    if (serviceSummary) summaries.push(serviceSummary);
  }

  if (filters.quarantine !== 'exclude') {
    summaries.push({
      label: 'Quarantine',
      value: QUARANTINE_FILTER_LABELS[filters.quarantine],
    });
  }

  const lastPublishedSummary = formatRangeFilterSummary(
    'Last Publication',
    filters.lastPublishedFromMonth ?? '',
    filters.lastPublishedToMonth ?? ''
  );
  if (lastPublishedSummary) summaries.push(lastPublishedSummary);

  return summaries;
}
