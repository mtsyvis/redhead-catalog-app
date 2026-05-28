import { useMemo } from 'react';
import { IconButton, Tooltip } from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import type { GridColDef } from '@mui/x-data-grid';
import type { Site } from '../../../types/sites.types';
import { formatLanguageTableValue } from '../../../utils/language';
import { formatOptionalServicePrice } from '../../../utils/serviceAvailability';
import { formatTerm } from '../../../utils/term';
import { StatusBadge } from '../feedback/StatusBadge';
import type { GridRow } from './useSitesGridRows';
import { isNotFoundRow } from './useSitesGridRows';
import {
  normalizeSitesColumnWidth,
  sitesColumnRegistry,
} from '../table-views/sitesTableColumns';
import type { SitesColumnMetadata } from '../table-views/sitesTableColumns';

interface UseSitesColumnsOptions {
  isAdmin: boolean;
  isClient: boolean;
  visibleColumnIds: string[];
  columnWidths: Record<string, number>;
  onEdit: (site: Site) => void;
}

function formatCell<T>(row: GridRow, value: T, format: (v: T) => string): string {
  return isNotFoundRow(row) ? '—' : format(value);
}

function formatPrice(row: GridRow, value: number | null): string {
  return formatCell(row, value, (v) => (v == null ? 'NO' : `$${v}`));
}

function formatOptionalServiceCell(
  row: GridRow,
  price: number | null,
  status: Site['priceCasinoStatus']
): string {
  return formatCell(row, price, (v) => formatOptionalServicePrice(status, v));
}

function formatNullableInteger(row: GridRow, value: number | null): string {
  return formatCell(row, value, (v) => (v == null ? '—' : String(v)));
}

function formatAuditDate(row: GridRow, value: string | null | undefined): string {
  if (isNotFoundRow(row) || !value) return '—';

  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '—';

  const day = String(d.getUTCDate()).padStart(2, '0');
  const month = String(d.getUTCMonth() + 1).padStart(2, '0');
  const year = d.getUTCFullYear();
  return `${day}.${month}.${year}`;
}

function formatAuditUser(row: GridRow, value: string | null | undefined): string {
  if (isNotFoundRow(row)) return '—';
  return value?.trim() || 'system';
}

function formatLocationCell(row: GridRow, value: string | null): string {
  if (isNotFoundRow(row)) return '—';

  const location = value || '—';
  const importedLocationRaw = (row as Site).importedLocationRaw?.trim();
  if (location === 'Other' && importedLocationRaw) {
    return `Other - ${importedLocationRaw}`;
  }

  return location;
}

const columnMetadata: Record<string, SitesColumnMetadata> = Object.fromEntries(
  sitesColumnRegistry.map((column) => [column.id, column])
);
const domainDefaultWidth = columnMetadata.domain.width;

function gridColumnDefaults(
  field: string,
  columnWidths: Record<string, number>
): Pick<
  GridColDef<GridRow>,
  'headerName' | 'width' | 'minWidth' | 'maxWidth' | 'hideable' | 'resizable'
> {
  const metadata = columnMetadata[field];
  const width = normalizeSitesColumnWidth(metadata, columnWidths[field]) ?? metadata?.width;

  return {
    headerName: metadata?.label ?? field,
    width,
    minWidth: metadata?.minWidth,
    maxWidth: metadata?.maxWidth,
    hideable: !metadata?.required && !metadata?.systemOnly,
    resizable: metadata?.resizable ?? !metadata?.systemOnly,
  };
}

export function useSitesColumns({
  isAdmin,
  isClient,
  visibleColumnIds,
  columnWidths,
  onEdit,
}: UseSitesColumnsOptions) {
  return useMemo<GridColDef<GridRow>[]>(
    () => {
      const allColumns: GridColDef<GridRow>[] = [
        {
          ...gridColumnDefaults('domain', columnWidths),
          field: 'domain',
          ...(columnWidths.domain === domainDefaultWidth ? { flex: 1 } : {}),
        },
        {
          ...gridColumnDefaults('dr', columnWidths),
          field: 'dr',
          type: 'number',
          valueFormatter: (value, row) =>
            formatCell(row, value as number | null, (v) => (v == null ? '' : String(v))),
        },
        {
          ...gridColumnDefaults('traffic', columnWidths),
          field: 'traffic',
          type: 'number',
          valueFormatter: (value, row) => {
            if (isNotFoundRow(row)) return '—';
            if (value == null) return '';
            return new Intl.NumberFormat('en-US').format(value as number);
          },
        },
        {
          ...gridColumnDefaults('location', columnWidths),
          field: 'location',
          valueFormatter: (value, row) => formatLocationCell(row, value as string | null),
        },
        {
          ...gridColumnDefaults('priceUsd', columnWidths),
          field: 'priceUsd',
          type: 'number',
          valueFormatter: (value, row) => formatPrice(row, value as number | null),
        },
        {
          ...gridColumnDefaults('priceCasino', columnWidths),
          field: 'priceCasino',
          type: 'number',
          valueFormatter: (value, row) =>
            formatOptionalServiceCell(row, value as number | null, (row as Site).priceCasinoStatus),
        },
        {
          ...gridColumnDefaults('priceCrypto', columnWidths),
          field: 'priceCrypto',
          type: 'number',
          valueFormatter: (value, row) =>
            formatOptionalServiceCell(row, value as number | null, (row as Site).priceCryptoStatus),
        },
        {
          ...gridColumnDefaults('priceLinkInsert', columnWidths),
          field: 'priceLinkInsert',
          type: 'number',
          valueFormatter: (value, row) =>
            formatOptionalServiceCell(
              row,
              value as number | null,
              (row as Site).priceLinkInsertStatus
            ),
        },
        {
          ...gridColumnDefaults('priceLinkInsertCasino', columnWidths),
          field: 'priceLinkInsertCasino',
          type: 'number',
          valueFormatter: (value, row) =>
            formatOptionalServiceCell(
              row,
              value as number | null,
              (row as Site).priceLinkInsertCasinoStatus
            ),
        },
        {
          ...gridColumnDefaults('priceDating', columnWidths),
          field: 'priceDating',
          type: 'number',
          valueFormatter: (value, row) =>
            formatOptionalServiceCell(row, value as number | null, (row as Site).priceDatingStatus),
        },
        {
          ...gridColumnDefaults('niche', columnWidths),
          field: 'niche',
          sortable: false,
          valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
        },
        {
          ...gridColumnDefaults('categories', columnWidths),
          field: 'categories',
          sortable: false,
          valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
        },
        {
          ...gridColumnDefaults('numberDFLinks', columnWidths),
          field: 'numberDFLinks',
          type: 'number',
          valueFormatter: (value, row) => formatNullableInteger(row, value as number | null),
        },
        {
          ...gridColumnDefaults('sponsoredTag', columnWidths),
          field: 'sponsoredTag',
          sortable: false,
          valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
        },
        {
          ...gridColumnDefaults('term', columnWidths),
          field: 'term',
          valueFormatter: (_value, row) => {
            if (isNotFoundRow(row)) return '—';
            const site = row as Site;
            return formatTerm(site.termType, site.termValue, site.termUnit);
          },
        },
        {
          ...gridColumnDefaults('language', columnWidths),
          field: 'language',
          sortable: false,
          valueFormatter: (value, row) => {
            if (isNotFoundRow(row)) return '—';
            return formatLanguageTableValue(value as string | null);
          },
        },
        {
          ...gridColumnDefaults('isQuarantined', columnWidths),
          field: 'isQuarantined',
          sortable: false,
          align: 'center',
          headerAlign: 'center',
          renderCell: (params) => {
            if (isNotFoundRow(params.row)) return '—';
            const isQuarantined = params.value as boolean;
            return <StatusBadge isAvailable={!isQuarantined} />;
          },
        },
        {
          ...gridColumnDefaults('lastPublishedDate', columnWidths),
          field: 'lastPublishedDate',
          valueFormatter: (_value, row) => {
            if (isNotFoundRow(row)) return '—';
            const site = row as Site;
            if (site.lastPublishedDate == null) {
              return 'Before January 2026';
            }
            const d = new Date(site.lastPublishedDate);
            if (site.lastPublishedDateIsMonthOnly) {
              return d.toLocaleDateString('en-GB', { month: 'long', year: 'numeric' });
            }
            const day = String(d.getUTCDate()).padStart(2, '0');
            const month = String(d.getUTCMonth() + 1).padStart(2, '0');
            const year = d.getUTCFullYear();
            return `${day}.${month}.${year}`;
          },
        },
        {
          ...gridColumnDefaults('createdAt', columnWidths),
          field: 'createdAt',
          valueFormatter: (value, row) => {
            const site = row as Site;
            return formatAuditDate(row, (value as string | null | undefined) ?? site.createdAtUtc);
          },
        },
        ...(!isClient
          ? [
              {
                ...gridColumnDefaults('quarantineReason', columnWidths),
                field: 'quarantineReason',
                sortable: false,
                valueFormatter: (_value, row) => {
                  if (isNotFoundRow(row)) return '—';
                  const site = row as Site;
                  return site.isQuarantined ? site.quarantineReason || '—' : '—';
                },
              } as GridColDef<GridRow>,
              {
                ...gridColumnDefaults('updatedAt', columnWidths),
                field: 'updatedAt',
                valueFormatter: (value, row) => {
                  const site = row as Site;
                  return formatAuditDate(row, (value as string | null | undefined) ?? site.updatedAtUtc);
                },
              } as GridColDef<GridRow>,
              {
                ...gridColumnDefaults('createdBy', columnWidths),
                field: 'createdBy',
                sortable: false,
                valueFormatter: (value, row) =>
                  formatAuditUser(row, value as string | null | undefined),
              } as GridColDef<GridRow>,
              {
                ...gridColumnDefaults('updatedBy', columnWidths),
                field: 'updatedBy',
                sortable: false,
                valueFormatter: (value, row) =>
                  formatAuditUser(row, value as string | null | undefined),
              } as GridColDef<GridRow>,
            ]
          : []),
        ...(isAdmin
          ? [
              {
                ...gridColumnDefaults('actions', columnWidths),
                field: 'actions',
                headerName: '',
                sortable: false,
                width: 56,
                minWidth: 56,
                maxWidth: 56,
                align: 'center',
                headerAlign: 'center',
                renderCell: (params: { row: GridRow }) => {
                  if (isNotFoundRow(params.row)) return null;
                  return (
                    <Tooltip title="Edit site">
                      <IconButton
                        size="small"
                        aria-label={`Edit ${params.row.domain}`}
                        onClick={() => onEdit(params.row as Site)}
                        sx={{
                          color: 'text.secondary',
                          '&:hover': {
                            bgcolor: 'action.hover',
                            color: 'text.primary',
                          },
                        }}
                      >
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  );
                },
              } as GridColDef<GridRow>,
            ]
          : []),
      ];

      const columnsByField = new Map(allColumns.map((column) => [column.field, column]));
      const visibleColumnSet = new Set(visibleColumnIds);
      const registryOrderedFields = sitesColumnRegistry
        .map((column) => column.id)
        .filter((field) => field !== 'actions' && columnsByField.has(field));
      const orderedFields = [
        ...visibleColumnIds.filter((field) => columnsByField.has(field)),
        ...registryOrderedFields.filter((field) => !visibleColumnSet.has(field)),
        ...(columnsByField.has('actions') ? ['actions'] : []),
      ];

      return orderedFields
        .map((field) => columnsByField.get(field))
        .filter((column): column is GridColDef<GridRow> => Boolean(column));
    },
    [columnWidths, isAdmin, isClient, onEdit, visibleColumnIds]
  );
}
