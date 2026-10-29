import dayjs from 'dayjs';
import { apiClient, ApiClientError, API_BASE_URL } from './api.client';
import type { GoogleDriveExportResponse } from '../types/sites.types';
import { notifySessionExpired } from './sessionExpired';
import type { EntityChangeHistoryItem } from '../types/changeHistory.types';
import type { WebmasterPriceComparison } from '../types/webmasterPriceComparison.types';
import type {
  UpdateWebmasterOfferPayload,
  LinkDetectivePrices,
  WebmasterOffer,
  WebmasterOfferEdit,
  WebmasterOffersSearchResult,
  WebmasterOffersSummarySearchResult,
  WebmasterOffersMultiSearchResult,
} from '../types/webmasterOffers.types';

export interface WebmasterOffersExportPayload {
  queryText: string;
  multiSearch: boolean;
  exportAll: boolean;
}

async function requestExport(payload: WebmasterOffersExportPayload, googleDrive: boolean, signal?: AbortSignal) {
  const response = await fetch(`${API_BASE_URL}/api/webmaster-offers/${googleDrive ? 'export/google-drive' : 'export.xlsx'}`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
    signal,
  });
  if (!response.ok) {
    const error = await response.json().catch(() => null);
    signal?.throwIfAborted();
    if (response.status === 401) notifySessionExpired();
    throw new ApiClientError(error?.message || 'Failed to export webmaster offers', response.status);
  }
  return response;
}

export const webmasterOffersService = {
  getPriceComparison(domain: string): Promise<WebmasterPriceComparison> {
    return apiClient.get(`/api/webmaster-offers/price-comparison?domain=${encodeURIComponent(domain)}`);
  },
  getLinkDetectivePrices(domain: string, forceRefresh = false): Promise<LinkDetectivePrices> {
    return apiClient.post('/api/webmaster-offers/linkdetective-prices', { domain, forceRefresh });
  },
  multiSearch(queryText: string): Promise<WebmasterOffersMultiSearchResult> {
    return apiClient.post('/api/webmaster-offers/multi-search', { queryText });
  },
  async exportWorkbook(payload: WebmasterOffersExportPayload, signal?: AbortSignal): Promise<void> {
    const response = await requestExport(payload, false, signal);
    const blob = await response.blob();
    signal?.throwIfAborted();
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `webmaster-offers-${dayjs().format('YYYY-MM-DD')}.xlsx`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  },
  async exportToGoogleDrive(payload: WebmasterOffersExportPayload, signal?: AbortSignal): Promise<GoogleDriveExportResponse> {
    const response = await requestExport(payload, true, signal);
    const result: GoogleDriveExportResponse = await response.json();
    signal?.throwIfAborted();
    return result;
  },
  getSummaryByDomain(domain: string): Promise<WebmasterOffersSummarySearchResult> {
    return apiClient.get<WebmasterOffersSummarySearchResult>(
      `/api/webmaster-offers?domain=${encodeURIComponent(domain)}`
    );
  },
  getByDomain(domain: string): Promise<WebmasterOffersSearchResult> {
    return apiClient.get<WebmasterOffersSearchResult>(
      `/api/webmaster-offers?domain=${encodeURIComponent(domain)}`
    );
  },
  getForEdit(offerId: string): Promise<WebmasterOfferEdit> {
    return apiClient.get<WebmasterOfferEdit>(`/api/webmaster-offers/${offerId}`);
  },
  update(offerId: string, payload: UpdateWebmasterOfferPayload): Promise<WebmasterOffer> {
    return apiClient.put<WebmasterOffer, UpdateWebmasterOfferPayload>(
      `/api/webmaster-offers/${offerId}`,
      payload
    );
  },
  getHistory(offerId: string): Promise<EntityChangeHistoryItem[]> {
    return apiClient.get<EntityChangeHistoryItem[]>(`/api/webmaster-offers/${offerId}/history`);
  },
};
