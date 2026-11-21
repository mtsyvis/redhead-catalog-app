import type { BULK_PREVIEW_OUTCOME } from "../constants/bulkOfferPricePreview.constants";

export interface BulkOfferPriceInputRow {
  rowNumber: number;
  domain: string;
  values: string[];
}

export interface BulkOfferPricePreviewRequest {
  primaryEmail: string;
  termType: number | null;
  termValue: number | null;
  termUnit: number | null;
  sourceCurrency: string;
  priceTypes: number[];
  rows: BulkOfferPriceInputRow[];
}

export interface BulkOfferPriceValue {
  availabilityStatus: number;
  webmasterPriceUsd: string | null;
  sourceCurrency: string;
  sourceAmount: string | null;
  appliedUsdRate: string | null;
  rateDate: string | null;
  appliedUsdSurcharge: number;
}

export interface BulkOfferPricePreviewRow {
  unsubmittedPriceTypes?: number[];
  catalogRecalculationNeeded: boolean;
  purchaseMinima:
    | {
        service: number;
        term: { termKey: string; displayLabel: string };
    beforeUsd: string | null;
    afterUsd: string | null;
        isQuarantined: boolean;
      }[]
    | null;
  domain: string;
  invalidDomain: boolean;
  inputs: BulkOfferPriceInputRow[];
  outcome: (typeof BULK_PREVIEW_OUTCOME)[keyof typeof BULK_PREVIEW_OUTCOME];
  category: string | null;
  reasons: string[];
  offerId: string | null;
  currentTerm: string | null;
  proposedTerm: string | null;
  currentStatus: number | null;
  proposedStatus: number | null;
  prices: {
    priceType: number;
    current: BulkOfferPriceValue | null;
    proposed: BulkOfferPriceValue;
    changed: boolean;
  }[];
}

export interface BulkOfferPricePreview {
  previewVersion: string;
  primaryEmail: string;
  term: string;
  sourceCurrency: string;
  inputCount: number;
  duplicateInputCount: number;
  readyCount: number;
  noChangesCount: number;
  exceptionCount: number;
  rows: BulkOfferPricePreviewRow[];
  conversionReference: {
    sourceCurrency: string;
    appliedUsdRate: string;
    rateReference: string | null;
    rateDate: string | null;
    appliedUsdSurcharge: number;
  } | null;
}

export interface BulkOfferPriceApplyRequest {
  operationId: string;
  input: BulkOfferPricePreviewRequest;
  previewVersion: string;
}

export interface BulkOfferPriceOperation {
  id: string;
  actor: string;
  appliedAtUtc: string;
  priceTypes: number[];
  result: BulkOfferPricePreview;
}

export interface BulkOfferPriceOperationSummary {
  id: string;
  actor: string;
  appliedAtUtc: string;
  primaryEmail: string;
  updatedCount: number;
  noChangesCount: number;
  exceptionCount: number;
}
