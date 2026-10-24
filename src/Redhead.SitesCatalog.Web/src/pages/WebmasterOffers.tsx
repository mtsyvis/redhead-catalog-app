import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Navigate } from 'react-router-dom';
import {
  Alert,
  Box,
  CircularProgress,
  IconButton,
  Stack,
  TextField,
  Typography,
  TablePagination,
} from '@mui/material';
import ClearIcon from '@mui/icons-material/Clear';
import SearchIcon from '@mui/icons-material/Search';
import DownloadIcon from '@mui/icons-material/Download';
import { BrandButton } from '../components/common/BrandButton';
import { SearchModeToggle } from '../components/common/SearchModeToggle';
import { SearchBarLayout } from '../components/common/SearchBarLayout';
import { WebmasterOffersDomainList } from '../components/webmaster-offers/WebmasterOffersDomainList';
import { DEFAULT_MULTI_SEARCH_LIMIT } from '../constants/search.constants';
import { PageShell } from '../components/layout/PageShell';
import { WebmasterOffersComparison } from '../components/webmaster-offers/WebmasterOffersComparison';
import { LinkDetectivePricesPanel } from '../components/webmaster-offers/LinkDetectivePricesPanel';
import { EditWebmasterOfferDialog } from '../components/webmaster-offers/EditWebmasterOfferDialog';
import { WebmasterOffersSummaryTable } from '../components/webmaster-offers/WebmasterOffersSummaryTable';
import { useAuth } from '../contexts/AuthContext';
import { useUserRoles } from '../hooks/useUserRoles';
import { webmasterOffersService } from '../services/webmasterOffers.service';
import type {
  WebmasterOffersResult,
  WebmasterOfferRow,
  WebmasterOfferSummaryRow,
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

function isFullOffers(offers: WebmasterOfferSummaryRow[]): offers is WebmasterOfferRow[] {
  return offers.every((offer) => 'prices' in offer && 'contactRawText' in offer);
}

function WebmasterOffersPage({ userId, access }: WebmasterOffersCacheScope) {
  const { canReadWebmasterOffers, canManageWebmasterOffers, canExportWebmasterOffers } =
    useUserRoles();
  const [cachedSearch] = useState(() => readCachedWebmasterOffersSearch({ userId, access }));
  const [multiSearchMode, setMultiSearchMode] = useState(cachedSearch?.mode === 'multi');
  const [domain, setDomain] = useState(cachedSearch?.query ?? '');
  const [result, setResult] = useState<WebmasterOffersResult | null>(cachedSearch?.result ?? null);
  const [expandedOfferIds, setExpandedOfferIds] = useState<Set<string>>(() => new Set());
  const [loading, setLoading] = useState(false);
  const [searchVersion, setSearchVersion] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [editingOfferId, setEditingOfferId] = useState<string | null>(null);
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(50);
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [appliedQuery, setAppliedQuery] = useState(cachedSearch?.query ?? '');
  const searchRequestSequence = useRef(0);
  const exportController = useRef<AbortController | null>(null);
  const lastSearchKey = useRef(cachedSearch?.query.trim().toLowerCase() ?? '');
  const mounted = useRef(false);
  const multiResult = result && 'results' in result ? result : null;
  const singleResult = result && 'offers' in result ? result : null;
  const inputCount = domain.trim() ? domain.trim().split(/\s+/).length : 0;
  const limitExceeded = multiSearchMode && inputCount > DEFAULT_MULTI_SEARCH_LIMIT;
  const duplicateInputCount = multiResult
    ? multiResult.inputCount - multiResult.uniqueDomainCount
    : 0;
  const offers = useMemo<WebmasterOfferSummaryRow[]>(
    () =>
      multiResult
        ? multiResult.results.flatMap((group) =>
            group.offers.map((offer) => ({ ...offer, domain: group.domain }))
          )
        : (singleResult?.offers ?? []),
    [multiResult, singleResult]
  );
  const visibleOffers = multiResult
    ? offers.slice(page * rowsPerPage, (page + 1) * rowsPerPage)
    : offers;
  const editingDomain = multiResult
    ? (offers.find((offer) => offer.id === editingOfferId)?.domain ?? '')
    : (singleResult?.domain ?? domain.trim());

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      searchRequestSequence.current += 1;
      exportController.current?.abort();
    };
  }, []);

  const runSearch = useCallback(
    async (query: string, mode: 'automatic' | 'manual' | 'after-save' = 'automatic') => {
      if (!mounted.current) return;
      const trimmed = query.trim();
      if (!trimmed) return;
      if (multiSearchMode && trimmed.split(/\s+/).length > DEFAULT_MULTI_SEARCH_LIMIT) return;

      const searchKey = trimmed.toLowerCase();
      if (mode === 'automatic' && lastSearchKey.current === searchKey) return;
      lastSearchKey.current = searchKey;
      const requestSequence = ++searchRequestSequence.current;

      exportController.current?.abort();
      exportController.current = null;
      setExporting(false);
      setExportError(null);
      setLoading(true);
      setError(null);
      try {
        const searchResult = multiSearchMode
          ? await webmasterOffersService.multiSearch(trimmed)
          : canReadWebmasterOffers
            ? await webmasterOffersService.getByDomain(trimmed)
            : await webmasterOffersService.getSummaryByDomain(trimmed);
        if (searchRequestSequence.current !== requestSequence) return;
        setResult(searchResult);
        setSearchVersion((current) => current + 1);
        setAppliedQuery(trimmed);
        if (mode !== 'after-save') {
          setExpandedOfferIds(new Set());
          setPage(0);
        } else if ('results' in searchResult) {
          const count = searchResult.results.reduce(
            (total, group) => total + group.offers.length,
            0
          );
          setPage((current) => Math.min(current, Math.max(0, Math.ceil(count / rowsPerPage) - 1)));
        }
        cacheWebmasterOffersSearch(
          trimmed,
          searchResult,
          { userId, access },
          multiSearchMode ? 'multi' : 'single'
        );
      } catch (err) {
        if (searchRequestSequence.current !== requestSequence) return;
        lastSearchKey.current = '';
        setError(
          err instanceof Error
            ? err.message
            : mode === 'after-save'
              ? 'Offer was saved, but the results could not be refreshed'
              : 'Failed to load webmaster offers'
        );
      } finally {
        if (searchRequestSequence.current === requestSequence) {
          setLoading(false);
        }
      }
    },
    [canReadWebmasterOffers, userId, access, multiSearchMode, rowsPerPage]
  );

  useEffect(() => {
    if (multiSearchMode) return;
    const timer = window.setTimeout(() => {
      void runSearch(domain);
    }, SEARCH_DEBOUNCE_MS);

    return () => window.clearTimeout(timer);
  }, [domain, runSearch, multiSearchMode]);

  const handleDomainChange = (value: string) => {
    searchRequestSequence.current += 1;
    exportController.current?.abort();
    exportController.current = null;
    setExporting(false);
    lastSearchKey.current = '';
    setDomain(value);
    setResult(null);
    setExpandedOfferIds(new Set());
    setLoading(false);
    setError(null);
    setExportError(null);
    setEditingOfferId(null);
    setPage(0);
    setAppliedQuery('');
    clearCachedWebmasterOffersSearch();
  };

  const handleModeChange = (enabled: boolean) => {
    if (enabled === multiSearchMode) return;
    handleDomainChange('');
    setMultiSearchMode(enabled);
  };

  const handleExport = async () => {
    if (exportController.current) return;
    const controller = new AbortController();
    exportController.current = controller;
    const requestSequence = searchRequestSequence.current;
    setExporting(true);
    setExportError(null);
    try {
      await webmasterOffersService.exportMultiSearch(appliedQuery, controller.signal);
    } catch (err) {
      if (!controller.signal.aborted && mounted.current && requestSequence === searchRequestSequence.current) {
        setExportError(err instanceof Error ? err.message : 'Failed to export webmaster offers');
      }
    } finally {
      if (exportController.current === controller) {
        exportController.current = null;
        if (mounted.current) setExporting(false);
      }
    }
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
    if (!result) return;
    await runSearch(appliedQuery, 'after-save');
  };

  return (
    <PageShell maxWidth="xl">
      <Box sx={{ mb: 1 }}>
        <Typography variant="h4" component="h1" sx={{ fontWeight: 600 }}>
          Webmaster Offers
        </Typography>
      </Box>
      <Box sx={{ mb: 1.5 }}>
        <Box sx={{ mb: 1 }}>
          <SearchBarLayout
            multiSearchMode={multiSearchMode}
            modeControl={
              <SearchModeToggle multiSearchMode={multiSearchMode} onChange={handleModeChange} />
            }
            input={
              <TextField
                fullWidth
                multiline={multiSearchMode}
                minRows={multiSearchMode ? 3 : 1}
                maxRows={multiSearchMode ? 8 : 1}
                placeholder={
                  multiSearchMode
                    ? 'Enter domains or URLs'
                    : 'Search by domain (example.com or https://www.example.com/path)'
                }
                error={limitExceeded}
                helperText={
                  multiSearchMode
                    ? limitExceeded
                      ? `Too many inputs. Limit is ${DEFAULT_MULTI_SEARCH_LIMIT.toLocaleString('en-US')}.`
                      : `${inputCount.toLocaleString('en-US')} / ${DEFAULT_MULTI_SEARCH_LIMIT.toLocaleString('en-US')} inputs`
                    : undefined
                }
                value={domain}
                onChange={(event) => handleDomainChange(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter' && (!multiSearchMode || event.ctrlKey)) {
                    event.preventDefault();
                    void runSearch(domain, 'manual');
                  }
                }}
                inputProps={{
                  'aria-label': multiSearchMode
                    ? 'Search webmaster offers by domains'
                    : 'Search webmaster offers by domain',
                }}
                InputProps={{
                  startAdornment: <SearchIcon sx={{ mr: 1, color: 'text.secondary' }} />,
                  endAdornment:
                    loading || domain ? (
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
                FormHelperTextProps={{
                  sx: {
                    mx: 1.5,
                    textAlign: 'right',
                    color: limitExceeded ? 'error.main' : 'text.secondary',
                  },
                }}
                sx={
                  multiSearchMode
                    ? undefined
                    : { flex: '1 1 360px', minWidth: { xs: '100%', md: 320 } }
                }
              />
            }
            searchAction={
              <BrandButton
                kind="primary"
                sx={{ minWidth: 120 }}
                disabled={loading || !domain.trim() || limitExceeded}
                onClick={() => void runSearch(domain, 'manual')}
              >
                {loading ? 'Searching…' : 'Search'}
              </BrandButton>
            }
          />
        </Box>
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

      {singleResult && !singleResult.siteFound && (
        <Alert severity="warning">Site not found: {singleResult.domain || domain.trim()}</Alert>
      )}

      {(multiResult || singleResult?.siteFound) && (
        <Stack spacing={2}>
          {!multiSearchMode && singleResult?.siteFound && canReadWebmasterOffers && (
            <LinkDetectivePricesPanel
              key={singleResult.domain}
              domain={singleResult.domain}
              searchVersion={searchVersion}
            />
          )}
          {multiResult && (
            <Typography variant="body2" color="text.secondary">
              {multiResult.uniqueDomainCount} domains · {multiResult.results.length} with offers
              {' · '}
              {multiResult.domainsWithoutOffers.length} without offers
              {' · '}
              {multiResult.notFoundDomains.length} not in catalog
              {duplicateInputCount > 0 &&
                ` · ${duplicateInputCount} duplicate ${duplicateInputCount === 1 ? 'input' : 'inputs'} removed`}
            </Typography>
          )}
          <Stack direction="row" spacing={2} alignItems="center" justifyContent="space-between">
            <Typography variant="body2" color="text.secondary">
              {pluralizeOffers(offers.length)}
              {!canReadWebmasterOffers &&
                (multiResult ? ' · Newest first within each domain' : ' · Newest first')}
            </Typography>
            {multiResult && canExportWebmasterOffers && (
              <BrandButton
                size="small"
                startIcon={exporting ? <CircularProgress size={16} /> : <DownloadIcon />}
                disabled={loading || exporting || offers.length === 0}
                onClick={() => void handleExport()}
              >
                {exporting ? 'Exporting…' : 'Export Excel'}
              </BrandButton>
            )}
          </Stack>
          {exportError && <Alert severity="error">{exportError}</Alert>}

          {offers.length === 0 ? (
            <Alert severity="info">No webmaster offers found.</Alert>
          ) : canReadWebmasterOffers && isFullOffers(visibleOffers) ? (
            <WebmasterOffersComparison
              offers={visibleOffers}
              showDomain={Boolean(multiResult)}
              startIndex={multiResult ? page * rowsPerPage : 0}
              expandedOfferIds={expandedOfferIds}
              onExpandedOfferChange={handleExpandedOfferChange}
              canEdit={canManageWebmasterOffers}
              onEditOffer={setEditingOfferId}
            />
          ) : (
            <WebmasterOffersSummaryTable
              offers={visibleOffers}
              showDomain={Boolean(multiResult)}
              startIndex={multiResult ? page * rowsPerPage : 0}
            />
          )}
          {multiResult && offers.length > 0 && (
            <TablePagination
              component="div"
              count={offers.length}
              page={page}
              rowsPerPage={rowsPerPage}
              rowsPerPageOptions={[25, 50, 100]}
              labelRowsPerPage="Offers per page:"
              onPageChange={(_, nextPage) => setPage(nextPage)}
              onRowsPerPageChange={(event) => {
                setRowsPerPage(Number(event.target.value));
                setPage(0);
              }}
            />
          )}
          {multiResult && (
            <>
              <WebmasterOffersDomainList
                title="Without offers"
                domains={multiResult.domainsWithoutOffers}
              />
              <WebmasterOffersDomainList
                title="Not in catalog"
                domains={multiResult.notFoundDomains}
              />
            </>
          )}
        </Stack>
      )}

      {canManageWebmasterOffers && (
        <EditWebmasterOfferDialog
          open={Boolean(editingOfferId)}
          offerId={editingOfferId}
          domain={editingDomain}
          onClose={() => setEditingOfferId(null)}
          onSaved={handleOfferSaved}
        />
      )}
    </PageShell>
  );
}
