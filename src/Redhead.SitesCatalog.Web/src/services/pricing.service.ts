import { ApiClient } from './api.client';
import type { PricingPage, PricingQuery, PricingSite, PricingTerm } from '../types/pricing.types';
import type { UpdateSitePricingPayload } from '../types/sites.types';

export const pricingService = {
  search: (query: PricingQuery) => {
    const params = new URLSearchParams();
    Object.entries(query).forEach(([key, value]) => {
      if (value !== undefined && value !== '') params.set(key, String(value));
    });
    return ApiClient.get<PricingPage>(`/api/pricing?${params}`);
  },
  terms: () => ApiClient.get<PricingTerm[]>('/api/pricing/terms'),
  get: (domain: string) => ApiClient.get<PricingSite>(`/api/pricing/${encodeURIComponent(domain)}`),
  save: (site: PricingSite, pricing: UpdateSitePricingPayload) =>
    ApiClient.put<void>(`/api/pricing/${encodeURIComponent(site.domain)}`, {
      version: site.version,
      pricing,
    }),
};
