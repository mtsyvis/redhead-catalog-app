import { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  LinearProgress,
  MenuItem,
  Paper,
  Snackbar,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import { useUserRoles } from '../../hooks/useUserRoles';
import { webmasterOffersService } from '../../services/webmasterOffers.service';
import { getTableSurfaceStyles } from '../../theme/tableStyles';
import type { WebmasterOfferPriceType } from '../../types/webmasterOffers.types';
import type {
  WebmasterComparisonOffer,
  WebmasterComparisonPrice,
  WebmasterComparisonTerm,
  WebmasterPriceComparison,
} from '../../types/webmasterPriceComparison.types';
import {
  normalizeServiceAvailabilityStatus,
  SERVICE_AVAILABILITY_STATUS,
} from '../../utils/serviceAvailability';
import { BrandButton } from '../common/BrandButton';
import { CopyToClipboardIconButton } from '../common/CopyToClipboardIconButton';
import { LinkDetectivePricesPanel } from './LinkDetectivePricesPanel';
import { OfferTermChip, OfferTermStatus } from './OfferTermStatus';

const columns = [
  { label: 'Price USD', name: 'Main', value: 0 },
  { label: 'Casino', name: 'Casino', value: 1 },
  { label: 'Crypto', name: 'Crypto', value: 2 },
  { label: 'Dating', name: 'Dating', value: 3 },
  { label: 'Link Insert', name: 'LinkInsertion', value: 4 },
  { label: 'Link Insert 18+', name: 'LinkInsertion18Plus', value: 5 },
] as const;
const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  maximumFractionDigits: 2,
});
const hasType = (price: { priceType: WebmasterOfferPriceType }, column: (typeof columns)[number]) =>
  price.priceType === column.name || price.priceType === column.value;

function PriceCell({
  price,
  term,
  offerTermLabel,
}: {
  price: WebmasterComparisonPrice | undefined;
  term: WebmasterComparisonTerm | undefined;
  offerTermLabel: string;
}) {
  const status = normalizeServiceAvailabilityStatus(price?.availabilityStatus);
  if (
    !price ||
    status === SERVICE_AVAILABILITY_STATUS.Unknown ||
    status === SERVICE_AVAILABILITY_STATUS.NotAvailable ||
    (status === SERVICE_AVAILABILITY_STATUS.Available && price.webmasterPriceUsd == null)
  ) {
    return (
      <TableCell align="right" sx={{ color: 'text.secondary' }}>
        {status === SERVICE_AVAILABILITY_STATUS.NotAvailable ? 'NO' : '—'}
      </TableCell>
    );
  }
  const value =
    status === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice
      ? 'YES'
      : price.webmasterPriceUsd != null
        ? currency.format(price.webmasterPriceUsd)
        : '—';
  const margin = price.margins.find((item) => item.termKey === term?.termKey);
  const hasNumericPrice =
    status === SERVICE_AVAILABILITY_STATUS.Available && price.webmasterPriceUsd != null;
  const reason = hasNumericPrice
    ? (margin?.unavailableReason ?? 'No catalog price')
    : 'Price not specified';
  const amount = margin?.amountUsd;
  const showPriceTerm = hasNumericPrice && price.termLabel !== offerTermLabel;

  return (
    <TableCell align="right">
      <Typography
        variant="body2"
        sx={{ fontWeight: 600, fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}
      >
        {value}
      </Typography>
      {amount != null ? (
        <Tooltip title="Catalog price for the same service and term − webmaster price, in USD">
          <Typography
            variant="caption"
            component="div"
            sx={{
              color: amount <= 0 ? 'error.main' : 'success.main',
              fontWeight: 500,
              fontVariantNumeric: 'tabular-nums',
              whiteSpace: 'nowrap',
              mt: 0.25,
            }}
          >
            Margin {amount > 0 ? '+' : amount < 0 ? '−' : ''}
            {amount === 0 ? '$0' : currency.format(Math.abs(amount))}
          </Typography>
        </Tooltip>
      ) : (
        <Typography
          variant="caption"
          color={reason === 'Terms differ' ? 'warning.dark' : 'text.secondary'}
          display="block"
          sx={{ mt: 0.25 }}
        >
          {reason}
        </Typography>
      )}
      {showPriceTerm && (
        <Typography variant="caption" color="text.secondary" display="block">
          Term: {price.termLabel}
        </Typography>
      )}
    </TableCell>
  );
}

function OfferRow({
  offer,
  index,
  term,
  onViewOffer,
  onCopySuccess,
  onCopyError,
}: {
  offer: WebmasterComparisonOffer;
  index: number;
  term: WebmasterComparisonTerm | undefined;
  onViewOffer: (id: string) => void;
  onCopySuccess: () => void;
  onCopyError: () => void;
}) {
  const email = offer.primaryEmail?.trim();
  return (
    <TableRow hover>
      <TableCell component="th" scope="row">
        <Tooltip title="View offer details">
          <Button
            size="small"
            onClick={() => onViewOffer(offer.id)}
            aria-label={`View offer ${index + 1}`}
            sx={{
              minWidth: 0,
              px: 0.5,
              py: 0.25,
              textDecoration: 'underline',
              textUnderlineOffset: '3px',
              '&.Mui-focusVisible': { outline: '2px solid', outlineColor: 'primary.main' },
            }}
          >
            #{index + 1}
          </Button>
        </Tooltip>
      </TableCell>
      <TableCell>
        <OfferTermStatus termLabel={offer.termLabel} status={offer.status} />
      </TableCell>
      <TableCell>
        <Stack direction="row" alignItems="center" gap={0.5}>
          <Typography
            variant="body2"
            color={email ? 'text.primary' : 'text.secondary'}
            sx={{ minWidth: 0, overflowWrap: 'anywhere', userSelect: 'text' }}
          >
            {email || '—'}
          </Typography>
          {email && (
            <CopyToClipboardIconButton
              key={email}
              value={email}
              copyLabel="Copy Primary Email"
              copiedLabel="Copied"
              onCopySuccess={onCopySuccess}
              onCopyError={onCopyError}
            />
          )}
        </Stack>
      </TableCell>
      {columns.map((column) => (
        <PriceCell
          key={column.value}
          price={offer.prices.find((price) => hasType(price, column))}
          term={term}
          offerTermLabel={offer.termLabel}
        />
      ))}
    </TableRow>
  );
}

export function WebmasterPriceComparisonPanel({
  domain,
  searchVersion,
  onViewOffer,
}: {
  domain: string;
  searchVersion: number;
  onViewOffer: (id: string) => void;
}) {
  const { canReadLinkDetectivePrices } = useUserRoles();
  const [comparison, setComparison] = useState<WebmasterPriceComparison | null>(null);
  const [selectedTermKey, setSelectedTermKey] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [retryVersion, setRetryVersion] = useState(0);
  const [copyError, setCopyError] = useState(false);

  useEffect(() => {
    let active = true;
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const result = await webmasterOffersService.getPriceComparison(domain);
        if (!active) return;
        setComparison(result);
        setSelectedTermKey((current) =>
          result.terms.some((term) => term.termKey === current) ? current : result.defaultTermKey
        );
      } catch (err) {
        if (active)
          setError(err instanceof Error ? err.message : 'Price comparison could not be loaded.');
      } finally {
        if (active) setLoading(false);
      }
    };
    void load();
    return () => {
      active = false;
    };
  }, [domain, searchVersion, retryVersion]);

  const term = comparison?.terms.find((item) => item.termKey === selectedTermKey);
  const termControl =
    comparison && comparison.terms.length > 1 ? (
      <TextField
        select
        size="small"
        label="Catalog price term"
        value={term?.termKey ?? ''}
        disabled={loading}
        onChange={(event) => setSelectedTermKey(event.target.value)}
        sx={{
          minWidth: 220,
          maxWidth: '100%',
          '& .MuiInputBase-input': { fontSize: 12 },
        }}
      >
        {comparison.terms.map((item) => (
          <MenuItem key={item.termKey} value={item.termKey}>
            {item.label} · {item.comparableOfferCount}{' '}
            {item.comparableOfferCount === 1 ? 'offer' : 'offers'}
          </MenuItem>
        ))}
      </TextField>
    ) : term ? (
      <OfferTermChip label={term.label} />
    ) : (
      <Typography variant="caption" color="text.secondary">
        —
      </Typography>
    );

  return (
    <Stack spacing={2.5}>
      {canReadLinkDetectivePrices && (
        <LinkDetectivePricesPanel domain={domain} searchVersion={searchVersion} />
      )}
      <Box
        component="section"
        aria-label="Catalog and webmaster price comparison"
        aria-busy={loading}
      >
        <Typography variant="subtitle1" component="h2" fontWeight={600}>
          Catalog vs webmaster offers
        </Typography>
        {loading && comparison && (
          <LinearProgress aria-label="Updating price comparison" sx={{ mt: 1 }} />
        )}
        {error && (
          <Alert
            severity="warning"
            sx={{ mt: 1 }}
            action={
              <BrandButton size="small" onClick={() => setRetryVersion((value) => value + 1)}>
                Retry
              </BrandButton>
            }
          >
            {error}
          </Alert>
        )}
        {loading && !comparison && (
          <Box sx={{ py: 3 }}>
            <CircularProgress size={24} aria-label="Loading price comparison" />
          </Box>
        )}
        {comparison?.siteFound === false && (
          <Alert severity="warning" sx={{ mt: 1 }}>
            Site not found: {domain}
          </Alert>
        )}
        {comparison?.siteFound && (
          <>
            <Typography variant="caption" color="text.secondary" component="p" sx={{ my: 1 }}>
              {comparison.offers.length} {comparison.offers.length === 1 ? 'offer' : 'offers'}
            </Typography>
            {comparison.terms.length === 0 && (
              <Alert severity="info" sx={{ mb: 1 }}>
                No catalog prices available for comparison.
              </Alert>
            )}
            <TableContainer
              component={Paper}
              role="region"
              aria-label="Scrollable price comparison"
              tabIndex={0}
              sx={(theme) => getTableSurfaceStyles(theme)}
            >
              <Table
                size="small"
                aria-label="Catalog and webmaster price comparison"
                sx={{
                  minWidth: 1276,
                  tableLayout: 'fixed',
                  '& .MuiTableCell-root': { verticalAlign: 'top', py: 1 },
                  '& .MuiTableCell-head': { verticalAlign: 'middle', whiteSpace: 'nowrap' },
                  '& .MuiTableBody-root > .MuiTableRow-root:last-child > .MuiTableCell-root': {
                    borderBottom: 0,
                  },
                }}
              >
                <colgroup>
                  <col style={{ width: 56 }} />
                  <col style={{ width: 140 }} />
                  <col style={{ width: 280 }} />
                  {columns.map((column) => (
                    <col key={column.value} style={{ width: column.value === 5 ? 160 : 128 }} />
                  ))}
                </colgroup>
                <TableHead>
                  <TableRow>
                    <TableCell scope="col">#</TableCell>
                    <TableCell scope="col">Status &amp; Term</TableCell>
                    <TableCell scope="col">Primary Email</TableCell>
                    {columns.map((column) => (
                      <TableCell key={column.value} scope="col" align="right">
                        {column.value === 5 ? (
                          <Stack
                            direction="row"
                            alignItems="center"
                            justifyContent="flex-end"
                            gap={0.25}
                          >
                            {column.label}
                            <Tooltip title="Catalog: Link Insert Casino">
                              <IconButton
                                size="small"
                                aria-label="Catalog equivalent: Link Insert Casino"
                                sx={{ p: 0.25 }}
                              >
                                <InfoOutlinedIcon sx={{ fontSize: 14 }} />
                              </IconButton>
                            </Tooltip>
                          </Stack>
                        ) : (
                          column.label
                        )}
                      </TableCell>
                    ))}
                  </TableRow>
                </TableHead>
                <TableBody>
                  <TableRow
                    sx={{ bgcolor: 'grey.50', '& .MuiTableCell-root': { borderBottomWidth: 2 } }}
                  >
                    <TableCell />
                    <TableCell component="th" scope="row" colSpan={2}>
                      <Stack direction="row" alignItems="center" gap={1.25} flexWrap="wrap">
                        <Typography variant="body2" fontWeight={600}>
                          Catalog
                        </Typography>
                        {termControl}
                      </Stack>
                    </TableCell>
                    {columns.map((column) => {
                      const amount = term?.catalogPrices.find((price) =>
                        hasType(price, column)
                      )?.amountUsd;
                      return (
                        <TableCell
                          key={column.value}
                          align="right"
                          sx={{
                            fontWeight: 600,
                            fontVariantNumeric: 'tabular-nums',
                            color: amount == null ? 'text.secondary' : 'text.primary',
                          }}
                        >
                          {amount == null ? '—' : currency.format(amount)}
                        </TableCell>
                      );
                    })}
                  </TableRow>
                  {comparison.offers.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={columns.length + 3} sx={{ color: 'text.secondary' }}>
                        No webmaster offers found to calculate margin.
                      </TableCell>
                    </TableRow>
                  )}
                  {comparison.offers.map((offer, index) => (
                    <OfferRow
                      key={offer.id}
                      offer={offer}
                      index={index}
                      term={term}
                      onViewOffer={onViewOffer}
                      onCopySuccess={() => setCopyError(false)}
                      onCopyError={() => setCopyError(true)}
                    />
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </>
        )}
      </Box>
      <Snackbar
        open={copyError}
        autoHideDuration={4000}
        onClose={() => setCopyError(false)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity="error" onClose={() => setCopyError(false)}>
          Could not copy Primary Email
        </Alert>
      </Snackbar>
    </Stack>
  );
}
