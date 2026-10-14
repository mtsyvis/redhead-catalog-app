import { useEffect, useState } from 'react';
import { Alert, Box, CircularProgress, LinearProgress, Skeleton, Typography } from '@mui/material';
import RefreshIcon from '@mui/icons-material/Refresh';
import DownloadIcon from '@mui/icons-material/Download';
import { catalogCoverageAnalyticsService } from '../../services/catalogCoverageAnalytics.service';
import type { CatalogCoverageAnalytics as CatalogCoverageReport } from '../../types/catalogCoverageAnalytics.types';
import { formatInteger } from '../../utils/numberFormat';
import { BrandButton } from '../common/BrandButton';
import { AnalyticsInfo, AnalyticsMetrics } from './AnalyticsShared';

export function CatalogCoverageAnalytics({ active }: { active: boolean }) {
  const [data, setData] = useState<CatalogCoverageReport | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [refresh, setRefresh] = useState(0);
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  useEffect(() => {
    if (!active) return;
    let cancelled = false;
    setLoading(true);
    setData(null);
    setError(null);
    catalogCoverageAnalyticsService.get()
      .then((result) => { if (!cancelled) setData(result); })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Failed to load catalog coverage');
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [active, refresh]);

  const download = async () => {
    setDownloading(true);
    setDownloadError(null);
    try {
      await catalogCoverageAnalyticsService.downloadDomainsWithoutOffers();
    } catch (err) {
      setDownloadError(err instanceof Error ? err.message : 'Failed to download domains without offers');
    } finally {
      setDownloading(false);
    }
  };

  const percent = data?.coveragePercent.toLocaleString('en-US', { maximumFractionDigits: 2 }) ?? '0';

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
          <Typography variant="body2" color="text.secondary">
            {data ? `Updated ${new Date(data.generatedAtUtc).toLocaleString()}` : 'Current catalog coverage'}
          </Typography>
          <AnalyticsInfo label="Catalog coverage" text="Current catalog state, including quarantined sites. A site is covered when it has at least one Active or Inactive webmaster offer, regardless of price, contact or term." />
        </Box>
        <BrandButton size="small" startIcon={loading ? <CircularProgress size={16} color="inherit" /> : <RefreshIcon />} disabled={loading} onClick={() => setRefresh((value) => value + 1)}>
          Refresh
        </BrandButton>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}
      {loading && (
        <Box aria-label="Loading catalog coverage" sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Skeleton height={36} />
          <Skeleton variant="rounded" height={96} />
          <Skeleton height={36} />
        </Box>
      )}
      {data && !loading && (
        <>
          <AnalyticsMetrics inline metrics={[
            { label: 'Sites in catalog', value: data.catalogSites, helperText: 'Unique catalog domains, including quarantined sites.' },
            { label: 'Covered sites', value: data.coveredSites, helperText: 'Unique catalog domains with at least one offer. Multiple offers for a domain count as one covered site.' },
            { label: 'Sites without offers', value: data.sitesWithoutOffers, helperText: 'Catalog domains with no webmaster offers of either status.' },
            { label: 'Webmaster offers', value: data.totalOffers, helperText: 'All stored offers. Different prices, contacts or terms remain separate offers.' },
            { label: 'Active offers', value: data.activeOffers },
            { label: 'Inactive offers', value: data.inactiveOffers },
          ]} />

          <Box sx={{ py: 2, borderTop: 1, borderBottom: 1, borderColor: 'divider' }}>
            <Box sx={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1, mb: 1 }}>
              <Typography sx={{ fontSize: 24, fontWeight: 600 }}>{percent}% covered</Typography>
              <Typography variant="body2" color="text.secondary">
                {formatInteger(data.coveredSites)} of {formatInteger(data.catalogSites)} sites
              </Typography>
            </Box>
            <LinearProgress variant="determinate" value={data.coveragePercent} aria-label="Catalog coverage" aria-valuetext={`${percent}% of catalog sites covered by webmaster offers`} sx={{ height: 10, borderRadius: 1 }} />
          </Box>

          <AnalyticsMetrics inline metrics={[
            { label: 'Sites with only Inactive offers', value: data.sitesWithOnlyInactiveOffers, helperText: 'These sites are included in coverage, but have no Active offers.' },
            { label: 'Quarantined sites', value: data.quarantinedSites, helperText: 'Quarantined sites are included in the catalog totals and coverage calculation.' },
            { label: 'Quarantined sites without offers', value: data.quarantinedSitesWithoutOffers, helperText: 'Included in the domains download. Subtract this count from sites without offers to find non-quarantined sites needing offers.' },
          ]} />

          {data.catalogSites === 0 && <Alert severity="info">The catalog is empty.</Alert>}
          {data.catalogSites > 0 && data.sitesWithoutOffers === 0 && <Alert severity="success">Every catalog site has at least one webmaster offer.</Alert>}
          <Box>
            <BrandButton kind="primary" startIcon={downloading ? <CircularProgress size={18} color="inherit" /> : <DownloadIcon />} disabled={downloading || data.sitesWithoutOffers === 0} onClick={() => void download()}>
              {downloading ? 'Preparing Excel…' : 'Download domains without offers'}
            </BrandButton>
          </Box>
        </>
      )}
      {downloadError && <Alert severity="error">{downloadError}</Alert>}
    </Box>
  );
}
