import { apiClient } from './api.client';
import type {
  Site,
  SitesListResponse,
  SitesQueryParams,
  LocationsResponse,
  MultiSearchResponse,
  ExportMultiSearchPayload,
  UpdateSitePayload,
} from '../types/sites.types';

export interface ExportMetadata {
  requestedRows: number;
  exportedRows: number;
  truncated: boolean;
  limitRows?: number;
}

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

    // Availability filters
    if (params.casinoAvailability) {
      queryParams.append('casinoAvailability', params.casinoAvailability);
    }
    if (params.cryptoAvailability) {
      queryParams.append('cryptoAvailability', params.cryptoAvailability);
    }
    if (params.linkInsertAvailability) {
      queryParams.append('linkInsertAvailability', params.linkInsertAvailability);
    }
    if (params.linkInsertCasinoAvailability) {
      queryParams.append('linkInsertCasinoAvailability', params.linkInsertCasinoAvailability);
    }
    if (params.datingAvailability) {
      queryParams.append('datingAvailability', params.datingAvailability);
    }

    // Quarantine filter
    if (params.quarantine) {
      queryParams.append('quarantine', params.quarantine);
    }

    // Last published date range (yyyy-MM)
    if (params.lastPublishedFromMonth) {
      queryParams.append('lastPublishedFromMonth', params.lastPublishedFromMonth);
    }
    if (params.lastPublishedToMonth) {
      queryParams.append('lastPublishedToMonth', params.lastPublishedToMonth);
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
   * Update site fields (Admin/SuperAdmin). Includes all editable fields + quarantine.
   */
  async updateSite(domain: string, payload: UpdateSitePayload): Promise<Site> {
    return apiClient.put<Site, UpdateSitePayload>(
      `${this.baseUrl}/${encodeURIComponent(domain)}`,
      payload
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
   * Export sites as CSV with current filters. Returns metadata from response headers.
   */
  async exportSites(params: SitesQueryParams): Promise<ExportMetadata> {
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

    // Availability filters
    if (params.casinoAvailability) {
      queryParams.append('casinoAvailability', params.casinoAvailability);
    }
    if (params.cryptoAvailability) {
      queryParams.append('cryptoAvailability', params.cryptoAvailability);
    }
    if (params.linkInsertAvailability) {
      queryParams.append('linkInsertAvailability', params.linkInsertAvailability);
    }
    if (params.linkInsertCasinoAvailability) {
      queryParams.append('linkInsertCasinoAvailability', params.linkInsertCasinoAvailability);
    }
    if (params.datingAvailability) {
      queryParams.append('datingAvailability', params.datingAvailability);
    }

    // Quarantine filter
    if (params.quarantine) {
      queryParams.append('quarantine', params.quarantine);
    }

    // Last published date range (yyyy-MM)
    if (params.lastPublishedFromMonth) {
      queryParams.append('lastPublishedFromMonth', params.lastPublishedFromMonth);
    }
    if (params.lastPublishedToMonth) {
      queryParams.append('lastPublishedToMonth', params.lastPublishedToMonth);
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

    const metadata = readExportMetadata(response.headers);

    const blob = await response.blob();
    const downloadUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = downloadUrl;
    link.download = 'sites.csv';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(downloadUrl);

    return metadata;
  }

  /**
   * Export multi-search result as CSV (filtered Found + all Not found). Uses POST.
   * Returns metadata from response headers.
   */
  async exportSitesMultiSearch(payload: ExportMultiSearchPayload): Promise<ExportMetadata> {
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

    const metadata = readExportMetadata(response.headers);

    const blob = await response.blob();
    const downloadUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = downloadUrl;
    link.download = 'sites.csv';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(downloadUrl);

    return metadata;
  }
}

export const sitesService = new SitesService();

function readExportMetadata(headers: Headers): ExportMetadata {
  return {
    requestedRows: parseInt(headers.get('X-Export-Requested-Rows') ?? '0', 10),
    exportedRows: parseInt(headers.get('X-Export-Exported-Rows') ?? '0', 10),
    truncated: headers.get('X-Export-Truncated') === 'true',
    limitRows: headers.has('X-Export-Limit-Rows')
      ? parseInt(headers.get('X-Export-Limit-Rows')!, 10)
      : undefined,
  };
}
