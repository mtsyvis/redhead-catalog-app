import type { ServiceAvailabilityStatus } from './sites.types';

export type WebmasterOfferStatus = 'Active' | 'Inactive' | 1 | 2;
export type LinkbuilderMailboxOfferSource = 'Import' | 'Manual' | 1 | 2;
export type WebmasterOfferPriceType =
  | 'Main'
  | 'Casino'
  | 'Crypto'
  | 'Dating'
  | 'LinkInsertion'
  | 'LinkInsertion18Plus'
  | 'Banner'
  | 'Banner18Plus'
  | 'HomepageTextLink'
  | 'HomepageTextLink18Plus'
  | 0
  | 1
  | 2
  | 3
  | 4
  | 5
  | 6
  | 7
  | 8
  | 9;
export type WebmasterOfferTermType = 'Permanent' | 'Finite' | 1 | 2;
export type WebmasterOfferTermUnit = 'Year' | 1;

export interface WebmasterOffersSearchResult {
  domain: string;
  siteFound: boolean;
  offers: WebmasterOffer[];
}

export interface WebmasterOffer {
  id: string;
  primaryEmail: string | null;
  contactRawText: string;
  outreachSenderRawText: string | null;
  linkbuilderMailboxRawText: string | null;
  linkPolicyText: string | null;
  dfLinksRawText: string | null;
  sponsoredTagRawText: string | null;
  commentText: string | null;
  clientRawText: string | null;
  termRawText: string | null;
  termType: WebmasterOfferTermType | null;
  termValue: number | null;
  termUnit: WebmasterOfferTermUnit | null;
  termLabel: string;
  status: WebmasterOfferStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
  updatedBy: string | null;
  linkbuilderMailboxes: WebmasterOfferMailbox[];
  prices: WebmasterOfferPrice[];
}

export interface WebmasterOfferEdit {
  offer: WebmasterOffer;
  availableMailboxes: WebmasterOfferMailboxOption[];
}

export interface WebmasterOfferMailboxOption {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
}

export interface UpdateWebmasterOfferPricePayload {
  priceType: number;
  availabilityStatus: number;
  webmasterPriceUsd: number | null;
  webmasterPriceDetails: string | null;
}

export interface UpdateWebmasterOfferPayload {
  expectedUpdatedAtUtc: string;
  status: number;
  outreachSenderRawText: string | null;
  linkPolicyText: string | null;
  dfLinksRawText: string | null;
  sponsoredTagRawText: string | null;
  commentText: string | null;
  clientRawText: string | null;
  termType: number | null;
  termValue: number | null;
  termUnit: number | null;
  linkbuilderMailboxIds: string[];
  prices: UpdateWebmasterOfferPricePayload[];
}

export interface WebmasterOfferMailbox {
  id: string;
  email: string;
  displayName: string;
  source: LinkbuilderMailboxOfferSource;
}

export interface WebmasterOfferPrice {
  id: string;
  priceType: WebmasterOfferPriceType;
  availabilityStatus: ServiceAvailabilityStatus;
  webmasterPriceUsd: number | null;
  webmasterPriceDetails: string | null;
  termType: WebmasterOfferTermType | null;
  termValue: number | null;
  termUnit: WebmasterOfferTermUnit | null;
  termLabel: string;
}
