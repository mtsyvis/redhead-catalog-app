import type { SitesFilters } from './sites.types';

export interface SavedFilterSettings extends Omit<SitesFilters, 'search' | 'stopListDomains'> {
  schemaVersion: 1;
  stopListDomains?: string[] | null;
}

export interface SavedFilterSet {
  id: string;
  name: string;
  schemaVersion: number;
  settings: SavedFilterSettings;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface SavedFilterSetsResponse {
  filterSets: SavedFilterSet[];
}

export interface CreateSavedFilterSetPayload {
  name: string;
  settings: SavedFilterSettings;
}

export interface UpdateSavedFilterSetPayload {
  name?: string;
  settings?: SavedFilterSettings;
}
