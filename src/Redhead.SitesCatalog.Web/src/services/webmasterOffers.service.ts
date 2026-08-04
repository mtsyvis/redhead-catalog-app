import { apiClient } from './api.client';
import type { WebmasterOffersSearchResult } from '../types/webmasterOffers.types';

export const webmasterOffersService = {
  getByDomain(domain: string): Promise<WebmasterOffersSearchResult> {
    return apiClient.get<WebmasterOffersSearchResult>(
      `/api/webmaster-offers?domain=${encodeURIComponent(domain)}`
    );
  },
};
