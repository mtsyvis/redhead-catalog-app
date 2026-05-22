import type { TableViewDensity, TableViewSettings } from '../../../types/tableViews.types';

export type SitesColumnGroup = 'Main' | 'SEO metrics' | 'Prices' | 'Publication' | 'Admin';

export interface SitesColumnMetadata {
  id: string;
  label: string;
  group: SitesColumnGroup;
  required?: boolean;
  defaultVisible?: boolean;
  includeInViews: boolean;
  systemOnly?: boolean;
  hiddenForClient?: boolean;
  width?: number;
}

export interface SitesSystemView {
  key: string;
  name: string;
  density: TableViewDensity;
  visibleColumnIds: string[];
}

export const SITES_TABLE_KEY = 'sites';
export const TABLE_VIEW_SCHEMA_VERSION = 1;

export const sitesColumnRegistry: SitesColumnMetadata[] = [
  {
    id: 'domain',
    label: 'Domain',
    group: 'Main',
    required: true,
    defaultVisible: true,
    includeInViews: true,
    width: 240,
  },
  {
    id: 'dr',
    label: 'DR',
    group: 'SEO metrics',
    defaultVisible: true,
    includeInViews: true,
    width: 80,
  },
  {
    id: 'traffic',
    label: 'Traffic',
    group: 'SEO metrics',
    defaultVisible: true,
    includeInViews: true,
    width: 120,
  },
  {
    id: 'location',
    label: 'Location',
    group: 'Main',
    defaultVisible: true,
    includeInViews: true,
    width: 120,
  },
  {
    id: 'priceUsd',
    label: 'Price USD',
    group: 'Prices',
    defaultVisible: true,
    includeInViews: true,
    width: 100,
  },
  {
    id: 'priceCasino',
    label: 'Casino',
    group: 'Prices',
    defaultVisible: true,
    includeInViews: true,
    width: 100,
  },
  {
    id: 'priceCrypto',
    label: 'Crypto',
    group: 'Prices',
    defaultVisible: true,
    includeInViews: true,
    width: 100,
  },
  {
    id: 'priceLinkInsert',
    label: 'Link Insert',
    group: 'Prices',
    defaultVisible: true,
    includeInViews: true,
    width: 100,
  },
  {
    id: 'priceLinkInsertCasino',
    label: 'Link Insert Casino',
    group: 'Prices',
    includeInViews: true,
    width: 150,
  },
  {
    id: 'priceDating',
    label: 'Dating',
    group: 'Prices',
    includeInViews: true,
    width: 100,
  },
  {
    id: 'niche',
    label: 'Niche',
    group: 'Main',
    defaultVisible: true,
    includeInViews: true,
    width: 150,
  },
  {
    id: 'categories',
    label: 'Categories',
    group: 'Main',
    defaultVisible: true,
    includeInViews: true,
    width: 150,
  },
  {
    id: 'numberDFLinks',
    label: 'DF Links',
    group: 'SEO metrics',
    includeInViews: true,
    width: 110,
  },
  {
    id: 'sponsoredTag',
    label: 'Sponsored Tag',
    group: 'Publication',
    includeInViews: true,
    width: 150,
  },
  {
    id: 'term',
    label: 'Term',
    group: 'Publication',
    includeInViews: true,
    width: 120,
  },
  {
    id: 'language',
    label: 'Language',
    group: 'Main',
    includeInViews: true,
    width: 100,
  },
  {
    id: 'isQuarantined',
    label: 'Status',
    group: 'Main',
    defaultVisible: true,
    includeInViews: true,
    width: 110,
  },
  {
    id: 'lastPublishedDate',
    label: 'Last Published',
    group: 'Publication',
    defaultVisible: true,
    includeInViews: true,
    width: 150,
  },
  {
    id: 'quarantineReason',
    label: 'Quarantine reason',
    group: 'Admin',
    includeInViews: true,
    hiddenForClient: true,
    width: 160,
  },
  {
    id: 'actions',
    label: 'Actions',
    group: 'Admin',
    includeInViews: false,
    systemOnly: true,
    width: 90,
  },
];

export const sitesSystemViews: SitesSystemView[] = [
  {
    key: 'default',
    name: 'Default',
    density: 'standard',
    visibleColumnIds: [
      'domain',
      'dr',
      'traffic',
      'location',
      'priceUsd',
      'priceCasino',
      'priceCrypto',
      'priceLinkInsert',
      'niche',
      'categories',
      'isQuarantined',
      'lastPublishedDate',
    ],
  },
  {
    key: 'pricing',
    name: 'Pricing',
    density: 'standard',
    visibleColumnIds: [
      'domain',
      'priceUsd',
      'priceCasino',
      'priceCrypto',
      'priceLinkInsert',
      'priceLinkInsertCasino',
      'priceDating',
      'isQuarantined',
      'lastPublishedDate',
    ],
  },
  {
    key: 'seo',
    name: 'SEO',
    density: 'standard',
    visibleColumnIds: [
      'domain',
      'dr',
      'traffic',
      'location',
      'language',
      'niche',
      'categories',
      'numberDFLinks',
      'term',
      'lastPublishedDate',
    ],
  },
  {
    key: 'full',
    name: 'Full',
    density: 'standard',
    visibleColumnIds: [],
  },
];

export function createSitesViewSettings(
  visibleColumnIds: string[],
  density: TableViewDensity
): TableViewSettings {
  const columnWidths = Object.fromEntries(
    sitesColumnRegistry
      .filter((column) => column.includeInViews && column.width)
      .map((column) => [column.id, column.width as number])
  );

  return {
    schemaVersion: TABLE_VIEW_SCHEMA_VERSION,
    visibleColumnIds,
    density,
    columnWidths,
  };
}

export function normalizeSitesVisibleColumnIds(
  columnIds: string[],
  allowedViewColumns: SitesColumnMetadata[]
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

export function insertSitesColumnsByDefaultOrder(
  currentColumnIds: string[],
  columnIdsToAdd: string[],
  allowedViewColumns: SitesColumnMetadata[]
): string[] {
  const allowedColumnIds = new Set(allowedViewColumns.map((column) => column.id));
  const orderByColumnId = new Map(allowedViewColumns.map((column, index) => [column.id, index]));
  const result = normalizeSitesVisibleColumnIds(currentColumnIds, allowedViewColumns);

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

  return normalizeSitesVisibleColumnIds(result, allowedViewColumns);
}
