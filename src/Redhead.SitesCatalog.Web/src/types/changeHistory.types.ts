export interface EntityFieldChange {
  field: string;
  oldValue: string | null;
  newValue: string | null;
}

export interface EntityChangeHistoryItem {
  id: string;
  action: string;
  source: string;
  changedBy: string;
  changedAtUtc: string;
  changes: EntityFieldChange[];
}
