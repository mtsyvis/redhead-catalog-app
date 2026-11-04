export interface SyncPage<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
export interface LinkDetectiveSyncRun {
  id: string;
  startedAtUtc: string;
  finishedAtUtc: string | null;
  status: number;
  selectedCount: number;
  processedCount: number;
  pricesFoundCount: number;
  notFoundCount: number;
  failedCount: number;
  reusedCount: number;
  skippedCount: number;
  nextAttemptAtUtc: string | null;
  waitingReason: string | null;
}
export interface LinkDetectiveSyncItem {
  runId: string;
  domain: string;
  status: number;
  attempts: number;
  attemptedAtUtc: string | null;
  checkedAtUtc: string | null;
  nextAttemptAtUtc: string | null;
  error: string | null;
}
export interface LinkDetectiveSyncStatus {
  enabled: boolean;
  configured: boolean;
  cacheLifetimeDays: number;
  dailySelectionTimeUtc: string;
  batchSize: number;
  onDemandReserve: number;
  maxAttempts: number;
  retryDelayMinutes: number;
  nextSelectionAtUtc: string | null;
  catalogCount: number;
  freshCount: number;
  outdatedCount: number;
  neverCheckedCount: number;
  activeRun: LinkDetectiveSyncRun | null;
  providerState: {
    quotaLimit: number | null;
    quotaUsed: number | null;
    quotaRemaining: number | null;
    quotaPeriod: string | null;
    quotaResetAtUtc: string | null;
    quotaObservedAtUtc: string | null;
  } | null;
}
export interface LinkDetectiveSyncDetails {
  run: LinkDetectiveSyncRun;
  results: SyncPage<LinkDetectiveSyncItem>;
}
