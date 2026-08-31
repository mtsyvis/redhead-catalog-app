import { apiClient } from './api.client';
import type { WebmasterSearchResult, WebmasterWorkspace } from '../types/webmasters.types';

export const webmastersService = {
  search(contact: string, page: number, pageSize: number): Promise<WebmasterSearchResult> {
    const params = new URLSearchParams({
      contact,
      page: String(page),
      pageSize: String(pageSize),
    });
    return apiClient.get<WebmasterSearchResult>(`/api/webmasters/search?${params.toString()}`);
  },

  getWorkspace(webmasterId: string): Promise<WebmasterWorkspace> {
    return apiClient.get<WebmasterWorkspace>(`/api/webmasters/${webmasterId}/workspace`);
  },
};
