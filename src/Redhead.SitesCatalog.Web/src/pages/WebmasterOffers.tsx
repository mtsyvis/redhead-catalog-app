import { useState } from 'react';
import type { FormEvent } from 'react';
import { Navigate } from 'react-router-dom';
import {
  Alert,
  CircularProgress,
  IconButton,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import ClearIcon from '@mui/icons-material/Clear';
import SearchIcon from '@mui/icons-material/Search';
import { BrandButton } from '../components/common/BrandButton';
import { PageShell } from '../components/layout/PageShell';
import { WebmasterOffersComparison } from '../components/webmaster-offers/WebmasterOffersComparison';
import { EditWebmasterOfferDialog } from '../components/webmaster-offers/EditWebmasterOfferDialog';
import { useUserRoles } from '../hooks/useUserRoles';
import { webmasterOffersService } from '../services/webmasterOffers.service';
import type { WebmasterOffersSearchResult } from '../types/webmasterOffers.types';
import {
  cacheWebmasterOffersSearch,
  clearCachedWebmasterOffersSearch,
  readCachedWebmasterOffersSearch,
} from '../utils/webmasterOffersSearchCache';

function pluralizeOffers(count: number) {
  return count === 1 ? `${count} offer` : `${count} offers`;
}

export function WebmasterOffers() {
  const { canReadWebmasterOffers, canManageWebmasterOffers } = useUserRoles();
  const [cachedSearch] = useState(readCachedWebmasterOffersSearch);
  const [domain, setDomain] = useState(cachedSearch?.query ?? '');
  const [result, setResult] = useState<WebmasterOffersSearchResult | null>(
    cachedSearch?.result ?? null
  );
  const [expandedOfferIds, setExpandedOfferIds] = useState<Set<string>>(() => new Set());
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [editingOfferId, setEditingOfferId] = useState<string | null>(null);

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
      const searchResult = await webmasterOffersService.getByDomain(trimmed);
      setResult(searchResult);
      setExpandedOfferIds(new Set());
      cacheWebmasterOffersSearch(trimmed, searchResult);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load webmaster offers');
    } finally {
      setLoading(false);
    }
  };

  const handleClearSearch = () => {
    setDomain('');
    setResult(null);
    setExpandedOfferIds(new Set());
    setError(null);
    clearCachedWebmasterOffersSearch();
  };

  const handleExpandedOfferChange = (offerId: string, expanded: boolean) => {
    setExpandedOfferIds((current) => {
      const next = new Set(current);
      if (expanded) {
        next.add(offerId);
      } else {
        next.delete(offerId);
      }
      return next;
    });
  };

  const handleOfferSaved = async () => {
    setEditingOfferId(null);
    if (!result?.siteFound) return;

    setLoading(true);
    setError(null);
    try {
      const refreshed = await webmasterOffersService.getByDomain(result.domain);
      setResult(refreshed);
      cacheWebmasterOffersSearch(result.domain, refreshed);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Offer was saved, but the results could not be refreshed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <PageShell title="Webmaster Offers" maxWidth="xl">
      <Paper component="form" onSubmit={handleSubmit} sx={{ p: 2, mb: 3 }}>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
          <TextField
            label="Domain"
            value={domain}
            onChange={(event) => setDomain(event.target.value)}
            placeholder="example.com"
            size="small"
            fullWidth
            InputProps={{
              endAdornment: domain ? (
                <IconButton
                  aria-label="Clear search"
                  edge="end"
                  size="small"
                  disabled={loading}
                  onMouseDown={(event) => event.preventDefault()}
                  onClick={handleClearSearch}
                  sx={{ color: 'text.secondary' }}
                >
                  <ClearIcon fontSize="small" />
                </IconButton>
              ) : undefined,
            }}
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
          <Typography variant="body2" color="text.secondary">
            {pluralizeOffers(result.offers.length)}
          </Typography>

          {result.offers.length === 0 ? (
            <Alert severity="info">No webmaster offers found.</Alert>
          ) : (
            <WebmasterOffersComparison
              offers={result.offers}
              expandedOfferIds={expandedOfferIds}
              onExpandedOfferChange={handleExpandedOfferChange}
              canEdit={canManageWebmasterOffers}
              onEditOffer={setEditingOfferId}
            />
          )}
        </Stack>
      )}

      <EditWebmasterOfferDialog
        open={Boolean(editingOfferId)}
        offerId={editingOfferId}
        domain={result?.domain ?? domain.trim()}
        onClose={() => setEditingOfferId(null)}
        onSaved={handleOfferSaved}
      />
    </PageShell>
  );
}
