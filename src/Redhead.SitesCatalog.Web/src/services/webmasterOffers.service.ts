import { apiClient } from './api.client';
import type { EntityChangeHistoryItem } from '../types/changeHistory.types';
import type {
  UpdateWebmasterOfferPayload,
  WebmasterOffer,
  WebmasterOfferEdit,
  WebmasterOffersSearchResult,
} from '../types/webmasterOffers.types';

export const webmasterOffersService = {
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
