export interface AhrefsSyncRun {
  id: string;
  startedAt: string;
  finishedAt: string | null;
  status: number;
  runKind: number;
  triggeredByUserId: string | null;
  force: boolean;
  isFullCoverage: boolean;
  wasLimitedByBudget: boolean;
  snapshotMonth: string;
  usageResetDate: string | null;
  eligibleSitesCount: number;
  selectedSitesCount: number;
  processedSitesCount: number;
  updatedSitesCount: number;
  failedSitesCount: number;
  skippedSitesCount: number;
  costPerSite: number;
  fullEstimatedUnits: number;
  selectedEstimatedUnits: number;
  actualUnits: number;
  availableUnitsBefore: number;
  availableUnitsAfter: number | null;
  safetyBufferUnits: number;
  stopIfRemainingUnitsBelow: number;
  batchSize: number;
  maxSitesPerRun: number;
  targetMode: string;
  protocol: string;
  volumeMode: string;
  errorMessage: string | null;
}

export interface AhrefsSyncRunItem {
  id: string;
  runId: string;
  domain: string;
  status: number;
  oldTraffic: number;
  newTraffic: number | null;
  oldDomainRating: number;
  newDomainRating: number | null;
  snapshotMonth: string;
  snapshotSaved: boolean;
  ahrefsIndex: number | null;
  errorMessage: string | null;
}

export interface AhrefsSyncRunsPage {
  items: AhrefsSyncRun[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface AhrefsSyncRunDetails {
  run: AhrefsSyncRun;
  items: AhrefsSyncRunItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface AhrefsSyncStatus {
  schedulerEnabled: boolean;
  cron: string;
  nextScheduledRunUtc: string | null;
  isDueNow: boolean;
  dueOccurrenceUtc: string | null;
  limitsCheckedAt: string;
  usageResetDate: string | null;
  apiKeyRemainingUnits: number;
  workspaceRemainingUnits: number;
  appBudgetRemainingUnits: number;
  effectiveAvailableUnits: number;
  safetyBufferUnits: number;
  eligibleSitesCount: number;
  fullEstimatedUnits: number;
  batchSize: number;
  maxSitesPerRun: number;
  targetMode: string;
  protocol: string;
  volumeMode: string;
  activeRun: AhrefsSyncRun | null;
}
