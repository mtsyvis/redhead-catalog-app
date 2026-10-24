import dayjs from 'dayjs';
import { apiClient, API_BASE_URL } from './api.client';
import { notifySessionExpired } from './sessionExpired';
import type { EntityChangeHistoryItem } from '../types/changeHistory.types';
import type {
  UpdateWebmasterOfferPayload,
  LinkDetectivePrices,
  WebmasterOffer,
  WebmasterOfferEdit,
  WebmasterOffersSearchResult,
  WebmasterOffersSummarySearchResult,
  WebmasterOffersMultiSearchResult,
} from '../types/webmasterOffers.types';

export const webmasterOffersService = {
  getLinkDetectivePrices(domain: string, forceRefresh = false): Promise<LinkDetectivePrices> {
    return apiClient.post('/api/webmaster-offers/linkdetective-prices', { domain, forceRefresh });
  },
  multiSearch(queryText: string): Promise<WebmasterOffersMultiSearchResult> {
    return apiClient.post('/api/webmaster-offers/multi-search', { queryText });
  },
  async exportMultiSearch(queryText: string, signal?: AbortSignal): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/api/webmaster-offers/export.xlsx`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ queryText }),
      signal,
    });
    if (!response.ok) {
      const error = await response.json().catch(() => null);
      signal?.throwIfAborted();
      if (response.status === 401) notifySessionExpired();
      throw new Error(error?.message || 'Failed to export webmaster offers');
    }

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
