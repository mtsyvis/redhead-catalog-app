import { ApiClient } from './api.client';
import type {
  AnalyticsClientOption,
  BusinessDemandAnalytics,
  BusinessDemandAnalyticsQueryParams,
} from '../types/analytics.types';

export const adminAnalyticsService = {
  getBusinessDemand(params: BusinessDemandAnalyticsQueryParams): Promise<BusinessDemandAnalytics> {
    const query = new URLSearchParams({
      from: params.from,
      to: params.to,
    });

    if (params.clientId) query.set('clientId', params.clientId);
    if (params.destination) query.set('destination', params.destination);
    if (params.status) query.set('status', params.status);

    return ApiClient.get<BusinessDemandAnalytics>(
      `/api/admin/analytics/business-demand?${query.toString()}`
    );
  },

  listClients(): Promise<AnalyticsClientOption[]> {
    return ApiClient.get<AnalyticsClientOption[]>('/api/admin/analytics/clients');
  },
};
