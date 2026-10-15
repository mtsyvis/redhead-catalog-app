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
import { WebmasterOffersSummaryTable } from '../components/webmaster-offers/WebmasterOffersSummaryTable';
import { useAuth } from '../contexts/AuthContext';
import { useUserRoles } from '../hooks/useUserRoles';
import { webmasterOffersService } from '../services/webmasterOffers.service';
import type {
  WebmasterOffersSearchResult,
  WebmasterOffersSummarySearchResult,
} from '../types/webmasterOffers.types';
import {
  cacheWebmasterOffersSearch,
  clearCachedWebmasterOffersSearch,
  readCachedWebmasterOffersSearch,
  type WebmasterOffersCacheScope,
} from '../utils/webmasterOffersSearchCache';

const SEARCH_DEBOUNCE_MS = 400;

function pluralizeOffers(count: number) {
  return count === 1 ? `${count} offer` : `${count} offers`;
}

export function WebmasterOffers() {
  const { user } = useAuth();
  const { canReadWebmasterOffers, canReadWebmasterOffersSummary } = useUserRoles();
  if (!canReadWebmasterOffersSummary || !user) return <Navigate to="/sites" replace />;

  const access = canReadWebmasterOffers ? 'full' : 'summary';
  return <WebmasterOffersPage key={`${user.id}:${access}`} userId={user.id} access={access} />;
}

function isFullSearchResult(
  result: WebmasterOffersSummarySearchResult
): result is WebmasterOffersSearchResult {
  return result.offers.every((offer) => 'prices' in offer && 'contactRawText' in offer);
}

function WebmasterOffersPage({ userId, access }: WebmasterOffersCacheScope) {
  const { canReadWebmasterOffers, canManageWebmasterOffers } = useUserRoles();
  const [cachedSearch] = useState(() => readCachedWebmasterOffersSearch({ userId, access }));
  const [domain, setDomain] = useState(cachedSearch?.query ?? '');
  const [result, setResult] = useState<WebmasterOffersSummarySearchResult | null>(
    cachedSearch?.result ?? null
  );
  const [expandedOfferIds, setExpandedOfferIds] = useState<Set<string>>(() => new Set());
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [editingOfferId, setEditingOfferId] = useState<string | null>(null);
  const searchRequestSequence = useRef(0);
  const lastSearchKey = useRef(cachedSearch?.query.trim().toLowerCase() ?? '');
  const mounted = useRef(false);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      searchRequestSequence.current += 1;
    };
  }, []);

  const runSearch = useCallback(async (
    query: string,
    mode: 'automatic' | 'manual' | 'after-save' = 'automatic'
  ) => {
    if (!mounted.current) return;
    const trimmed = query.trim();
    if (!trimmed) return;

    const searchKey = trimmed.toLowerCase();
    if (mode === 'automatic' && lastSearchKey.current === searchKey) return;
    lastSearchKey.current = searchKey;
    const requestSequence = ++searchRequestSequence.current;

    setLoading(true);
    setError(null);
    try {
      const searchResult = canReadWebmasterOffers
        ? await webmasterOffersService.getByDomain(trimmed)
        : await webmasterOffersService.getSummaryByDomain(trimmed);
      if (searchRequestSequence.current !== requestSequence) return;
      setResult(searchResult);
      if (mode !== 'after-save') setExpandedOfferIds(new Set());
      cacheWebmasterOffersSearch(trimmed, searchResult, { userId, access });
    } catch (err) {
      if (searchRequestSequence.current !== requestSequence) return;
      lastSearchKey.current = '';
      setError(err instanceof Error ? err.message : mode === 'after-save'
        ? 'Offer was saved, but the results could not be refreshed'
        : 'Failed to load webmaster offers');
    } finally {
      if (searchRequestSequence.current === requestSequence) {
        setLoading(false);
      }
    }
  }, [canReadWebmasterOffers, userId, access]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void runSearch(domain);
    }, SEARCH_DEBOUNCE_MS);

    return () => window.clearTimeout(timer);
  }, [domain, runSearch]);

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
    if (!mounted.current) return;
    setEditingOfferId(null);
    if (!result?.siteFound) return;
    await runSearch(result.domain, 'after-save');
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
              void runSearch(domain, 'manual');
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
                    onClick={() => handleDomainChange('')}
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
            <BrandButton size="small" onClick={() => void runSearch(domain, 'manual')}>
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
            {!canReadWebmasterOffers && ' · Newest first'}
          </Typography>

          {result.offers.length === 0 ? (
            <Alert severity="info">No webmaster offers found.</Alert>
          ) : canReadWebmasterOffers && isFullSearchResult(result) ? (
            <WebmasterOffersComparison
              offers={result.offers}
              expandedOfferIds={expandedOfferIds}
              onExpandedOfferChange={handleExpandedOfferChange}
              canEdit={canManageWebmasterOffers}
              onEditOffer={setEditingOfferId}
            />
          ) : (
            <WebmasterOffersSummaryTable offers={result.offers} />
          )}
        </Stack>
      )}

      {canManageWebmasterOffers && (
        <EditWebmasterOfferDialog
          open={Boolean(editingOfferId)}
          offerId={editingOfferId}
          domain={result?.domain ?? domain.trim()}
          onClose={() => setEditingOfferId(null)}
          onSaved={handleOfferSaved}
        />
      )}
    </PageShell>
  );
}
