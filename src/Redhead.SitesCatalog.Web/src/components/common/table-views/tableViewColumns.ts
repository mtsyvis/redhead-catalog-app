import type { TableViewDensity, TableViewSettings } from '../../../types/tableViews.types';

export type TableColumnGroup =
  | 'Main'
  | 'SEO metrics'
  | 'Prices'
  | 'Publication'
  | 'Admin'
  | 'Market prices';

export interface TableColumnMetadata {
  id: string;
  label: string;
  group: TableColumnGroup;
  required?: boolean;
  defaultVisible?: boolean;
  includeInViews: boolean;
  systemOnly?: boolean;
  hiddenForClient?: boolean;
  width?: number;
  minWidth?: number;
  maxWidth?: number;
  resizable?: boolean;
}

export interface TableSystemView {
  key: string;
  name: string;
  density: TableViewDensity;
  visibleColumnIds: string[];
  columnWidths?: Record<string, number>;
}

export const TABLE_VIEW_SCHEMA_VERSION = 1;

export function createTableViewSettings(
  columns: TableColumnMetadata[],
  visibleColumnIds: string[],
  density: TableViewDensity,
  columnWidthsInput: Record<string, number> = {}
): TableViewSettings {
  const columnWidths = createDefaultTableColumnWidths(columns);
  for (const [columnId, width] of Object.entries(columnWidthsInput)) {
    const column = columns.find(
      (item) => item.id === columnId && item.includeInViews && !item.systemOnly
    );
    const normalized = normalizeTableColumnWidth(column, width);
    if (normalized != null) columnWidths[columnId] = normalized;
  }
  return { schemaVersion: TABLE_VIEW_SCHEMA_VERSION, visibleColumnIds, density, columnWidths };
}

export function createDefaultTableColumnWidths(
  columns: TableColumnMetadata[]
): Record<string, number> {
  return Object.fromEntries(
    columns
      .filter((column) => column.includeInViews && !column.systemOnly && column.width)
      .map((column) => [column.id, column.width as number])
  );
}

export function normalizeTableColumnWidth(
  column: TableColumnMetadata | undefined,
  width: number
): number | null {
  if (!column || column.systemOnly || !column.includeInViews || !Number.isFinite(width)) {
    return null;
  }

  const minWidth = column.minWidth ?? 50;
  const maxWidth = column.maxWidth ?? 1000;
  return Math.min(maxWidth, Math.max(minWidth, Math.round(width)));
}

export function normalizeTableVisibleColumnIds(
  columnIds: string[],
  allowedViewColumns: TableColumnMetadata[]
): string[] {
  const allowedColumnIds = new Set(allowedViewColumns.map((column) => column.id));
  const requiredColumnIds = allowedViewColumns
    .filter((column) => column.required)
    .map((column) => column.id);
  const result: string[] = [];

  for (const columnId of [...requiredColumnIds, ...columnIds]) {
    if (allowedColumnIds.has(columnId) && !result.includes(columnId)) {
      result.push(columnId);
    }
  }

  return result.length > 0 ? result : requiredColumnIds;
}

export function insertTableColumnsByDefaultOrder(
  currentColumnIds: string[],
  columnIdsToAdd: string[],
  allowedViewColumns: TableColumnMetadata[]
): string[] {
  const allowedColumnIds = new Set(allowedViewColumns.map((column) => column.id));
  const orderByColumnId = new Map(allowedViewColumns.map((column, index) => [column.id, index]));
  const result = normalizeTableVisibleColumnIds(currentColumnIds, allowedViewColumns);

  for (const columnId of columnIdsToAdd) {
    if (!allowedColumnIds.has(columnId) || result.includes(columnId)) {
      continue;
    }

    const columnOrder = orderByColumnId.get(columnId) ?? Number.MAX_SAFE_INTEGER;
    const firstMovableIndex = result.findIndex(
      (resultColumnId) =>
        !allowedViewColumns.find((column) => column.id === resultColumnId)?.required
    );
    const lockedPrefixEnd = firstMovableIndex === -1 ? result.length : firstMovableIndex;
    let insertAt = result.length;

    for (let index = lockedPrefixEnd; index < result.length; index += 1) {
      const resultColumnOrder = orderByColumnId.get(result[index]) ?? Number.MAX_SAFE_INTEGER;
      if (resultColumnOrder > columnOrder) {
        insertAt = index;
        break;
      }
    }

    result.splice(insertAt, 0, columnId);
  }

  return normalizeTableVisibleColumnIds(result, allowedViewColumns);
}
