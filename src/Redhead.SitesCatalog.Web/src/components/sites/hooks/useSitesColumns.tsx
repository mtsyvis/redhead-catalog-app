import { useMemo } from 'react';
import { Box, IconButton, Tooltip, Typography } from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import type { GridColDef } from '@mui/x-data-grid';
import type { Site } from '../../../types/sites.types';
import { formatLanguageTableValue } from '../../../utils/language';
import { formatTerm } from '../../../utils/term';
import {
  PRICE_FIELD_TO_TYPE,
  PRICE_TYPE,
  formatMainPriceCell,
  formatOptionalServicePriceCell,
} from '../../../utils/pricing';
import { StatusBadge } from '../feedback/StatusBadge';
import { TruncatedTextCell } from '../cells/TruncatedTextCell';
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

function renderTruncatedTextCell(value: string) {
  return <TruncatedTextCell value={value} />;
}

function renderPriceCell(summary: { primary: string; secondary: string | null; title: string }) {
  return (
    <Tooltip title={summary.title} disableInteractive>
      <Box sx={{ minWidth: 0, overflow: 'hidden', lineHeight: 1.2 }}>
        <Typography variant="body2" noWrap sx={{ fontWeight: 600, lineHeight: 1.2 }}>
          {summary.primary}
        </Typography>
        {summary.secondary && (
          <Typography variant="caption" color="text.secondary" noWrap sx={{ display: 'block' }}>
            {summary.secondary}
          </Typography>
        )}
      </Box>
    </Tooltip>
  );
}

const columnMetadata: Record<string, SitesColumnMetadata> = Object.fromEntries(
  sitesColumnRegistry.map((column) => [column.id, column])
);

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
          cellClassName: 'SitesGrid-domainCell',
          headerClassName: 'SitesGrid-domainHeader',
          renderCell: (params) => renderTruncatedTextCell(String(params.value ?? '—')),
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
          renderCell: (params) =>
            renderTruncatedTextCell(formatLocationCell(params.row, params.value as string | null)),
        },
        {
          ...gridColumnDefaults('priceUsd', columnWidths),
          field: 'priceUsd',
          type: 'number',
          valueFormatter: (_value, row) =>
            isNotFoundRow(row) ? '—' : formatMainPriceCell(row as Site).primary,
          renderCell: (params) =>
            isNotFoundRow(params.row)
              ? renderTruncatedTextCell('—')
              : renderPriceCell(formatMainPriceCell(params.row as Site)),
        },
        {
          ...gridColumnDefaults('priceCasino', columnWidths),
          field: 'priceCasino',
          type: 'number',
          valueFormatter: (_value, row) =>
            isNotFoundRow(row)
              ? '—'
              : formatOptionalServicePriceCell(row as Site, PRICE_FIELD_TO_TYPE.priceCasino)
                  .primary,
          renderCell: (params) =>
            isNotFoundRow(params.row)
              ? renderTruncatedTextCell('—')
              : renderPriceCell(
                  formatOptionalServicePriceCell(params.row as Site, PRICE_FIELD_TO_TYPE.priceCasino)
                ),
        },
        {
          ...gridColumnDefaults('priceCrypto', columnWidths),
          field: 'priceCrypto',
          type: 'number',
          valueFormatter: (_value, row) =>
            isNotFoundRow(row)
              ? '—'
              : formatOptionalServicePriceCell(row as Site, PRICE_FIELD_TO_TYPE.priceCrypto)
                  .primary,
          renderCell: (params) =>
            isNotFoundRow(params.row)
              ? renderTruncatedTextCell('—')
              : renderPriceCell(
                  formatOptionalServicePriceCell(params.row as Site, PRICE_FIELD_TO_TYPE.priceCrypto)
                ),
        },
        {
          ...gridColumnDefaults('priceLinkInsert', columnWidths),
          field: 'priceLinkInsert',
          type: 'number',
          valueFormatter: (_value, row) =>
            isNotFoundRow(row)
              ? '—'
              : formatOptionalServicePriceCell(row as Site, PRICE_TYPE.LinkInsertion).primary,
          renderCell: (params) =>
            isNotFoundRow(params.row)
              ? renderTruncatedTextCell('—')
              : renderPriceCell(
                  formatOptionalServicePriceCell(params.row as Site, PRICE_TYPE.LinkInsertion)
                ),
        },
        {
          ...gridColumnDefaults('priceLinkInsertCasino', columnWidths),
          field: 'priceLinkInsertCasino',
          type: 'number',
          valueFormatter: (_value, row) =>
            isNotFoundRow(row)
              ? '—'
              : formatOptionalServicePriceCell(row as Site, PRICE_TYPE.LinkInsertionCasino)
                  .primary,
          renderCell: (params) =>
            isNotFoundRow(params.row)
              ? renderTruncatedTextCell('—')
              : renderPriceCell(
                  formatOptionalServicePriceCell(
                    params.row as Site,
                    PRICE_TYPE.LinkInsertionCasino
                  )
                ),
        },
        {
          ...gridColumnDefaults('priceDating', columnWidths),
          field: 'priceDating',
          type: 'number',
          valueFormatter: (_value, row) =>
            isNotFoundRow(row)
              ? '—'
              : formatOptionalServicePriceCell(row as Site, PRICE_FIELD_TO_TYPE.priceDating)
                  .primary,
          renderCell: (params) =>
            isNotFoundRow(params.row)
              ? renderTruncatedTextCell('—')
              : renderPriceCell(
                  formatOptionalServicePriceCell(params.row as Site, PRICE_FIELD_TO_TYPE.priceDating)
                ),
        },
        {
          ...gridColumnDefaults('niche', columnWidths),
          field: 'niche',
          sortable: false,
          valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
          renderCell: (params) =>
            renderTruncatedTextCell(
              formatCell(params.row, params.value as string | null, (v) => v || '—')
            ),
        },
        {
          ...gridColumnDefaults('categories', columnWidths),
          field: 'categories',
          sortable: false,
          valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
          renderCell: (params) =>
            renderTruncatedTextCell(
              formatCell(params.row, params.value as string | null, (v) => v || '—')
            ),
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
          renderCell: (params) =>
            renderTruncatedTextCell(
              formatCell(params.row, params.value as string | null, (v) => v || '—')
            ),
        },
        {
          ...gridColumnDefaults('term', columnWidths),
          field: 'term',
          valueFormatter: (_value, row) => {
            if (isNotFoundRow(row)) return '—';
            const site = row as Site;
            return formatTerm(site.termType, site.termValue, site.termUnit);
          },
          renderCell: (params) => {
            if (isNotFoundRow(params.row)) return renderTruncatedTextCell('—');
            const site = params.row as Site;
            return renderTruncatedTextCell(formatTerm(site.termType, site.termValue, site.termUnit));
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
          renderCell: (params) => {
            if (isNotFoundRow(params.row)) return renderTruncatedTextCell('—');
            return renderTruncatedTextCell(formatLanguageTableValue(params.value as string | null));
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
              return '-';
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
                renderCell: (params) => {
                  if (isNotFoundRow(params.row)) return renderTruncatedTextCell('—');
                  const site = params.row as Site;
                  return renderTruncatedTextCell(
                    site.isQuarantined ? site.quarantineReason || '—' : '—'
                  );
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
                renderCell: (params) =>
                  renderTruncatedTextCell(
                    formatAuditUser(params.row, params.value as string | null | undefined)
                  ),
              } as GridColDef<GridRow>,
              {
                ...gridColumnDefaults('updatedBy', columnWidths),
                field: 'updatedBy',
                sortable: false,
                valueFormatter: (value, row) =>
                  formatAuditUser(row, value as string | null | undefined),
                renderCell: (params) =>
                  renderTruncatedTextCell(
                    formatAuditUser(params.row, params.value as string | null | undefined)
                  ),
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
