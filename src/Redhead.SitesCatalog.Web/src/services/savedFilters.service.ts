import { apiClient } from './api.client';
import type {
  CreateSavedFilterSetPayload,
  SavedFilterSet,
  SavedFilterSetsResponse,
  UpdateSavedFilterSetPayload,
} from '../types/savedFilters.types';

class SavedFiltersService {
  private readonly baseUrl = '/api/me/saved-filter-sets';

  async getFilterSets(tableKey: string): Promise<SavedFilterSetsResponse> {
    return apiClient.get<SavedFilterSetsResponse>(`${this.baseUrl}/${tableKey}`);
  }

  async createFilterSet(
    tableKey: string,
    payload: CreateSavedFilterSetPayload
  ): Promise<SavedFilterSet> {
    return apiClient.post<SavedFilterSet, CreateSavedFilterSetPayload>(
      `${this.baseUrl}/${tableKey}`,
      payload
    );
  }

  async updateFilterSet(
    tableKey: string,
    id: string,
    payload: UpdateSavedFilterSetPayload
  ): Promise<SavedFilterSet> {
    return apiClient.put<SavedFilterSet, UpdateSavedFilterSetPayload>(
      `${this.baseUrl}/${tableKey}/${encodeURIComponent(id)}`,
      payload
    );
  }

  async deleteFilterSet(tableKey: string, id: string): Promise<void> {
    await apiClient.delete<Record<string, never>>(
      `${this.baseUrl}/${tableKey}/${encodeURIComponent(id)}`
    );
  }
}

export const savedFiltersService = new SavedFiltersService();
