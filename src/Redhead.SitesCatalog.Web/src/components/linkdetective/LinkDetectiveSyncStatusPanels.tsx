import { Box, IconButton, Paper, Stack, Tooltip, Typography } from '@mui/material';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import type { LinkDetectiveSyncStatus } from '../../types/linkDetectiveSync.types';
import { formatInteger } from '../../utils/numberFormat';
import { syncDate } from '../../utils/linkDetectiveSyncDisplay';

export function LinkDetectiveSyncStatusPanels({ status }: { status: LinkDetectiveSyncStatus }) {
  const quota = status.providerState;
  const rows = (values: Array<[string, string]>) => (
    <Box
      component="dl"
      sx={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: 1.5, m: 0, mt: 2 }}
    >
      {values.map(([label, value]) => (
        <Box key={label} sx={{ display: 'contents' }}>
          <Typography component="dt" variant="body2" color="text.secondary">
            {label}
          </Typography>
          <Typography
            component="dd"
            variant="body2"
            sx={{ m: 0, textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}
          >
            {value}
          </Typography>
        </Box>
      ))}
    </Box>
  );
  const observed = quota?.quotaObservedAtUtc != null;
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 2.5 }}>
      <Paper variant="outlined" sx={{ p: 2.5 }}>
        <Stack direction="row" alignItems="center" spacing={1}>
          <Typography variant="h6">Catalog coverage</Typography>
          <Tooltip title="Fresh includes successful checks with prices and successful no-price results. A failed check does not refresh saved data.">
            <IconButton size="small" aria-label="About cache freshness">
              <InfoOutlinedIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Stack>
        <Typography variant="h4" sx={{ mt: 1 }}>
          {formatInteger(status.catalogCount)}
        </Typography>
        {rows([
          [
            `Fresh checks · within ${status.cacheLifetimeDays} days`,
            formatInteger(status.freshCount),
          ],
          ['Outdated checks', formatInteger(status.outdatedCount)],
          ['Never checked', formatInteger(status.neverCheckedCount)],
        ])}
      </Paper>
      <Paper variant="outlined" sx={{ p: 2.5 }}>
        <Typography variant="h6">API quota · last observed</Typography>
        <Typography variant="h4" sx={{ mt: 1 }}>
          {observed ? formatInteger(quota?.quotaRemaining ?? 0) : '—'}
        </Typography>
        {rows([
          [
            'Provider limit',
            observed ? `${formatInteger(quota?.quotaLimit ?? 0)} / ${quota?.quotaPeriod}` : '—',
          ],
          ['Reserve for on-demand', formatInteger(status.onDemandReserve)],
          [
            'Available to background sync',
            observed
              ? formatInteger(Math.max(0, (quota?.quotaRemaining ?? 0) - status.onDemandReserve))
              : '—',
          ],
          ['Provider reset', syncDate(quota?.quotaResetAtUtc)],
        ])}
        <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
          Observed: {syncDate(quota?.quotaObservedAtUtc)}
        </Typography>
      </Paper>
    </Box>
  );
}
