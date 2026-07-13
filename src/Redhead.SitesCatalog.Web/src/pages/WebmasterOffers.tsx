import { useState } from 'react';
import type { FormEvent } from 'react';
import { Navigate } from 'react-router-dom';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Divider,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { PageShell } from '../components/layout/PageShell';
import { BrandButton } from '../components/common/BrandButton';
import { useUserRoles } from '../hooks/useUserRoles';
import { webmasterOffersService } from '../services/webmasterOffers.service';
import type {
  WebmasterOffer,
  WebmasterOfferPrice,
  WebmasterOfferPriceType,
  WebmasterOfferStatus,
  WebmasterOffersSearchResult,
} from '../types/webmasterOffers.types';

const PRICE_TYPE_LABELS: Record<number | string, string> = {
  0: 'Main',
  1: 'Casino',
  2: 'Crypto',
  3: 'Dating',
  4: 'Link insertion',
  5: 'Link insertion 18+',
  6: 'Banner',
  7: 'Banner 18+',
  8: 'Homepage text link',
  9: 'Homepage text link 18+',
  Main: 'Main',
  Casino: 'Casino',
  Crypto: 'Crypto',
  Dating: 'Dating',
  LinkInsertion: 'Link insertion',
  LinkInsertion18Plus: 'Link insertion 18+',
  Banner: 'Banner',
  Banner18Plus: 'Banner 18+',
  HomepageTextLink: 'Homepage text link',
  HomepageTextLink18Plus: 'Homepage text link 18+',
};

const STATUS_LABELS: Record<number | string, string> = {
  1: 'Active',
  2: 'Inactive',
  Active: 'Active',
  Inactive: 'Inactive',
};

function priceTypeLabel(priceType: WebmasterOfferPriceType) {
  return PRICE_TYPE_LABELS[priceType] ?? String(priceType);
}

function statusLabel(status: WebmasterOfferStatus) {
  return STATUS_LABELS[status] ?? String(status);
}

function statusColor(status: WebmasterOfferStatus) {
  return status === 2 || status === 'Inactive' ? 'default' : 'success';
}

function formatCurrency(value: number | null) {
  if (value == null) return null;

  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 2,
  }).format(value);
}

function formatDate(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';

  const day = String(date.getUTCDate()).padStart(2, '0');
  const month = String(date.getUTCMonth() + 1).padStart(2, '0');
  const year = date.getUTCFullYear();
  return `${day}.${month}.${year}`;
}

function TextBlock({
  label,
  value,
}: {
  readonly label: string;
  readonly value: string | null | undefined;
}) {
  if (!value?.trim()) {
    return null;
  }

  return (
    <Box>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
        {label}
      </Typography>
      <Typography
        variant="body2"
        sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', lineHeight: 1.55 }}
      >
        {value}
      </Typography>
    </Box>
  );
}

function PriceList({ prices }: { readonly prices: readonly WebmasterOfferPrice[] }) {
  if (prices.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary">
        No raw prices
      </Typography>
    );
  }

  return (
    <Box
      sx={{
        display: 'grid',
        gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, minmax(0, 1fr))' },
        gap: 1,
      }}
    >
      {prices.map((price) => {
        const amount = formatCurrency(price.webmasterPriceUsd);
        return (
          <Box
            key={price.id}
            sx={{
              border: 1,
              borderColor: 'divider',
              borderRadius: 1,
              p: 1.25,
              minWidth: 0,
            }}
          >
            <Typography variant="subtitle2" sx={{ mb: 0.25 }}>
              {priceTypeLabel(price.priceType)}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 700 }}>
              {amount ?? 'No amount'}
            </Typography>
            {price.webmasterPriceDetails?.trim() && (
              <Typography
                variant="body2"
                color="text.secondary"
                sx={{ mt: 0.5, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}
              >
                {price.webmasterPriceDetails}
              </Typography>
            )}
          </Box>
        );
      })}
    </Box>
  );
}

function OfferCard({ offer, index }: { readonly offer: WebmasterOffer; readonly index: number }) {
  const mailboxLabels = offer.linkbuilderMailboxes.map((mailbox) =>
    mailbox.displayName && mailbox.displayName !== mailbox.email
      ? `${mailbox.displayName} <${mailbox.email}>`
      : mailbox.email
  );

  return (
    <Paper variant="outlined" sx={{ p: { xs: 2, sm: 2.5 } }}>
      <Stack spacing={2}>
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 1.5,
            flexWrap: 'wrap',
          }}
        >
          <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
            <Typography variant="h6">Offer {index + 1}</Typography>
            <Chip
              size="small"
              label={statusLabel(offer.status)}
              color={statusColor(offer.status)}
              variant="outlined"
            />
            <Chip size="small" label={offer.termLabel || 'No term'} variant="outlined" />
          </Stack>
          <Typography variant="caption" color="text.secondary">
            {formatDate(offer.createdAtUtc)}
          </Typography>
        </Box>

        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', md: 'minmax(0, 1fr) minmax(0, 1fr)' },
            gap: 2,
          }}
        >
          <Stack spacing={1.5}>
            <TextBlock label="Contact" value={offer.contactRawText} />
            <TextBlock label="Outreach sender" value={offer.outreachSenderRawText} />
            <TextBlock label="Linkbuilder mailbox raw" value={offer.linkbuilderMailboxRawText} />
            {mailboxLabels.length > 0 && (
              <Box>
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ display: 'block', mb: 0.75 }}
                >
                  Parsed mailboxes
                </Typography>
                <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                  {mailboxLabels.map((label) => (
                    <Chip key={label} size="small" label={label} variant="outlined" />
                  ))}
                </Stack>
              </Box>
            )}
          </Stack>

          <Stack spacing={1.5}>
            <TextBlock label="Link policy" value={offer.linkPolicyText} />
            <TextBlock label="Comments" value={offer.commentText} />
            <TextBlock label="Client raw" value={offer.clientRawText} />
            <TextBlock label="Raw term" value={offer.termRawText} />
          </Stack>
        </Box>

        <Divider />
        <PriceList prices={offer.prices} />
      </Stack>
    </Paper>
  );
}

export function WebmasterOffers() {
  const { canReadWebmasterOffers } = useUserRoles();
  const [domain, setDomain] = useState('');
  const [result, setResult] = useState<WebmasterOffersSearchResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!canReadWebmasterOffers) {
    return <Navigate to="/sites" replace />;
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const trimmed = domain.trim();
    if (!trimmed) return;

    setLoading(true);
    setError(null);
    try {
      setResult(await webmasterOffersService.getByDomain(trimmed));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load webmaster offers');
    } finally {
      setLoading(false);
    }
  };

  return (
    <PageShell title="Webmaster Offers" maxWidth="lg">
      <Paper component="form" onSubmit={handleSubmit} sx={{ p: 2, mb: 3 }}>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
          <TextField
            label="Domain"
            value={domain}
            onChange={(event) => setDomain(event.target.value)}
            placeholder="example.com"
            size="small"
            fullWidth
          />
          <BrandButton
            type="submit"
            disabled={loading || !domain.trim()}
            startIcon={loading ? <CircularProgress size={18} color="inherit" /> : <SearchIcon />}
            sx={{ minWidth: 120 }}
          >
            Search
          </BrandButton>
        </Stack>
      </Paper>

      {error && (
        <Alert severity="error" sx={{ mb: 3 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {result && !result.siteFound && (
        <Alert severity="warning">Site not found: {result.domain || domain.trim()}</Alert>
      )}

      {result?.siteFound && (
        <Stack spacing={2}>
          <Box>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              {result.domain}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {result.offers.length} offers
            </Typography>
          </Box>

          {result.offers.length === 0 ? (
            <Alert severity="info">No webmaster offers found.</Alert>
          ) : (
            result.offers.map((offer, index) => (
              <OfferCard key={offer.id} offer={offer} index={index} />
            ))
          )}
        </Stack>
      )}
    </PageShell>
  );
}
