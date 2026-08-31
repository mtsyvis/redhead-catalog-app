import { useCallback, useEffect, useRef, useState } from 'react';
import { Navigate } from 'react-router-dom';
import {
  Alert,
  Box,
  CircularProgress,
  IconButton,
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

const SEARCH_DEBOUNCE_MS = 400;

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
  const searchRequestSequence = useRef(0);
  const lastSearchKey = useRef(cachedSearch?.query.trim().toLowerCase() ?? '');

  const runSearch = useCallback(async (query: string, force = false) => {
    const trimmed = query.trim();
    if (!trimmed) return;

    const searchKey = trimmed.toLowerCase();
    if (!force && lastSearchKey.current === searchKey) return;
    lastSearchKey.current = searchKey;
    const requestSequence = ++searchRequestSequence.current;

    setLoading(true);
    setError(null);
    try {
      const searchResult = await webmasterOffersService.getByDomain(trimmed);
      if (searchRequestSequence.current !== requestSequence) return;
      setResult(searchResult);
      setExpandedOfferIds(new Set());
      cacheWebmasterOffersSearch(trimmed, searchResult);
    } catch (err) {
      if (searchRequestSequence.current !== requestSequence) return;
      lastSearchKey.current = '';
      setError(err instanceof Error ? err.message : 'Failed to load webmaster offers');
    } finally {
      if (searchRequestSequence.current === requestSequence) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    if (!canReadWebmasterOffers) return;

    const timer = window.setTimeout(() => {
      void runSearch(domain);
    }, SEARCH_DEBOUNCE_MS);

    return () => window.clearTimeout(timer);
  }, [canReadWebmasterOffers, domain, runSearch]);

  if (!canReadWebmasterOffers) {
    return <Navigate to="/sites" replace />;
  }

  const handleDomainChange = (value: string) => {
    searchRequestSequence.current += 1;
    lastSearchKey.current = '';
    setDomain(value);
    setResult(null);
    setExpandedOfferIds(new Set());
    setLoading(false);
    setError(null);
    clearCachedWebmasterOffersSearch();
  };

  const handleClearSearch = () => {
    searchRequestSequence.current += 1;
    lastSearchKey.current = '';
    setDomain('');
    setResult(null);
    setExpandedOfferIds(new Set());
    setLoading(false);
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
      <Box sx={{ mb: 1.5 }}>
        <TextField
          fullWidth
          placeholder="Search by domain (example.com or https://www.example.com/path)"
          value={domain}
          onChange={(event) => handleDomainChange(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              void runSearch(domain, true);
            }
          }}
          inputProps={{
            'aria-label': 'Search webmaster offers by domain',
          }}
          InputProps={{
            startAdornment: <SearchIcon sx={{ mr: 1, color: 'text.secondary' }} />,
            endAdornment: loading || domain ? (
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                {loading && <CircularProgress size={18} color="inherit" />}
                {domain && (
                  <IconButton
                    aria-label="Clear search"
                    edge="end"
                    size="small"
                    onMouseDown={(event) => event.preventDefault()}
                    onClick={handleClearSearch}
                    sx={{ color: 'text.secondary' }}
                  >
                    <ClearIcon fontSize="small" />
                  </IconButton>
                )}
              </Box>
            ) : undefined,
          }}
        />
      </Box>

      {error && (
        <Alert
          severity="error"
          sx={{ mb: 3 }}
          action={
            <BrandButton size="small" onClick={() => void runSearch(domain, true)}>
              Retry
            </BrandButton>
          }
        >
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
