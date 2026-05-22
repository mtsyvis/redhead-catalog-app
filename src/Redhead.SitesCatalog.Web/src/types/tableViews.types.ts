export type TableViewType = 'system' | 'custom';
export type TableViewDensity = 'compact' | 'standard' | 'comfortable';

export interface TableViewSettings {
  schemaVersion: 1;
  visibleColumnIds: string[];
  density: TableViewDensity;
  columnWidths: Record<string, number>;
}

export interface TableCustomView {
  id: string;
  name: string;
  schemaVersion: number;
  settings: TableViewSettings;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TableViewsResponse {
  activeViewType: TableViewType;
  activeViewKey: string;
  customViews: TableCustomView[];
}

export interface SetActiveTableViewPayload {
  viewType: TableViewType;
  viewKey: string;
}

export interface CreateTableCustomViewPayload {
  name: string;
  settings: TableViewSettings;
}

export interface UpdateTableCustomViewPayload {
  name?: string;
  settings?: TableViewSettings;
}
