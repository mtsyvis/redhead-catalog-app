import type { ServiceAvailabilityStatus } from './sites.types';
import type { WebmasterOfferPriceType, WebmasterOfferStatus } from './webmasterOffers.types';

export interface WebmasterComparisonMargin {
  termKey: string;
  amountUsd: number | null;
  unavailableReason: string | null;
}

export interface WebmasterComparisonPrice {
  priceType: WebmasterOfferPriceType;
  availabilityStatus: ServiceAvailabilityStatus;
  webmasterPriceUsd: number | null;
  termKey: string | null;
  termLabel: string;
  margins: WebmasterComparisonMargin[];
}

export interface WebmasterComparisonOffer {
  id: string;
  primaryEmail: string | null;
  status: WebmasterOfferStatus;
  termLabel: string;
  prices: WebmasterComparisonPrice[];
}

export interface WebmasterComparisonTerm {
  termKey: string;
  label: string;
  comparableOfferCount: number;
  catalogPrices: { priceType: WebmasterOfferPriceType; amountUsd: number | null }[];
}

export interface WebmasterPriceComparison {
  domain: string;
  siteFound: boolean;
  defaultTermKey: string | null;
  terms: WebmasterComparisonTerm[];
  offers: WebmasterComparisonOffer[];
}
