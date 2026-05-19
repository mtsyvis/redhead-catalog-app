import { apiClient } from './api.client';
import type {
  Site,
  SitesListResponse,
  SitesQueryParams,
  LocationsResponse,
  FilterOptionsResponse,
  MultiSearchResponse,
  ExportMultiSearchPayload,
  ExportSitesPayload,
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
    return apiClient.post<SitesListResponse, SitesQueryParams>(`${this.baseUrl}/search`, params);
  }

  /**
   * Multi-search by domains/URLs (exact match, max 500).
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
   * Fetch filter option values for advanced filters.
   */
  async getFilterOptions(): Promise<FilterOptionsResponse> {
    return apiClient.get<FilterOptionsResponse>(`${this.baseUrl}/filter-options`);
  }

  /**
   * Export sites as Excel with current filters. Returns metadata from response headers.
   */
  async exportSites(params: SitesQueryParams): Promise<ExportMetadata> {
    const response = await fetch('/api/export/sites.xlsx', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ filters: params } satisfies ExportSitesPayload),
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
    link.download = 'sites.xlsx';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(downloadUrl);

    return metadata;
  }

  /**
   * Export multi-search result as Excel (filtered Found + Not found sheet when applicable). Uses POST.
   * Returns metadata from response headers.
   */
  async exportSitesMultiSearch(payload: ExportMultiSearchPayload): Promise<ExportMetadata> {
    const baseUrl = import.meta.env.DEV ? '' : (import.meta.env.VITE_API_URL || '');
    const response = await fetch(`${baseUrl}/api/export/sites-multi-search.xlsx`, {
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
    link.download = 'sites.xlsx';
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
