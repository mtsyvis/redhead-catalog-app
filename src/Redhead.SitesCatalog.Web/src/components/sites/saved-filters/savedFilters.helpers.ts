import type { SavedFilterSettings } from '../../../types/savedFilters.types';
import type {
  LocationFilterSelection,
  ServiceAvailabilityFilter,
  ServiceAvailabilityFilterValue,
  SitesFilters,
} from '../../../types/sites.types';

interface BuildSavedFilterSettingsOptions {
  includeStopListDomains: boolean;
}

interface ApplySavedFilterSettingsOptions {
  multiSearchMode: boolean;
}

export function buildSavedFilterSettings(
  filters: SitesFilters,
  options: BuildSavedFilterSettingsOptions
): SavedFilterSettings {
  return {
    schemaVersion: 1,
    stopListDomains: options.includeStopListDomains ? [...filters.stopListDomains] : null,
    drMin: filters.drMin,
    drMax: filters.drMax,
    trafficMin: filters.trafficMin,
    trafficMax: filters.trafficMax,
    priceMin: filters.priceMin,
    priceMax: filters.priceMax,
    termKey: filters.termKey,
    locationSelections: cloneLocationSelections(filters.locationSelections),
    excludedLocationKeys: [...filters.excludedLocationKeys],
    niches: [...filters.niches],
    categorySearchTerms: [...filters.categorySearchTerms],
    topicFitMode: filters.topicFitMode,
    excludedNiches: [...filters.excludedNiches],
    excludedCategorySearchTerms: [...filters.excludedCategorySearchTerms],
    languages: [...filters.languages],
    casinoAvailability: [...filters.casinoAvailability],
    cryptoAvailability: [...filters.cryptoAvailability],
    linkInsertAvailability: [...filters.linkInsertAvailability],
    linkInsertCasinoAvailability: [...filters.linkInsertCasinoAvailability],
    datingAvailability: [...filters.datingAvailability],
    quarantine: filters.quarantine,
    lastPublishedFromMonth: filters.lastPublishedFromMonth,
    lastPublishedToMonth: filters.lastPublishedToMonth,
  };
}

export function applySavedFilterSettings(
  currentFilters: SitesFilters,
  settings: SavedFilterSettings,
  options: ApplySavedFilterSettingsOptions
): SitesFilters {
  return {
    ...currentFilters,
    stopListDomains:
      !options.multiSearchMode
        ? [...(settings.stopListDomains ?? [])]
        : currentFilters.stopListDomains,
    drMin: normalizeString(settings.drMin),
    drMax: normalizeString(settings.drMax),
    trafficMin: normalizeString(settings.trafficMin),
    trafficMax: normalizeString(settings.trafficMax),
    priceMin: normalizeString(settings.priceMin),
    priceMax: normalizeString(settings.priceMax),
    termKey: normalizeNullableString(settings.termKey),
    locationSelections: cloneLocationSelections(settings.locationSelections),
    excludedLocationKeys: normalizeStringArray(settings.excludedLocationKeys),
    niches: normalizeStringArray(settings.niches),
    categorySearchTerms: normalizeStringArray(settings.categorySearchTerms),
    topicFitMode: settings.topicFitMode === 'narrow' ? 'narrow' : 'expand',
    excludedNiches: normalizeStringArray(settings.excludedNiches),
    excludedCategorySearchTerms: normalizeStringArray(settings.excludedCategorySearchTerms),
    languages: normalizeStringArray(settings.languages),
    casinoAvailability: normalizeAvailabilityFilter(settings.casinoAvailability),
    cryptoAvailability: normalizeAvailabilityFilter(settings.cryptoAvailability),
    linkInsertAvailability: normalizeAvailabilityFilter(settings.linkInsertAvailability),
    linkInsertCasinoAvailability: normalizeAvailabilityFilter(
      settings.linkInsertCasinoAvailability
    ),
    datingAvailability: normalizeAvailabilityFilter(settings.datingAvailability),
    quarantine:
      settings.quarantine === 'all' || settings.quarantine === 'only'
        ? settings.quarantine
        : 'exclude',
    lastPublishedFromMonth: settings.lastPublishedFromMonth ?? null,
    lastPublishedToMonth: settings.lastPublishedToMonth ?? null,
  };
}

export function areSavedFilterSettingsEqual(
  left: SavedFilterSettings,
  right: SavedFilterSettings
): boolean {
  return JSON.stringify(left) === JSON.stringify(right);
}

function normalizeString(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

function normalizeNullableString(value: unknown): string | null {
  return typeof value === 'string' && value.trim() !== '' ? value : null;
}

function normalizeStringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [];
}

function normalizeAvailabilityFilter(value: unknown): ServiceAvailabilityFilter {
  return normalizeStringArray(value).filter(
    (item): item is ServiceAvailabilityFilterValue =>
      item === 'unknown' ||
      item === 'available' ||
      item === 'notAvailable' ||
      item === 'availableWithUnknownPrice'
  );
}

function cloneLocationSelections(
  selections: LocationFilterSelection[] | undefined
): LocationFilterSelection[] {
  return Array.isArray(selections)
    ? selections.map((selection) => ({
        ...selection,
        ...(selection.kind === 'group' && selection.locations
          ? { locations: selection.locations.map((location) => ({ ...location })) }
          : {}),
      }))
    : [];
}
