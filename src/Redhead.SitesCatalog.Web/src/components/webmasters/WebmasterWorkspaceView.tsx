import {
  Alert,
  Box,
  Chip,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import WarningAmberOutlinedIcon from '@mui/icons-material/WarningAmberOutlined';
import { alpha } from '@mui/material/styles';
import { WebmasterOfferDetails } from '../webmaster-offers/WebmasterOfferDetails';
import {
  PRICE_GRID_TEMPLATE,
  PRICE_MATRIX_MIN_WIDTH,
  WebmasterOfferPriceCells,
  WebmasterOfferPriceHeader,
} from '../webmaster-offers/WebmasterOfferPriceMatrix';
import type { WebmasterOffer, WebmasterOfferStatus } from '../../types/webmasterOffers.types';
import type { WebmasterWorkspace } from '../../types/webmasters.types';

interface WebmasterWorkspaceViewProps {
  readonly workspace: WebmasterWorkspace;
}

function offerStatusLabel(status: WebmasterOfferStatus) {
  return status === 1 || status === 'Active' ? 'Active' : 'Inactive';
}

function offerCountLabel(count: number) {
  return `${count} ${count === 1 ? 'offer' : 'offers'}`;
}

function OfferStatusChip({ status }: { readonly status: WebmasterOfferStatus }) {
  const active = status === 1 || status === 'Active';
  return (
    <Chip
      size="small"
      label={offerStatusLabel(status)}
      variant="outlined"
      sx={(theme) => ({
        bgcolor: alpha(active ? theme.palette.success.main : theme.palette.error.main, 0.12),
        borderColor: alpha(active ? theme.palette.success.main : theme.palette.error.main, 0.3),
        color: active ? theme.palette.success.dark : theme.palette.error.dark,
        fontWeight: 600,
      })}
    />
  );
}

function OfferCard({ offer }: { readonly offer: WebmasterOffer }) {
  return (
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
      <Stack
        direction="row"
        alignItems="center"
        spacing={1.5}
        flexWrap="wrap"
        useFlexGap
        sx={{ px: 2, py: 1.5 }}
      >
        <OfferStatusChip status={offer.status} />
        <Chip size="small" label={offer.termLabel} variant="outlined" />
        <Typography variant="caption" color="text.secondary">
          Updated {new Date(offer.updatedAtUtc).toLocaleDateString()}
        </Typography>
      </Stack>

      <Box sx={{ overflowX: 'auto', borderTop: 1, borderBottom: 1, borderColor: 'divider' }}>
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: PRICE_GRID_TEMPLATE,
            minWidth: PRICE_MATRIX_MIN_WIDTH,
            bgcolor: 'grey.100',
            borderBottom: 1,
            borderColor: 'divider',
          }}
        >
          <WebmasterOfferPriceHeader />
        </Box>
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: PRICE_GRID_TEMPLATE,
            minWidth: PRICE_MATRIX_MIN_WIDTH,
          }}
        >
          <WebmasterOfferPriceCells offer={offer} />
        </Box>
      </Box>

      <Box sx={{ p: 2 }}>
        <WebmasterOfferDetails offer={offer} />
      </Box>
    </Paper>
  );
}

export function WebmasterWorkspaceView({ workspace }: WebmasterWorkspaceViewProps) {
  const { webmaster } = workspace;

  return (
    <Stack spacing={2.5}>
      <Paper variant="outlined" sx={{ p: 2 }}>
        <Box sx={{ minWidth: 0 }}>
          <Typography variant="h6" component="h2" sx={{ fontWeight: 600 }}>
            {webmaster.primaryEmail ?? 'Primary email not detected'}
          </Typography>
          <Typography
            variant="body2"
            color="text.secondary"
            sx={{ mt: 0.75, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}
          >
            {webmaster.representativeContactRawText || 'No representative Contact text'}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
            {webmaster.offerCount} offers · {webmaster.activeOfferCount} active ·{' '}
            {webmaster.domainCount} domains
          </Typography>
        </Box>
      </Paper>

      {workspace.domains.length === 0 && (
        <Alert severity="info">This webmaster has no offers.</Alert>
      )}

      {workspace.domains.map((domainGroup) => (
        <Paper key={domainGroup.domain} variant="outlined" sx={{ p: { xs: 1.5, sm: 2 } }}>
          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            alignItems={{ xs: 'flex-start', sm: 'center' }}
            justifyContent="space-between"
            spacing={1}
            sx={{ mb: 2 }}
          >
            <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
              {domainGroup.isQuarantined && (
                <WarningAmberOutlinedIcon color="error" fontSize="small" />
              )}
              <Typography variant="h6" component="h3" sx={{ fontWeight: 600 }}>
                {domainGroup.domain}
              </Typography>
              {!domainGroup.siteFound ? (
                <Chip size="small" color="warning" label="Not in catalog" />
              ) : domainGroup.isQuarantined ? (
                <Chip size="small" color="error" label="Unavailable · quarantined" />
              ) : null}
            </Stack>
            <Stack
              direction="row"
              spacing={1}
              alignItems="center"
              flexWrap="wrap"
              useFlexGap
            >
              <Chip
                size="small"
                variant="outlined"
                label={offerCountLabel(domainGroup.offers.length)}
                sx={{ bgcolor: 'background.paper' }}
              />
              {domainGroup.otherWebmasterOfferCount > 0 && (
                <Chip
                  size="small"
                  color="info"
                  variant="outlined"
                  label={`+${domainGroup.otherWebmasterOfferCount} from other webmasters`}
                />
              )}
            </Stack>
          </Stack>

          {domainGroup.isQuarantined && domainGroup.quarantineReason && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              {domainGroup.quarantineReason}
            </Alert>
          )}

          <Stack spacing={2}>
            {domainGroup.offers.map((offer) => (
              <OfferCard key={offer.id} offer={offer} />
            ))}
          </Stack>
        </Paper>
      ))}
    </Stack>
  );
}
