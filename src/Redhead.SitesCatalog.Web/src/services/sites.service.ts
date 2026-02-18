import { apiClient } from './api.client';
import type {
  SitesListResponse,
  SitesQueryParams,
  LocationsResponse,
  MultiSearchResponse,
  ExportMultiSearchPayload,
} from '../types/sites.types';

/**
 * Service for sites-related API calls
 */
class SitesService {
  private readonly baseUrl = '/api/sites';

  /**
   * Fetch sites with filters, sorting, and pagination
   */
  async getSites(params: SitesQueryParams): Promise<SitesListResponse> {
    const queryParams = new URLSearchParams();

    // Pagination
    queryParams.append('page', params.page.toString());
    queryParams.append('pageSize', params.pageSize.toString());

    // Sorting
    if (params.sortBy) {
      queryParams.append('sortBy', params.sortBy);
    }
    if (params.sortDir) {
      queryParams.append('sortDir', params.sortDir);
    }

    // Search
    if (params.search) {
      queryParams.append('search', params.search);
    }

    // Range filters
    if (params.drMin !== undefined) {
      queryParams.append('drMin', params.drMin.toString());
    }
    if (params.drMax !== undefined) {
      queryParams.append('drMax', params.drMax.toString());
    }
    if (params.trafficMin !== undefined) {
      queryParams.append('trafficMin', params.trafficMin.toString());
    }
    if (params.trafficMax !== undefined) {
      queryParams.append('trafficMax', params.trafficMax.toString());
    }
    if (params.priceMin !== undefined) {
      queryParams.append('priceMin', params.priceMin.toString());
    }
    if (params.priceMax !== undefined) {
      queryParams.append('priceMax', params.priceMax.toString());
    }

    // Location multi-select
    if (params.location && params.location.length > 0) {
      params.location.forEach(loc => queryParams.append('locations', loc));
    }

    // Allowed flags
    if (params.casinoAllowed !== undefined) {
      queryParams.append('casinoAllowed', params.casinoAllowed.toString());
    }
    if (params.cryptoAllowed !== undefined) {
      queryParams.append('cryptoAllowed', params.cryptoAllowed.toString());
    }
    if (params.linkInsertAllowed !== undefined) {
      queryParams.append('linkInsertAllowed', params.linkInsertAllowed.toString());
    }

    // Quarantine filter
    if (params.quarantine) {
      queryParams.append('quarantine', params.quarantine);
    }

    return apiClient.get<SitesListResponse>(`${this.baseUrl}?${queryParams.toString()}`);
  }

  /**
   * Multi-search by domains/URLs (exact match, max 500). Uses same search box input as queryText.
   */
  async multiSearch(queryText: string): Promise<MultiSearchResponse> {
    return apiClient.post<MultiSearchResponse, { queryText: string }>(
      `${this.baseUrl}/multi-search`,
      { queryText }
    );
  }

  /**
   * Fetch distinct location values for filter dropdown
   */
  async getLocations(): Promise<string[]> {
    const response = await apiClient.get<LocationsResponse>(`${this.baseUrl}/locations`);
    return response.locations;
  }

  /**
   * Export sites as CSV with current filters
   */
  async exportSites(params: SitesQueryParams): Promise<void> {
    const queryParams = new URLSearchParams();

    // Pagination (not used for export, but kept for consistency)
    queryParams.append('page', params.page.toString());
    queryParams.append('pageSize', params.pageSize.toString());

    // Sorting
    if (params.sortBy) {
      queryParams.append('sortBy', params.sortBy);
    }
    if (params.sortDir) {
      queryParams.append('sortDir', params.sortDir);
    }

    // Search
    if (params.search) {
      queryParams.append('search', params.search);
    }

    // Range filters
    if (params.drMin !== undefined) {
      queryParams.append('drMin', params.drMin.toString());
    }
    if (params.drMax !== undefined) {
      queryParams.append('drMax', params.drMax.toString());
    }
    if (params.trafficMin !== undefined) {
      queryParams.append('trafficMin', params.trafficMin.toString());
    }
    if (params.trafficMax !== undefined) {
      queryParams.append('trafficMax', params.trafficMax.toString());
    }
    if (params.priceMin !== undefined) {
      queryParams.append('priceMin', params.priceMin.toString());
    }
    if (params.priceMax !== undefined) {
      queryParams.append('priceMax', params.priceMax.toString());
    }

    // Location multi-select
    if (params.location && params.location.length > 0) {
      params.location.forEach(loc => queryParams.append('locations', loc));
    }

    // Allowed flags
    if (params.casinoAllowed !== undefined) {
      queryParams.append('casinoAllowed', params.casinoAllowed.toString());
    }
    if (params.cryptoAllowed !== undefined) {
      queryParams.append('cryptoAllowed', params.cryptoAllowed.toString());
    }
    if (params.linkInsertAllowed !== undefined) {
      queryParams.append('linkInsertAllowed', params.linkInsertAllowed.toString());
    }

    // Quarantine filter
    if (params.quarantine) {
      queryParams.append('quarantine', params.quarantine);
    }

    const url = `/api/export/sites.csv?${queryParams.toString()}`;
    const response = await fetch(url, {
      method: 'GET',
      credentials: 'include',
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: 'Export failed' }));
      throw new Error((error as { error?: string }).error || 'Export failed');
    }

    const blob = await response.blob();
    const downloadUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = downloadUrl;
    link.download = 'sites.csv';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(downloadUrl);
  }

  /**
   * Export multi-search result as CSV (filtered Found + all Not found). Uses POST.
   */
  async exportSitesMultiSearch(payload: ExportMultiSearchPayload): Promise<void> {
    const baseUrl = import.meta.env.DEV ? '' : (import.meta.env.VITE_API_URL || '');
    const response = await fetch(`${baseUrl}/api/export/sites-multi-search.csv`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Export failed' }));
      throw new Error((error as { detail?: string }).detail || 'Export failed');
    }

    const blob = await response.blob();
    const downloadUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = downloadUrl;
    link.download = 'sites.csv';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(downloadUrl);
  }
}

export const sitesService = new SitesService();
