import { useMemo } from 'react';
import { Box, Chip, IconButton, Tooltip, Typography } from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import type { GridColDef } from '@mui/x-data-grid';
import type { Site } from '../../../types/sites.types';
import type { TableViewDensity } from '../../../types/tableViews.types';
import { formatLanguageTableValue } from '../../../utils/language';
import { formatTerm } from '../../../utils/term';
import {
  PRICE_FIELD_TO_TYPE,
  PRICE_TYPE,
  type PricingCellSummary,
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
  density: TableViewDensity;
  onEdit: (site: Site) => void;
  onViewPricing: (site: Site) => void;
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

function renderPricingTooltipContent(label: string, summary: PricingCellSummary) {
  if (
    summary.tooltipRows.length === 0 &&
    !summary.statusLabel &&
    summary.primary === '—'
  ) {
    return '';
  }

  return (
    <Box sx={{ minWidth: 180 }}>
      <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 700 }}>
        {label}
      </Typography>
      {summary.tooltipRows.length > 0 ? (
        <Box sx={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) auto', gap: 1.5 }}>
          {summary.tooltipRows.map((row) => (
            <Box
              key={`${row.termLabel}:${row.amount}`}
              sx={{
                display: 'contents',
              }}
            >
              <Typography variant="body2" color="text.secondary">
                {row.termLabel}
              </Typography>
              <Typography variant="body2" sx={{ justifySelf: 'end', fontWeight: 600 }}>
                {row.amount}
              </Typography>
            </Box>
          ))}
        </Box>
      ) : (
        <Box>
          {summary.statusLabel && (
            <Typography variant="body2" sx={{ fontWeight: 700 }}>
              {summary.statusLabel}
            </Typography>
          )}
          {summary.statusHelper && (
            <Typography variant="body2" color="text.secondary">
              {summary.statusHelper}
            </Typography>
          )}
        </Box>
      )}
    </Box>
  );
}

function renderHiddenCountChip(count: number) {
  if (count <= 0) return null;

  return (
    <Chip
      component="span"
      label={`+${count}`}
      size="small"
      variant="outlined"
      sx={{
        height: 18,
        minWidth: 0,
        flexShrink: 0,
        borderColor: 'divider',
        bgcolor: 'action.hover',
        color: 'text.secondary',
        fontSize: '0.6875rem',
        fontWeight: 600,
        '& .MuiChip-label': {
          px: 0.75,
        },
      }}
    />
  );
}

function renderStandardPriceDetails(summary: PricingCellSummary) {
  if (summary.snippets.length > 0) {
    return (
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 0.5,
          minWidth: 0,
          overflow: 'hidden',
          whiteSpace: 'nowrap',
          lineHeight: 1.25,
        }}
      >
        {summary.snippets.map((snippet, index) => (
          <Box
            key={snippet}
            component="span"
            sx={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 0.5,
              minWidth: 0,
              flexShrink: index === summary.snippets.length - 1 ? 1 : 0,
              whiteSpace: 'nowrap',
            }}
          >
            {index > 0 && (
              <Box component="span" sx={{ color: 'text.disabled', flexShrink: 0 }}>
                ·
              </Box>
            )}
            <Typography
              component="span"
              variant="caption"
              color="text.secondary"
              sx={{
                display: 'inline',
                minWidth: 0,
                overflow: 'hidden',
                whiteSpace: 'nowrap',
                lineHeight: 1.25,
              }}
            >
              {snippet}
            </Typography>
          </Box>
        ))}
        {summary.hiddenCount > 0 && (
          <>
            <Box component="span" sx={{ color: 'text.disabled', flexShrink: 0 }}>
              ·
            </Box>
            {renderHiddenCountChip(summary.hiddenCount)}
          </>
        )}
      </Box>
    );
  }

  if (!summary.secondary) return null;

  return (
    <Typography
      variant="caption"
      color="text.secondary"
      noWrap
      sx={{
        display: 'block',
        lineHeight: 1.25,
        whiteSpace: 'nowrap',
      }}
    >
      {summary.secondary}
    </Typography>
  );
}

function renderCompactPriceLine(summary: PricingCellSummary) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, minWidth: 0 }}>
      <Typography
        component="span"
        variant="body2"
        noWrap
        sx={{ minWidth: 0, fontWeight: 600, lineHeight: 1.2 }}
      >
        {summary.primary}
      </Typography>
      {renderHiddenCountChip(summary.compactHiddenCount)}
    </Box>
  );
}

function renderPriceCell(
  summary: PricingCellSummary,
  label: string,
  site: Site,
  onViewPricing: (site: Site) => void,
  density: TableViewDensity
) {
  const isCompact = density === 'compact';

  return (
    <Tooltip
      title={renderPricingTooltipContent(label, summary)}
      arrow
      slotProps={{
        tooltip: {
          sx: {
            bgcolor: 'background.paper',
            color: 'text.primary',
            boxShadow: 3,
            border: 1,
            borderColor: 'divider',
            p: 1.25,
            maxWidth: 360,
          },
        },
        arrow: {
          sx: {
            color: 'background.paper',
          },
        },
      }}
    >
      <Box
        component="button"
        type="button"
        aria-label={`Open ${label} pricing details for ${site.domain}`}
        onClick={(event) => {
          event.stopPropagation();
          onViewPricing(site);
        }}
        sx={{
          minWidth: 0,
          width: '100%',
          overflow: 'hidden',
          lineHeight: 1.2,
          p: 0,
          m: 0,
          border: 0,
          bgcolor: 'transparent',
          color: 'inherit',
          textAlign: 'left',
          cursor: 'pointer',
          font: 'inherit',
          display: 'block',
          '&:focus-visible': {
            outline: '2px solid',
            outlineColor: 'primary.main',
            outlineOffset: 2,
            borderRadius: 0.5,
          },
        }}
      >
        {isCompact ? (
          renderCompactPriceLine(summary)
        ) : (
          <>
            <Typography
              variant="body2"
              noWrap
              sx={{ display: 'block', fontWeight: 600, lineHeight: 1.2 }}
            >
              {summary.primary}
            </Typography>
            {renderStandardPriceDetails(summary)}
          </>
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
  density,
  onEdit,
  onViewPricing,
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
              : renderPriceCell(
                  formatMainPriceCell(params.row as Site),
                  'Price USD',
                  params.row as Site,
                  onViewPricing,
                  density
                ),
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
                  formatOptionalServicePriceCell(
                    params.row as Site,
                    PRICE_FIELD_TO_TYPE.priceCasino
                  ),
                  'Casino',
                  params.row as Site,
                  onViewPricing,
                  density
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
                  formatOptionalServicePriceCell(
                    params.row as Site,
                    PRICE_FIELD_TO_TYPE.priceCrypto
                  ),
                  'Crypto',
                  params.row as Site,
                  onViewPricing,
                  density
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
                  formatOptionalServicePriceCell(params.row as Site, PRICE_TYPE.LinkInsertion),
                  'Link Insert',
                  params.row as Site,
                  onViewPricing,
                  density
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
                  ),
                  'Link Insert Casino',
                  params.row as Site,
                  onViewPricing,
                  density
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
                  formatOptionalServicePriceCell(
                    params.row as Site,
                    PRICE_FIELD_TO_TYPE.priceDating
                  ),
                  'Dating',
                  params.row as Site,
                  onViewPricing,
                  density
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
        {
          ...gridColumnDefaults('actions', columnWidths),
          field: 'actions',
          headerName: '',
          sortable: false,
          width: 48,
          minWidth: 48,
          maxWidth: 48,
          align: 'center',
          headerAlign: 'center',
          renderCell: (params: { row: GridRow }) => {
            if (isNotFoundRow(params.row)) return null;
            const site = params.row as Site;
            return (
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.25 }}>
                {isAdmin && (
                  <Tooltip title="Edit site">
                    <IconButton
                      size="small"
                      aria-label={`Edit ${site.domain}`}
                      onClick={() => onEdit(site)}
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
                )}
              </Box>
            );
          },
        } as GridColDef<GridRow>,
      ];

      const columnsByField = new Map(allColumns.map((column) => [column.field, column]));
      const visibleColumnSet = new Set(visibleColumnIds);
      const registryOrderedFields = sitesColumnRegistry
        .map((column) => column.id)
        .filter((field) => field !== 'actions' && columnsByField.has(field));
      const orderedFields = [
        ...visibleColumnIds.filter((field) => columnsByField.has(field)),
        ...registryOrderedFields.filter((field) => !visibleColumnSet.has(field)),
        ...(isAdmin && columnsByField.has('actions') ? ['actions'] : []),
      ];

      return orderedFields
        .map((field) => columnsByField.get(field))
        .filter((column): column is GridColDef<GridRow> => Boolean(column));
    },
    [columnWidths, density, isAdmin, isClient, onEdit, onViewPricing, visibleColumnIds]
  );
}
