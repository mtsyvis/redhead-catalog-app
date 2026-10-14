import { API_BASE_URL, ApiClient } from './api.client';
import { notifySessionExpired } from './sessionExpired';
import type { CatalogCoverageAnalytics } from '../types/catalogCoverageAnalytics.types';

const endpoint = '/api/admin/analytics/catalog-coverage';

export const catalogCoverageAnalyticsService = {
  get(): Promise<CatalogCoverageAnalytics> {
    return ApiClient.get<CatalogCoverageAnalytics>(endpoint);
  },

  async downloadDomainsWithoutOffers(): Promise<void> {
    const response = await fetch(`${API_BASE_URL}${endpoint}/domains-without-offers.xlsx`, {
      credentials: 'include',
      cache: 'no-store',
    });
    if (!response.ok) {
      if (response.status === 401) notifySessionExpired();
      const error = await response.json().catch(() => null);
      throw new Error(error?.message || 'Failed to download domains without offers');
    }

    const blob = await response.blob();
    const downloadUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = downloadUrl;
    const serverFilename = response.headers.get('Content-Disposition')?.match(/filename="?([^";]+)"?/i)?.[1];
    link.download = serverFilename || 'catalog-domains-without-offers.xlsx';
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(downloadUrl);
  },
};
