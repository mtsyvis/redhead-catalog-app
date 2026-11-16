// Wire values must match BulkOfferPricePreviewOutcomes on the server.
export const BULK_PREVIEW_OUTCOME = {
  Ready: "Ready",
  NoChanges: "No changes",
  Exceptions: "Exceptions",
} as const;

export const ALL_EXCEPTION_CATEGORIES = "all";

export const BULK_PREVIEW_TABS = [
  {
    value: BULK_PREVIEW_OUTCOME.Ready,
    label: "Ready to update",
    countKey: "readyCount",
    id: "bulk-preview-tab-ready",
  },
  {
    value: BULK_PREVIEW_OUTCOME.NoChanges,
    label: "No changes",
    countKey: "noChangesCount",
    id: "bulk-preview-tab-no-changes",
  },
  {
    value: BULK_PREVIEW_OUTCOME.Exceptions,
    label: "Exceptions",
    countKey: "exceptionCount",
    id: "bulk-preview-tab-exceptions",
  },
] as const;
