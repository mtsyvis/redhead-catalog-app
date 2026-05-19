import { useMemo } from 'react';
import type { GridColDef } from '@mui/x-data-grid';
import { BrandButton } from '../common/BrandButton';
import type { Site } from '../../types/sites.types';
import { formatLanguageTableValue } from '../../utils/language';
import { formatOptionalServicePrice } from '../../utils/serviceAvailability';
import { formatTerm } from '../../utils/term';
import { StatusBadge } from './StatusBadge';
import type { GridRow } from './useSitesGridRows';
import { isNotFoundRow } from './useSitesGridRows';

interface UseSitesColumnsOptions {
  isAdmin: boolean;
  isClient: boolean;
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

export function useSitesColumns({ isAdmin, isClient, onEdit }: UseSitesColumnsOptions) {
  return useMemo<GridColDef<GridRow>[]>(
    () => [
      {
        field: 'domain',
        headerName: 'Domain',
        flex: 1,
        minWidth: 200,
      },
      {
        field: 'dr',
        headerName: 'DR',
        width: 80,
        type: 'number',
        valueFormatter: (value, row) =>
          formatCell(row, value as number | null, (v) => (v == null ? '' : String(v))),
      },
      {
        field: 'traffic',
        headerName: 'Traffic',
        width: 120,
        type: 'number',
        valueFormatter: (value, row) => {
          if (isNotFoundRow(row)) return '—';
          if (value == null) return '';
          return new Intl.NumberFormat('en-US').format(value as number);
        },
      },
      {
        field: 'location',
        headerName: 'Location',
        width: 120,
        valueFormatter: (value, row) => formatCell(row, value as string, (v) => v ?? '—'),
      },
      {
        field: 'priceUsd',
        headerName: 'Price USD',
        width: 100,
        type: 'number',
        valueFormatter: (value, row) => formatPrice(row, value as number | null),
      },
      {
        field: 'priceCasino',
        headerName: 'Casino',
        width: 100,
        type: 'number',
        valueFormatter: (value, row) =>
          formatOptionalServiceCell(row, value as number | null, (row as Site).priceCasinoStatus),
      },
      {
        field: 'priceCrypto',
        headerName: 'Crypto',
        width: 100,
        type: 'number',
        valueFormatter: (value, row) =>
          formatOptionalServiceCell(row, value as number | null, (row as Site).priceCryptoStatus),
      },
      {
        field: 'priceLinkInsert',
        headerName: 'Link Insert',
        width: 100,
        type: 'number',
        valueFormatter: (value, row) =>
          formatOptionalServiceCell(row, value as number | null, (row as Site).priceLinkInsertStatus),
      },
      {
        field: 'priceLinkInsertCasino',
        headerName: 'Link Insert Casino',
        width: 150,
        type: 'number',
        valueFormatter: (value, row) =>
          formatOptionalServiceCell(row, value as number | null, (row as Site).priceLinkInsertCasinoStatus),
      },
      {
        field: 'priceDating',
        headerName: 'Dating',
        width: 100,
        type: 'number',
        valueFormatter: (value, row) =>
          formatOptionalServiceCell(row, value as number | null, (row as Site).priceDatingStatus),
      },
      {
        field: 'niche',
        headerName: 'Niche',
        width: 150,
        sortable: false,
        valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
      },
      {
        field: 'categories',
        headerName: 'Categories',
        width: 150,
        sortable: false,
        valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
      },
      {
        field: 'numberDFLinks',
        headerName: 'DF Links',
        width: 110,
        type: 'number',
        valueFormatter: (value, row) => formatNullableInteger(row, value as number | null),
      },
      {
        field: 'sponsoredTag',
        headerName: 'Sponsored Tag',
        width: 150,
        sortable: false,
        valueFormatter: (value, row) => formatCell(row, value as string | null, (v) => v || '—'),
      },
      {
        field: 'term',
        headerName: 'Term',
        width: 120,
        valueFormatter: (_value, row) => {
          if (isNotFoundRow(row)) return '—';
          const site = row as Site;
          return formatTerm(site.termType, site.termValue, site.termUnit);
        },
      },
      {
        field: 'language',
        headerName: 'Language',
        width: 100,
        sortable: false,
        valueFormatter: (value, row) => {
          if (isNotFoundRow(row)) return '—';
          return formatLanguageTableValue(value as string | null);
        },
      },
      {
        field: 'isQuarantined',
        headerName: 'Status',
        width: 110,
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
        field: 'lastPublishedDate',
        headerName: 'Last Published',
        width: 150,
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
              field: 'quarantineReason',
              headerName: 'Quarantine reason',
              width: 160,
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
              field: 'actions',
              headerName: 'Actions',
              width: 90,
              sortable: false,
              renderCell: (params: { row: GridRow }) => {
                if (isNotFoundRow(params.row)) return null;
                return (
                  <BrandButton
                    kind="outline"
                    size="small"
                    onClick={() => onEdit(params.row as Site)}
                  >
                    Edit
                  </BrandButton>
                );
              },
            } as GridColDef<GridRow>,
          ]
        : []),
    ],
    [isAdmin, isClient, onEdit]
  );
}
