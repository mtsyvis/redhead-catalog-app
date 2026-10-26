import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, Box, CircularProgress, Stack, Tooltip, Typography } from '@mui/material';
import RefreshIcon from '@mui/icons-material/Refresh';
import { BrandButton } from '../common/BrandButton';
import { webmasterOffersService } from '../../services/webmasterOffers.service';
import type { LinkDetectivePrices } from '../../types/webmasterOffers.types';

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  maximumFractionDigits: 2,
});

export function LinkDetectivePricesPanel({
  domain,
  searchVersion,
}: {
  domain: string;
  searchVersion: number;
}) {
  const [prices, setPrices] = useState<LinkDetectivePrices | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [now, setNow] = useState(() => Date.now());
  const requestSequence = useRef(0);
  const mounted = useRef(false);
  const busy = useRef(true);

  const loadPrices = useCallback(
    async (forceRefresh = false) => {
      const sequence = ++requestSequence.current;
      busy.current = true;
      setLoading(true);
      setError(null);
      try {
        const result = await webmasterOffersService.getLinkDetectivePrices(domain, forceRefresh);
        if (mounted.current && sequence === requestSequence.current) {
          setPrices(result);
          setNow(Date.now());
        }
      } catch {
        if (mounted.current && sequence === requestSequence.current) {
          setError(
            forceRefresh
              ? 'Market prices could not be updated. Try again.'
              : 'Market prices could not be loaded. Try again.'
          );
        }
      } finally {
        if (mounted.current && sequence === requestSequence.current) {
          busy.current = false;
          setLoading(false);
        }
      }
    },
    [domain]
  );

  useEffect(() => {
    mounted.current = true;
    void loadPrices();
    return () => {
      mounted.current = false;
      requestSequence.current += 1;
    };
  }, [loadPrices, searchVersion]);

  useEffect(() => {
    if (!prices?.retryAfterUtc) return;
    const timer = window.setTimeout(
      () => setNow(Date.now()),
      Math.min(2_147_483_647, Math.max(0, Date.parse(prices.retryAfterUtc) - Date.now()))
    );
    return () => window.clearTimeout(timer);
  }, [prices?.retryAfterUtc]);

  const coolingDown = Boolean(prices?.retryAfterUtc && Date.parse(prices.retryAfterUtc) > now);

  const refresh = () => {
    if (!busy.current) void loadPrices(true);
  };

  const hasPrices =
    prices != null &&
    [prices.minimumUsd, prices.averageUsd, prices.medianUsd].some((amount) => amount != null);

  const refreshTooltip = [
    'Service type and placement term are not specified by the source.',
    coolingDown && prices?.retryAfterUtc
      ? `Refresh available after ${new Date(prices.retryAfterUtc).toLocaleString()}.`
      : null,
  ]
    .filter(Boolean)
    .join(' ');

  const refreshButton = (
    <Tooltip title={refreshTooltip} describeChild>
      <Box
        component="span"
        tabIndex={loading || coolingDown ? 0 : undefined}
        sx={{ display: 'inline-flex' }}
      >
        <BrandButton
          size="small"
          disabled={loading || coolingDown}
          startIcon={loading ? <CircularProgress size={16} /> : <RefreshIcon />}
          onClick={refresh}
        >
          {loading ? 'Loading…' : error && !hasPrices ? 'Retry' : 'Refresh prices'}
        </BrandButton>
      </Box>
    </Tooltip>
  );

  if (!hasPrices) {
    return (
      <Box aria-label="LinkDetective market prices" sx={{ px: 0.25, py: 0.5 }}>
        <Stack direction="row" alignItems="center" flexWrap="wrap" gap={1}>
          <Typography variant="body2" fontWeight={600}>
            Market prices · LinkDetective
          </Typography>
          <Typography
            variant="body2"
            color="text.secondary"
            sx={{ flex: '1 1 240px' }}
            role="status"
          >
            {loading
              ? 'Loading market prices…'
              : error || prices?.message || 'No market prices found for this domain.'}
          </Typography>
          {loading ? (
            <CircularProgress size={16} aria-label="Loading market prices" />
          ) : (
            (error || prices?.checkedAtUtc || prices?.retryAfterUtc) && refreshButton
          )}
        </Stack>
        {prices?.checkedAtUtc && (
          <Typography variant="caption" color="text.secondary" display="block" sx={{ mt: 0.25 }}>
            Retrieved: {new Date(prices.checkedAtUtc).toLocaleString()}
          </Typography>
        )}
      </Box>
    );
  }

  return (
    <Box
      sx={{
        p: 1.5,
        border: 1,
        borderColor: 'divider',
        borderRadius: 1,
        bgcolor: 'background.paper',
      }}
      aria-label="LinkDetective market prices"
    >
      <Stack direction="row" alignItems="center" flexWrap="wrap" gap={2}>
        <Typography variant="body2" fontWeight={600}>
          LinkDetective
        </Typography>
        {(
          [
            ['Minimum', prices?.minimumUsd],
            ['Median', prices?.medianUsd],
            ['Average', prices?.averageUsd],
          ] as const
        ).map(([label, amount]) => (
          <Stack key={label} direction="row" alignItems="baseline" gap={0.5}>
            <Typography variant="caption" color="text.secondary">
              {label}
            </Typography>
            <Typography variant="body2">
              {amount == null ? '—' : currency.format(amount)}
            </Typography>
          </Stack>
        ))}
        {prices?.checkedAtUtc && (
          <Typography variant="caption" color="text.secondary" sx={{ ml: 'auto' }}>
            Retrieved: {new Date(prices.checkedAtUtc).toLocaleString()}
            {prices.isStale ? ' · Outdated' : ''}
          </Typography>
        )}
        {refreshButton}
      </Stack>
      {(error || prices?.message) && (
        <Alert
          severity={error || prices?.isStale || !prices?.checkedAtUtc ? 'warning' : 'info'}
          sx={{ mt: 1 }}
        >
          {error || prices?.message}
        </Alert>
      )}
    </Box>
  );
}
