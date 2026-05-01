import type {
  ServiceAvailabilityStatusValue,
  Site,
  TermTypeValue,
  UpdateSitePayload,
} from '../../types/sites.types';
import {
  normalizeServiceAvailabilityStatus,
  SERVICE_AVAILABILITY_STATUS,
} from '../../utils/serviceAvailability';
import { normalizeTermType, TERM_TYPE, TERM_UNIT } from '../../utils/term';

export type EditSiteFormState = {
  dr: string;
  traffic: string;
  location: string;
  priceUsd: string;
  priceCasino: string;
  priceCasinoStatus: ServiceAvailabilityStatusValue;
  priceCrypto: string;
  priceCryptoStatus: ServiceAvailabilityStatusValue;
  priceLinkInsert: string;
  priceLinkInsertStatus: ServiceAvailabilityStatusValue;
  priceLinkInsertCasino: string;
  priceLinkInsertCasinoStatus: ServiceAvailabilityStatusValue;
  priceDating: string;
  priceDatingStatus: ServiceAvailabilityStatusValue;
  numberDFLinks: string;
  termType: '' | TermTypeValue;
  termValue: string;
  niche: string;
  categories: string;
  sponsoredTag: string;
  isQuarantined: boolean;
  quarantineReason: string;
};

export type OptionalServiceStatusField =
  | 'priceCasinoStatus'
  | 'priceCryptoStatus'
  | 'priceLinkInsertStatus'
  | 'priceLinkInsertCasinoStatus'
  | 'priceDatingStatus';

export type OptionalServicePriceField =
  | 'priceCasino'
  | 'priceCrypto'
  | 'priceLinkInsert'
  | 'priceLinkInsertCasino'
  | 'priceDating';

export function parseNumberOrNull(input: string): number | null {
  const t = input.trim();
  if (t === '') return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

export function getServiceStateHint(status: ServiceAvailabilityStatusValue): string {
  if (status === SERVICE_AVAILABILITY_STATUS.NotAvailable) return 'Will be shown as NO';
  if (status === SERVICE_AVAILABILITY_STATUS.Unknown) return 'Will be shown as —';
  return 'Enter a non-negative price';
}

export function clearFieldError(
  errors: Record<string, string[]>,
  key: string
): Record<string, string[]> {
  if (errors[key]) {
    const next = { ...errors };
    delete next[key];
    return next;
  }
  return errors;
}

export const EMPTY_FORM_STATE: EditSiteFormState = {
  dr: '',
  traffic: '',
  location: '',
  priceUsd: '',
  priceCasino: '',
  priceCasinoStatus: SERVICE_AVAILABILITY_STATUS.Unknown,
  priceCrypto: '',
  priceCryptoStatus: SERVICE_AVAILABILITY_STATUS.Unknown,
  priceLinkInsert: '',
  priceLinkInsertStatus: SERVICE_AVAILABILITY_STATUS.Unknown,
  priceLinkInsertCasino: '',
  priceLinkInsertCasinoStatus: SERVICE_AVAILABILITY_STATUS.Unknown,
  priceDating: '',
  priceDatingStatus: SERVICE_AVAILABILITY_STATUS.Unknown,
  numberDFLinks: '',
  termType: '',
  termValue: '',
  niche: '',
  categories: '',
  sponsoredTag: '',
  isQuarantined: false,
  quarantineReason: '',
};

export function createInitialFormState(site: Site): EditSiteFormState {
  const casinoStatus = normalizeServiceAvailabilityStatus(site.priceCasinoStatus);
  const cryptoStatus = normalizeServiceAvailabilityStatus(site.priceCryptoStatus);
  const linkInsertStatus = normalizeServiceAvailabilityStatus(site.priceLinkInsertStatus);
  const linkInsertCasinoStatus = normalizeServiceAvailabilityStatus(site.priceLinkInsertCasinoStatus);
  const datingStatus = normalizeServiceAvailabilityStatus(site.priceDatingStatus);
  const termType = normalizeTermType(site.termType);
  return {
    dr: String(site.dr ?? ''),
    traffic: String(site.traffic ?? ''),
    location: site.location ?? '',
    priceUsd: site.priceUsd == null ? '' : String(site.priceUsd),
    priceCasinoStatus: casinoStatus,
    priceCasino:
      casinoStatus === SERVICE_AVAILABILITY_STATUS.Available && site.priceCasino != null
        ? String(site.priceCasino)
        : '',
    priceCryptoStatus: cryptoStatus,
    priceCrypto:
      cryptoStatus === SERVICE_AVAILABILITY_STATUS.Available && site.priceCrypto != null
        ? String(site.priceCrypto)
        : '',
    priceLinkInsertStatus: linkInsertStatus,
    priceLinkInsert:
      linkInsertStatus === SERVICE_AVAILABILITY_STATUS.Available && site.priceLinkInsert != null
        ? String(site.priceLinkInsert)
        : '',
    priceLinkInsertCasinoStatus: linkInsertCasinoStatus,
    priceLinkInsertCasino:
      linkInsertCasinoStatus === SERVICE_AVAILABILITY_STATUS.Available && site.priceLinkInsertCasino != null
        ? String(site.priceLinkInsertCasino)
        : '',
    priceDatingStatus: datingStatus,
    priceDating:
      datingStatus === SERVICE_AVAILABILITY_STATUS.Available && site.priceDating != null
        ? String(site.priceDating)
        : '',
    numberDFLinks: site.numberDFLinks == null ? '' : String(site.numberDFLinks),
    termType: termType ?? '',
    termValue:
      termType === TERM_TYPE.Finite && site.termValue != null
        ? String(site.termValue)
        : '',
    niche: site.niche ?? '',
    categories: site.categories ?? '',
    sponsoredTag: site.sponsoredTag ?? '',
    isQuarantined: Boolean(site.isQuarantined),
    quarantineReason: site.quarantineReason ?? '',
  };
}

function validateOptionalServicePrice(
  price: string,
  fieldKey: string,
  errors: Record<string, string[]>
): void {
  const parsed = parseNumberOrNull(price);
  if (parsed === null) {
    errors[fieldKey] = ['Required when status is Available.'];
  } else if (parsed < 0) {
    errors[fieldKey] = ['Must be 0 or greater.'];
  }
}

export function validateEditSiteForm(form: EditSiteFormState): Record<string, string[]> {
  const errors: Record<string, string[]> = {};

  const parsedDr = parseNumberOrNull(form.dr);
  if (parsedDr === null || parsedDr < 0 || parsedDr > 100) {
    errors.dr = ['DR must be between 0 and 100.'];
  }

  const parsedTraffic = parseNumberOrNull(form.traffic);
  if (parsedTraffic === null || parsedTraffic < 0) {
    errors.traffic = ['Traffic must be 0 or greater.'];
  } else if (!Number.isInteger(parsedTraffic)) {
    errors.traffic = ['Traffic must be a whole number.'];
  }

  if (!form.location.trim()) {
    errors.location = ['Location is required.'];
  }

  const parsedPriceUsd = parseNumberOrNull(form.priceUsd);
  if (parsedPriceUsd !== null && parsedPriceUsd < 0) {
    errors.priceUsd = ['Price USD must be 0 or greater.'];
  }

  if (form.priceCasinoStatus === SERVICE_AVAILABILITY_STATUS.Available) {
    validateOptionalServicePrice(form.priceCasino, 'priceCasino', errors);
  }
  if (form.priceCryptoStatus === SERVICE_AVAILABILITY_STATUS.Available) {
    validateOptionalServicePrice(form.priceCrypto, 'priceCrypto', errors);
  }
  if (form.priceLinkInsertStatus === SERVICE_AVAILABILITY_STATUS.Available) {
    validateOptionalServicePrice(form.priceLinkInsert, 'priceLinkInsert', errors);
  }
  if (form.priceLinkInsertCasinoStatus === SERVICE_AVAILABILITY_STATUS.Available) {
    validateOptionalServicePrice(form.priceLinkInsertCasino, 'priceLinkInsertCasino', errors);
  }
  if (form.priceDatingStatus === SERVICE_AVAILABILITY_STATUS.Available) {
    validateOptionalServicePrice(form.priceDating, 'priceDating', errors);
  }

  const parsedNumberDFLinks = parseNumberOrNull(form.numberDFLinks);
  if (form.numberDFLinks.trim() !== '') {
    if (parsedNumberDFLinks === null || parsedNumberDFLinks <= 0) {
      errors.numberDFLinks = ['Number DF Links must be greater than 0.'];
    } else if (!Number.isInteger(parsedNumberDFLinks)) {
      errors.numberDFLinks = ['Number DF Links must be a whole number.'];
    }
  }

  if (form.termType === TERM_TYPE.Finite) {
    const parsedTermValue = parseNumberOrNull(form.termValue);
    if (parsedTermValue === null || parsedTermValue <= 0) {
      errors.termValue = ['Term value must be greater than 0.'];
    } else if (!Number.isInteger(parsedTermValue)) {
      errors.termValue = ['Term value must be a whole number.'];
    }
  }

  const casinoNumericPrice =
    form.priceCasinoStatus === SERVICE_AVAILABILITY_STATUS.Available
      ? parseNumberOrNull(form.priceCasino)
      : null;
  const cryptoNumericPrice =
    form.priceCryptoStatus === SERVICE_AVAILABILITY_STATUS.Available
      ? parseNumberOrNull(form.priceCrypto)
      : null;
  const linkInsertNumericPrice =
    form.priceLinkInsertStatus === SERVICE_AVAILABILITY_STATUS.Available
      ? parseNumberOrNull(form.priceLinkInsert)
      : null;
  const linkInsertCasinoNumericPrice =
    form.priceLinkInsertCasinoStatus === SERVICE_AVAILABILITY_STATUS.Available
      ? parseNumberOrNull(form.priceLinkInsertCasino)
      : null;
  const datingNumericPrice =
    form.priceDatingStatus === SERVICE_AVAILABILITY_STATUS.Available
      ? parseNumberOrNull(form.priceDating)
      : null;

  if (
    parsedPriceUsd === null &&
    casinoNumericPrice === null &&
    cryptoNumericPrice === null &&
    linkInsertNumericPrice === null &&
    linkInsertCasinoNumericPrice === null &&
    datingNumericPrice === null
  ) {
    const errorText = 'At least one numeric price is required.';
    errors.priceUsd = [errorText];
    errors.priceCasino = [errorText];
    errors.priceCrypto = [errorText];
    errors.priceLinkInsert = [errorText];
    errors.priceLinkInsertCasino = [errorText];
    errors.priceDating = [errorText];
  }

  return errors;
}

export function buildUpdateSitePayload(form: EditSiteFormState): UpdateSitePayload {
  return {
    dr: parseNumberOrNull(form.dr)!,
    traffic: parseNumberOrNull(form.traffic)!,
    location: form.location.trim(),
    priceUsd: parseNumberOrNull(form.priceUsd),
    priceCasino:
      form.priceCasinoStatus === SERVICE_AVAILABILITY_STATUS.Available
        ? (parseNumberOrNull(form.priceCasino) ?? null)
        : null,
    priceCasinoStatus: form.priceCasinoStatus,
    priceCrypto:
      form.priceCryptoStatus === SERVICE_AVAILABILITY_STATUS.Available
        ? (parseNumberOrNull(form.priceCrypto) ?? null)
        : null,
    priceCryptoStatus: form.priceCryptoStatus,
    priceLinkInsert:
      form.priceLinkInsertStatus === SERVICE_AVAILABILITY_STATUS.Available
        ? (parseNumberOrNull(form.priceLinkInsert) ?? null)
        : null,
    priceLinkInsertStatus: form.priceLinkInsertStatus,
    priceLinkInsertCasino:
      form.priceLinkInsertCasinoStatus === SERVICE_AVAILABILITY_STATUS.Available
        ? (parseNumberOrNull(form.priceLinkInsertCasino) ?? null)
        : null,
    priceLinkInsertCasinoStatus: form.priceLinkInsertCasinoStatus,
    priceDating:
      form.priceDatingStatus === SERVICE_AVAILABILITY_STATUS.Available
        ? (parseNumberOrNull(form.priceDating) ?? null)
        : null,
    priceDatingStatus: form.priceDatingStatus,
    numberDFLinks: parseNumberOrNull(form.numberDFLinks),
    termType: form.termType === '' ? null : form.termType,
    termValue:
      form.termType === TERM_TYPE.Finite
        ? (parseNumberOrNull(form.termValue) ?? null)
        : null,
    termUnit: form.termType === TERM_TYPE.Finite ? TERM_UNIT.Year : null,
    niche: form.niche.trim() || null,
    categories: form.categories.trim() || null,
    SponsoredTag: form.sponsoredTag.trim() || null,
    isQuarantined: form.isQuarantined,
    quarantineReason: form.isQuarantined ? (form.quarantineReason.trim() || null) : null,
  };
}
