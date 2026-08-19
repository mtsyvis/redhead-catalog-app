import type {
  UpdateWebmasterOfferPayload,
  WebmasterOffer,
  WebmasterOfferEdit,
  WebmasterOfferMailboxOption,
  WebmasterOfferPrice,
} from '../../types/webmasterOffers.types';
import {
  SERVICE_AVAILABILITY_STATUS,
  normalizeServiceAvailabilityStatus,
} from '../../utils/serviceAvailability';

export const PRICE_TYPES = [
  { value: 0, label: 'Price USD', enumName: 'Main' },
  { value: 1, label: 'Casino', enumName: 'Casino' },
  { value: 2, label: 'Crypto', enumName: 'Crypto' },
  { value: 3, label: 'Dating', enumName: 'Dating' },
  { value: 4, label: 'Link Insert', enumName: 'LinkInsertion' },
  { value: 5, label: 'Link Insert 18+', enumName: 'LinkInsertion18Plus' },
  { value: 6, label: 'Banner', enumName: 'Banner' },
  { value: 7, label: 'Banner 18+', enumName: 'Banner18Plus' },
  { value: 8, label: 'Homepage Text Link', enumName: 'HomepageTextLink' },
  { value: 9, label: 'Homepage Text Link 18+', enumName: 'HomepageTextLink18Plus' },
] as const;

export const AVAILABILITY_OPTIONS = [
  { value: SERVICE_AVAILABILITY_STATUS.Available, label: 'Numeric price' },
  { value: SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice, label: 'YES' },
  { value: SERVICE_AVAILABILITY_STATUS.NotAvailable, label: 'NO' },
  { value: SERVICE_AVAILABILITY_STATUS.Unknown, label: 'Unknown' },
] as const;

export const STATUS_ACTIVE = 1;
export const STATUS_INACTIVE = 2;

const TERM_PERMANENT = 1;
const TERM_FINITE = 2;
const TERM_YEAR = 1;

export type TermKind = 'none' | 'permanent' | 'finite';

export interface PriceFormRow {
  availabilityStatus: number;
  amount: string;
  details: string;
}

export interface OfferFormState {
  status: number;
  termKind: TermKind;
  termYears: string;
  outreachSenderRawText: string;
  linkPolicyText: string;
  dfLinksRawText: string;
  sponsoredTagRawText: string;
  commentText: string;
  clientRawText: string;
  mailboxIds: string[];
  prices: Record<number, PriceFormRow>;
}

export type OfferFieldUpdater = <K extends keyof OfferFormState>(
  field: K,
  value: OfferFormState[K]
) => void;

function normalizeStatus(offer: WebmasterOffer) {
  return offer.status === 'Inactive' || offer.status === STATUS_INACTIVE
    ? STATUS_INACTIVE
    : STATUS_ACTIVE;
}

function priceMatches(price: WebmasterOfferPrice, value: number, enumName: string) {
  return price.priceType === value || price.priceType === enumName;
}

export function createWebmasterOfferForm(edit: WebmasterOfferEdit): OfferFormState {
  const offer = edit.offer;
  const termKind: TermKind = offer.termType === TERM_PERMANENT || offer.termType === 'Permanent'
    ? 'permanent'
    : offer.termType === TERM_FINITE || offer.termType === 'Finite'
      ? 'finite'
      : 'none';
  const prices = Object.fromEntries(
    PRICE_TYPES.map((type) => {
      const price = offer.prices.find((item) => priceMatches(item, type.value, type.enumName));
      return [
        type.value,
        {
          availabilityStatus: normalizeServiceAvailabilityStatus(price?.availabilityStatus),
          amount: price?.webmasterPriceUsd == null ? '' : String(price.webmasterPriceUsd),
          details: price?.webmasterPriceDetails ?? '',
        },
      ];
    })
  );

  return {
    status: normalizeStatus(offer),
    termKind,
    termYears: termKind === 'finite' && offer.termValue ? String(offer.termValue) : '',
    outreachSenderRawText: offer.outreachSenderRawText ?? '',
    linkPolicyText: offer.linkPolicyText ?? '',
    dfLinksRawText: offer.dfLinksRawText ?? '',
    sponsoredTagRawText: offer.sponsoredTagRawText ?? '',
    commentText: offer.commentText ?? '',
    clientRawText: offer.clientRawText ?? '',
    mailboxIds: offer.linkbuilderMailboxes.map((mailbox) => mailbox.id),
    prices,
  };
}

function trimToNull(value: string) {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
}

export function buildWebmasterOfferPayload(
  edit: WebmasterOfferEdit,
  form: OfferFormState
): UpdateWebmasterOfferPayload {
  const termYears = form.termKind === 'finite' ? Number(form.termYears) : null;
  const prices = PRICE_TYPES.flatMap((type) => {
    const row = form.prices[type.value];
    const amount = row.availabilityStatus === SERVICE_AVAILABILITY_STATUS.Available
      ? Number(row.amount)
      : null;
    const details = trimToNull(row.details);
    if (
      row.availabilityStatus === SERVICE_AVAILABILITY_STATUS.Unknown &&
      amount == null &&
      details == null
    ) {
      return [];
    }

    return [{
      priceType: type.value,
      availabilityStatus: row.availabilityStatus,
      webmasterPriceUsd: amount,
      webmasterPriceDetails: details,
    }];
  });

  return {
    expectedUpdatedAtUtc: edit.offer.updatedAtUtc,
    status: form.status,
    outreachSenderRawText: trimToNull(form.outreachSenderRawText),
    linkPolicyText: trimToNull(form.linkPolicyText),
    dfLinksRawText: trimToNull(form.dfLinksRawText),
    sponsoredTagRawText: trimToNull(form.sponsoredTagRawText),
    commentText: trimToNull(form.commentText),
    clientRawText: trimToNull(form.clientRawText),
    termType: form.termKind === 'none'
      ? null
      : form.termKind === 'permanent'
        ? TERM_PERMANENT
        : TERM_FINITE,
    termValue: termYears,
    termUnit: form.termKind === 'finite' ? TERM_YEAR : null,
    linkbuilderMailboxIds: [...form.mailboxIds].sort(),
    prices,
  };
}

export function validateWebmasterOfferForm(form: OfferFormState) {
  const errors: Record<string, string[]> = {};
  if (form.termKind === 'finite') {
    const years = Number(form.termYears);
    if (!Number.isInteger(years) || years <= 0) {
      errors.term = ['Years must be a positive whole number.'];
    }
  }

  PRICE_TYPES.forEach((type) => {
    const row = form.prices[type.value];
    if (row.availabilityStatus !== SERVICE_AVAILABILITY_STATUS.Available) return;
    const amount = Number(row.amount);
    if (!row.amount.trim() || !Number.isFinite(amount) || amount <= 0) {
      errors[`prices.${type.value}.webmasterPriceUsd`] = ['Price must be greater than 0.'];
    } else if (!/^\d+(?:\.\d{1,2})?$/.test(row.amount.trim())) {
      errors[`prices.${type.value}.webmasterPriceUsd`] = ['Price may have at most 2 decimal places.'];
    }
  });

  return errors;
}

export function webmasterOfferFormSignature(form: OfferFormState) {
  return JSON.stringify({
    ...form,
    mailboxIds: [...form.mailboxIds].sort(),
  });
}

export function mailboxLabel(mailbox: WebmasterOfferMailboxOption) {
  const label = mailbox.displayName && mailbox.displayName !== mailbox.email
    ? `${mailbox.displayName} <${mailbox.email}>`
    : mailbox.email;
  return mailbox.isActive ? label : `${label} (inactive)`;
}
