import { Box, Typography } from '@mui/material';
import type {
  WebmasterOffer,
  WebmasterOfferPrice,
  WebmasterOfferPriceType,
} from '../../types/webmasterOffers.types';
import {
  SERVICE_AVAILABILITY_STATUS,
  normalizeServiceAvailabilityStatus,
} from '../../utils/serviceAvailability';

interface PriceTypeColumn {
  readonly label: string;
  readonly stringValue: Exclude<WebmasterOfferPriceType, number>;
  readonly numericValue: number;
  readonly width?: number;
}

const PRICE_TYPE_COLUMNS: readonly PriceTypeColumn[] = [
  { label: 'Price USD', stringValue: 'Main', numericValue: 0 },
  { label: 'Casino', stringValue: 'Casino', numericValue: 1 },
  { label: 'Crypto', stringValue: 'Crypto', numericValue: 2 },
  { label: 'Dating', stringValue: 'Dating', numericValue: 3 },
  { label: 'Link Insert', stringValue: 'LinkInsertion', numericValue: 4 },
  { label: 'Link Insert 18+', stringValue: 'LinkInsertion18Plus', numericValue: 5 },
  { label: 'Banner', stringValue: 'Banner', numericValue: 6 },
  { label: 'Banner 18+', stringValue: 'Banner18Plus', numericValue: 7 },
  { label: 'Homepage Text Link', stringValue: 'HomepageTextLink', numericValue: 8 },
  {
    label: 'Homepage Text Link 18+',
    stringValue: 'HomepageTextLink18Plus',
    numericValue: 9,
    width: 210,
  },
];

const DEFAULT_PRICE_COLUMN_WIDTH = 150;
export const PRICE_GRID_TEMPLATE = PRICE_TYPE_COLUMNS
  .map((column) => `${column.width ?? DEFAULT_PRICE_COLUMN_WIDTH}px`)
  .join(' ');
export const PRICE_MATRIX_MIN_WIDTH = PRICE_TYPE_COLUMNS.reduce(
  (total, column) => total + (column.width ?? DEFAULT_PRICE_COLUMN_WIDTH),
  0
);

function formatCurrency(value: number | null) {
  if (value == null) return null;

  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 2,
  }).format(value);
}

function findPrice(offer: WebmasterOffer, column: PriceTypeColumn) {
  return offer.prices.find(
    (price) => price.priceType === column.stringValue || price.priceType === column.numericValue
  );
}

function formatRawPriceValue(price: WebmasterOfferPrice | undefined) {
  if (!price) return '—';

  const status = normalizeServiceAvailabilityStatus(price.availabilityStatus);
  if (status === SERVICE_AVAILABILITY_STATUS.NotAvailable) return 'NO';
  if (status === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice) return 'YES';
  if (status === SERVICE_AVAILABILITY_STATUS.Available) {
    return formatCurrency(price.webmasterPriceUsd) ?? '—';
  }

  return '—';
}

function priceValueColor(price: WebmasterOfferPrice | undefined) {
  const status = price
    ? normalizeServiceAvailabilityStatus(price.availabilityStatus)
    : SERVICE_AVAILABILITY_STATUS.Unknown;
  if (status === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice) return 'success.main';
  if (status === SERVICE_AVAILABILITY_STATUS.NotAvailable) return 'error.main';
  if (status === SERVICE_AVAILABILITY_STATUS.Unknown) return 'text.secondary';
  if (status === SERVICE_AVAILABILITY_STATUS.Available && price?.webmasterPriceUsd == null) {
    return 'text.secondary';
  }
  return 'text.primary';
}

function PriceCell({ price }: { readonly price: WebmasterOfferPrice | undefined }) {
  const details = price?.webmasterPriceDetails?.trim();
  const primary = formatRawPriceValue(price);
  const isEmpty = primary === '—';
  const hideEmptyPrimary = isEmpty && Boolean(details);

  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        px: 1.25,
        py: 1,
        minWidth: 0,
      }}
    >
      {!hideEmptyPrimary && (
        <Typography
          variant="body2"
          sx={{ fontWeight: isEmpty ? 400 : 600, color: priceValueColor(price), lineHeight: 1.2 }}
        >
          {primary}
        </Typography>
      )}
      {details && (
        <Typography
          variant="caption"
          color="text.secondary"
          sx={{
            display: 'block',
            mt: hideEmptyPrimary ? 0 : 0.25,
            whiteSpace: 'pre-wrap',
            overflowWrap: 'anywhere',
            lineHeight: 1.25,
          }}
        >
          {details}
        </Typography>
      )}
    </Box>
  );
}

export function WebmasterOfferPriceHeader() {
  return (
    <>
      {PRICE_TYPE_COLUMNS.map((column) => (
        <Box
          key={column.stringValue}
          sx={{ display: 'flex', alignItems: 'center', px: 1.25, py: 1, minWidth: 0 }}
        >
          <Typography
            variant="body2"
            sx={{ fontWeight: 600, lineHeight: 1.25, whiteSpace: 'nowrap' }}
          >
            {column.label}
          </Typography>
        </Box>
      ))}
    </>
  );
}

export function WebmasterOfferPriceCells({ offer }: { readonly offer: WebmasterOffer }) {
  return (
    <>
      {PRICE_TYPE_COLUMNS.map((column) => (
        <PriceCell key={column.stringValue} price={findPrice(offer, column)} />
      ))}
    </>
  );
}
