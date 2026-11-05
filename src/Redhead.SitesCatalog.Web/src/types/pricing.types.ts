import type { UpdateSitePricingPayload } from './sites.types';
import type { PriceTypeValue } from '../utils/pricing';

export interface PricingTerm {
  termKey: string;
  label: string;
}
export interface PricingPrice {
  priceType: PriceTypeValue;
  termKey: string;
  catalogPrice: number | null;
  offerPrice: number | null;
  offerId: string | null;
  margin: number | null;
}
export interface PricingMarket {
  minimumUsd: number | null;
  averageUsd: number | null;
  medianUsd: number | null;
  checkedAtUtc: string | null;
  isStale: boolean;
  lastError: string | null;
}
export interface PricingSite {
  domain: string;
  isQuarantined: boolean;
  version: string;
  terms: PricingTerm[];
  hasInvalidTerms: boolean;
  pricing: UpdateSitePricingPayload;
  prices: PricingPrice[];
  market: PricingMarket | null;
}
export interface PricingPage {
  items: PricingSite[];
  totalCount: number;
  page: number;
  pageSize: number;
}
export interface PricingQuery {
  domain: string;
  isQuarantined?: boolean;
  termKey: string;
  needsReview: boolean;
  priceType: PriceTypeValue;
  margin: 'all' | 'nonPositive' | 'missing';
  minMargin?: number;
  maxMargin?: number;
  sortBy: 'domain' | 'margin';
  sortPriceType: PriceTypeValue;
  descending: boolean;
  page: number;
  pageSize: number;
}
