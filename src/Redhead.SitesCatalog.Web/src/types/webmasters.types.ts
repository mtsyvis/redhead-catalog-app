import type { WebmasterOffer } from './webmasterOffers.types';

export interface WebmasterSearchResult {
  items: WebmasterSearchItem[];
  page: number;
  pageSize: number;
  total: number;
}

export interface WebmasterSearchItem {
  webmasterId: string;
  primaryEmail: string | null;
  representativeContactRawText: string;
  matchingContactSnippet: string;
  offerCount: number;
  activeOfferCount: number;
  domainCount: number;
  latestOfferUpdatedAtUtc: string;
}

export interface WebmasterWorkspace {
  webmaster: WebmasterWorkspaceSummary;
  domains: WebmasterWorkspaceDomain[];
}

export interface WebmasterWorkspaceSummary {
  webmasterId: string;
  primaryEmail: string | null;
  representativeContactRawText: string;
  offerCount: number;
  activeOfferCount: number;
  domainCount: number;
}

export interface WebmasterWorkspaceDomain {
  domain: string;
  siteFound: boolean;
  isQuarantined: boolean;
  quarantineReason: string | null;
  otherWebmasterOfferCount: number;
  offers: WebmasterOffer[];
}
