import { useMemo } from 'react';
import { IconButton, Tooltip } from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import type { GridColDef } from '@mui/x-data-grid';
import type { Site } from '../../types/sites.types';
import { formatLanguageTableValue } from '../../utils/language';
import { formatOptionalServicePrice } from '../../utils/serviceAvailability';
import { formatTerm } from '../../utils/term';
import { StatusBadge } from './StatusBadge';
import type { GridRow } from './useSitesGridRows';
import { isNotFoundRow } from './useSitesGridRows';
import { sitesColumnRegistry } from './sitesTableColumns';

interface UseSitesColumnsOptions {
  isAdmin: boolean;
  isClient: boolean;
  visibleColumnIds: string[];
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

const columnMetadata = Object.fromEntries(sitesColumnRegistry.map((column) => [column.id, column]));

function gridColumnDefaults(
  field: string
): Pick<GridColDef<GridRow>, 'headerName' | 'width' | 'hideable'> {
  const metadata = columnMetadata[field];
  return {
    headerName: metadata?.label ?? field,
    width: metadata?.width,
    hideable: !metadata?.required && !metadata?.systemOnly,
  };
}

export function useSitesColumns({
  isAdmin,
  isClient,
  visibleColumnIds,
  onEdit,
}: UseSitesColumnsOptions) {
  return useMemo<GridColDef<GridRow>[]>(
    () => {
      const allColumns: GridColDef<GridRow>[] = [
        {
          ...gridColumnDefaults('domain'),
          field: 'domain',
          flex: 1,
          minWidth: 200,
        },
        {
          ...gridColumnDefaults('dr'),
          field: 'dr',
          type: 'number',
          valueFormatter: (value, row) =>
            formatCell(row, value as number | null, (v) => (v == null ? '' : String(v))),
        },
        {
          ...gridColumnDefaults('traffic'),
          field: 'traffic',
          type: 'number',
          valueFormatter: (value, row) => {
            if (isNotFoundRow(row)) return '—';
            if (value == null) return '';
            return new Intl.NumberFormat('en-US').format(value as number);
          },
        },
        {
          ...gridColumnDefaults('location'),
          field: 'location',
          valueFormatter: (value, row) => formatCell(row, value as string, (v) => v ?? '—'),
        },
        {
          ...gridColumnDefaults('priceUsd'),
          field: 'priceUsd',
          type: 'number',
          valueFormatter: (value, row) => formatPrice(row, value as number | null),
        },
        {
          ...gridColumnDefaults('priceCasino'),
          field: 'priceCasino',
          type: 'number',
          valueFormatter: (value, row) =>
            formatOptionalServiceCell(row, value as number | null, (row as Site).priceCasinoStatus),
        },
        {
          ...gridColumnDefaults('priceCrypto'),
          field: 'priceCrypto',
          type: 'number',
          valueFormatter: (value, row) =>
            formatOptionalServiceCell(row, value as number | null, (row as Site).priceCryptoStatus),
        },
        {
          ...gridColumnDefaults('priceLinkInsert'),
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
          ...gridColumnDefaults('priceLinkInsertCasino'),
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
          ...gridColumnDefaults('priceDating'),
          field: 'priceDating',
          type: 'number',
          valueFormatter: (value, row) =>
            formatOptionalServiceCell(row, value as number | null, (row as Site).priceDatingStatus),
        },
        {
          ...gridColumnDefaults('niche'),
          field: 'niche',
          sortable: false,
          valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
        },
        {
          ...gridColumnDefaults('categories'),
          field: 'categories',
          sortable: false,
          valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
        },
        {
          ...gridColumnDefaults('numberDFLinks'),
          field: 'numberDFLinks',
          type: 'number',
          valueFormatter: (value, row) => formatNullableInteger(row, value as number | null),
        },
        {
          ...gridColumnDefaults('sponsoredTag'),
          field: 'sponsoredTag',
          sortable: false,
          valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
        },
        {
          ...gridColumnDefaults('term'),
          field: 'term',
          valueFormatter: (_value, row) => {
            if (isNotFoundRow(row)) return '—';
            const site = row as Site;
            return formatTerm(site.termType, site.termValue, site.termUnit);
          },
        },
        {
          ...gridColumnDefaults('language'),
          field: 'language',
          sortable: false,
          valueFormatter: (value, row) => {
            if (isNotFoundRow(row)) return '—';
            return formatLanguageTableValue(value as string | null);
          },
        },
        {
          ...gridColumnDefaults('isQuarantined'),
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
          ...gridColumnDefaults('lastPublishedDate'),
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
        ...(!isClient
          ? [
              {
                ...gridColumnDefaults('quarantineReason'),
                field: 'quarantineReason',
                sortable: false,
                valueFormatter: (_value, row) => {
                  if (isNotFoundRow(row)) return '—';
                  const site = row as Site;
                  return site.isQuarantined ? site.quarantineReason || '—' : '—';
                },
              } as GridColDef<GridRow>,
            ]
          : []),
        ...(isAdmin
          ? [
              {
                ...gridColumnDefaults('actions'),
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
    [isAdmin, isClient, onEdit, visibleColumnIds]
  );
}
