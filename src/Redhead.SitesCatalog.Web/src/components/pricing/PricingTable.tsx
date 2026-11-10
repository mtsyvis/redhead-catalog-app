import { PAGE_SIZE_OPTIONS } from '../../constants/pagination.constants';
import type { ReactNode } from 'react';
import type { TableViewDensity } from '../../types/tableViews.types';
import {
  Box,
  IconButton,
  LinearProgress,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TableSortLabel,
  Tooltip,
  Typography,
} from '@mui/material';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import { WebmasterOffersLink } from '../webmaster-offers/WebmasterOffersLink';
import type { PricingPage, PricingQuery, PricingSite } from '../../types/pricing.types';
import { pricingColumns } from './pricingColumns';
import { marketDescription } from '../../utils/pricingDisplay';
import { PRICE_TYPE, type PriceTypeValue } from '../../utils/pricing';
import { formatOptionalServicePrice } from '../../utils/serviceAvailability';
import { formatInteger, formatUsd } from '../../utils/numberFormat';
import { MarginAmount } from '../common/MarginAmount';
import { UnavailableSiteIndicator } from '../common/UnavailableSiteIndicator';
import {
  getPinnedTableCellStyles,
  getTableSurfaceStyles,
  getUnavailableTableRowStyles,
} from '../../theme/tableStyles';

function CatalogPrice({ site, type }: { site: PricingSite; type: PriceTypeValue }) {
  const price = site.prices.find(
    (row) => row.priceType === type && row.termKey === site.terms[0]?.termKey
  );
  const status =
    site.pricing.serviceAvailabilities.find((row) => row.serviceType === type)?.status ?? 0;
  const label =
    price?.catalogPrice != null
      ? formatUsd(price.catalogPrice)
      : type === PRICE_TYPE.Main
        ? '—'
        : formatOptionalServicePrice(status, null);
  return (
    <Box>
      <Typography variant="body2" fontWeight={600} sx={{ fontVariantNumeric: 'tabular-nums' }}>
        {label}
      </Typography>
      {price?.catalogPrice != null && (
        <MarginAmount
          amount={price.margin}
          tooltip={
            price.offerPrice == null
              ? 'No matching Active numeric offer for this service and term'
              : `Offer price ${formatUsd(price.offerPrice)} · ${site.terms[0]?.label} · Active offer`
          }
        />
      )}
    </Box>
  );
}

export function PricingTable({
  data,
  query,
  loading,
  columnIds,
  canEdit,
  toolbar,
  density,
  columnWidths,
  onQuery,
  onEdit,
}: {
  data: PricingPage | null;
  query: PricingQuery;
  loading: boolean;
  columnIds: string[];
  canEdit: boolean;
  toolbar: ReactNode;
  density: TableViewDensity;
  columnWidths: Record<string, number>;
  onQuery: (next: Partial<PricingQuery>) => void;
  onEdit: (domain: string, termKey?: string) => void;
}) {
  const columns = columnIds
    .map((id) => pricingColumns.find((column) => column.id === id)!)
    .filter(Boolean);
  const sort = (priceType?: PriceTypeValue) => {
    const same =
      priceType == null
        ? query.sortBy === 'domain'
        : query.sortBy === 'margin' && query.sortPriceType === priceType;
    onQuery({
      sortBy: priceType == null ? 'domain' : 'margin',
      sortPriceType: priceType ?? query.sortPriceType,
      descending: same ? !query.descending : false,
      page: 1,
    });
  };
  return (
    <Box
      sx={(theme) => ({
        ...getTableSurfaceStyles(theme),
        '& tbody .MuiTableCell-root': {
          py: `${density === 'compact' ? Math.max(0, theme.custom.table.cellPaddingY - 3) : density === 'comfortable' ? theme.custom.table.cellPaddingY + 4 : theme.custom.table.cellPaddingY}px`,
        },
      })}
    >
      {toolbar}
      {loading && <LinearProgress aria-label="Loading catalog pricing" />}
      <TableContainer sx={{ maxWidth: '100%' }}>
        <Table size="small" stickyHeader aria-label="Catalog pricing" aria-busy={loading}>
          <TableHead>
            <TableRow>
              <TableCell
                sx={(theme) => ({
                  ...getPinnedTableCellStyles(theme, true),
                  minWidth: columnWidths.domain ?? 300,
                })}
              >
                <TableSortLabel
                  active={query.sortBy === 'domain'}
                  direction={query.descending ? 'desc' : 'asc'}
                  onClick={() => sort()}
                >
                  Domain
                </TableSortLabel>
              </TableCell>
              <TableCell sx={{ width: columnWidths.term ?? 100, minWidth: columnWidths.term ?? 100 }}>Term</TableCell>
              {columns.map((column) => (
                <TableCell
                  key={column.id}
                  align="right"
                  sx={{ minWidth: columnWidths[column.id] ?? 145 }}
                >
                  {column.priceType == null ? (
                    <Tooltip title="Saved LinkDetective market reference. Service and placement term are unspecified.">
                      <span tabIndex={0}>{column.label}</span>
                    </Tooltip>
                  ) : (
                    <Tooltip title={`Sort by ${column.label} margin`}>
                      <TableSortLabel
                        active={
                          query.sortBy === 'margin' && query.sortPriceType === column.priceType
                        }
                        direction={query.descending ? 'desc' : 'asc'}
                        onClick={() => sort(column.priceType)}
                      >
                        {column.label}
                      </TableSortLabel>
                    </Tooltip>
                  )}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {data?.items.map((site) => (
              <TableRow
                key={`${site.domain}:${site.terms[0]?.termKey ?? ''}`}
                hover
                sx={
                  site.isQuarantined
                    ? (theme) =>
                        getUnavailableTableRowStyles(
                          theme,
                          '.MuiTableCell-root',
                          '.PricingTable-domainCell'
                        )
                    : undefined
                }
              >
                <TableCell
                  component="th"
                  scope="row"
                  className="PricingTable-domainCell"
                  sx={(theme) => ({
                    ...getPinnedTableCellStyles(theme),
                    minWidth: columnWidths.domain ?? 300,
                    // Keep the divider inside the sticky cell when row coordinates are fractional.
                    borderBottom: 0,
                    '&::after': {
                      content: '""',
                      position: 'absolute',
                      left: 0,
                      right: 0,
                      bottom: 0,
                      borderBottom: `1px solid ${theme.palette.divider}`,
                      pointerEvents: 'none',
                    },
                  })}
                >
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, minWidth: 0 }}>
                    {site.isQuarantined && <UnavailableSiteIndicator />}
                    <Typography
                      variant="body2"
                      fontWeight={600}
                      sx={{ flex: 1, minWidth: 0, overflowWrap: 'anywhere' }}
                    >
                      {site.domain}
                    </Typography>
                    <Tooltip title="Edit catalog prices">
                      <Box component="span" sx={{ display: 'inline-flex', flexShrink: 0 }}>
                        <IconButton
                          disabled={!canEdit}
                          aria-label={`Edit prices for ${site.domain}`}
                          onClick={() => onEdit(site.domain, site.terms[0]?.termKey)}
                          sx={{ width: 36, height: 36 }}
                        >
                          <EditOutlinedIcon fontSize="small" />
                        </IconButton>
                      </Box>
                    </Tooltip>
                    <WebmasterOffersLink domain={site.domain} />
                  </Box>
                </TableCell>
                <TableCell>
                  <Typography variant="body2">{site.terms[0]?.label ?? '—'}</Typography>
                </TableCell>
                {columns.map((column) => (
                  <TableCell key={column.id} align="right">
                    {column.priceType != null ? (
                      <CatalogPrice site={site} type={column.priceType} />
                    ) : (
                      <Tooltip title={marketDescription(site.market)}>
                        <Box tabIndex={0}>
                          <Typography variant="body2" sx={{ fontVariantNumeric: 'tabular-nums' }}>
                            {formatUsd(site.market?.[column.marketField!])}
                          </Typography>
                          {(site.market?.isStale || site.market?.lastError) && (
                            <Typography variant="caption" color="warning.dark">
                              {site.market.lastError ? 'Check failed' : 'Outdated'}
                            </Typography>
                          )}
                        </Box>
                      </Tooltip>
                    )}
                  </TableCell>
                ))}
              </TableRow>
            ))}
            {data?.items.length === 0 && !loading && (
              <TableRow>
                <TableCell colSpan={columns.length + 2} align="center">
                  No sites match these filters.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>
      <TablePagination
        component="div"
        count={data?.totalCount ?? 0}
        page={query.page - 1}
        rowsPerPage={query.pageSize}
        rowsPerPageOptions={PAGE_SIZE_OPTIONS}
        onPageChange={(_, page) => onQuery({ page: page + 1 })}
        onRowsPerPageChange={(event) => onQuery({ pageSize: Number(event.target.value), page: 1 })}
        labelDisplayedRows={({ from, to, count }) =>
          `${formatInteger(from)}–${formatInteger(to)} of ${formatInteger(count)}`
        }
      />
    </Box>
  );
}
