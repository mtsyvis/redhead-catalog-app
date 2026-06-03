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
  GoogleDriveExportPayload,
  GoogleDriveExportResponse,
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
  async exportSites(payload: ExportSitesPayload): Promise<ExportMetadata> {
    const response = await fetch('/api/export/sites.xlsx', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      throw new Error(await readExportErrorMessage(response, 'Export failed'));
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
   * Domain/default sorting preserves normalized input order.
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
      throw new Error(await readExportErrorMessage(response, 'Export failed'));
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
   * Export sites as Excel and save the workbook to the connected user's Google Drive folder.
   */
  async exportSitesToGoogleDrive(
    payload: GoogleDriveExportPayload
  ): Promise<GoogleDriveExportResponse> {
    return apiClient.post<GoogleDriveExportResponse, GoogleDriveExportPayload>(
      `${this.baseUrl}/export/google-drive`,
      payload
    );
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

async function readExportErrorMessage(response: Response, fallback: string): Promise<string> {
  const error = (await response.json().catch(() => null)) as {
    error?: string;
    message?: string;
    detail?: string;
    title?: string;
    errors?: string[] | Record<string, string[]>;
  } | null;

  if (!error) {
    return response.statusText || fallback;
  }

  if (Array.isArray(error.errors) && error.errors.length > 0) {
    return error.errors.join(' ');
  }

  if (error.errors && typeof error.errors === 'object') {
    const messages = Object.values(error.errors).flat();
    if (messages.length > 0) {
      return messages.join(' ');
    }
  }

  return error.error || error.message || error.detail || error.title || response.statusText || fallback;
}
