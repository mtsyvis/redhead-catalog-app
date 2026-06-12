import {
  Box,
  Chip,
  Divider,
  Drawer,
  IconButton,
  Stack,
  Typography,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import type { Site } from '../../../types/sites.types';
import {
  PRICE_TYPE,
  PRICE_TYPE_LABELS,
  PRICING_SECTION_ORDER,
  type PriceTypeValue,
  formatUsd,
  getFullTermLabel,
  getPrices,
  getServiceStatus,
} from '../../../utils/pricing';
import {
  SERVICE_AVAILABILITY_STATUS,
  SERVICE_AVAILABILITY_STATUS_OPTIONS,
  normalizeServiceAvailabilityStatus,
} from '../../../utils/serviceAvailability';

const PRICE_TYPE_TO_COLUMN_ID: Record<PriceTypeValue, string> = {
  [PRICE_TYPE.Main]: 'priceUsd',
  [PRICE_TYPE.Casino]: 'priceCasino',
  [PRICE_TYPE.Crypto]: 'priceCrypto',
  [PRICE_TYPE.LinkInsertion]: 'priceLinkInsert',
  [PRICE_TYPE.LinkInsertionCasino]: 'priceLinkInsertCasino',
  [PRICE_TYPE.Dating]: 'priceDating',
};

interface PricingDetailsDrawerProps {
  open: boolean;
  site: Site | null;
  visibleColumnIds: string[];
  onClose: () => void;
}

function getStatusLabel(status: number): string {
  return SERVICE_AVAILABILITY_STATUS_OPTIONS.find((option) => option.value === status)?.label ?? 'Unknown';
}

function statusChipColor(status: number) {
  if (status === SERVICE_AVAILABILITY_STATUS.Available) return 'success';
  if (status === SERVICE_AVAILABILITY_STATUS.NotAvailable) return 'error';
  if (status === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice) return 'info';
  return 'default';
}

export function PricingDetailsDrawer({
  open,
  site,
  visibleColumnIds,
  onClose,
}: PricingDetailsDrawerProps) {
  const visibleColumns = new Set(visibleColumnIds);
  const visibleSections = PRICING_SECTION_ORDER.filter((priceType) =>
    visibleColumns.has(PRICE_TYPE_TO_COLUMN_ID[priceType])
  );

  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
      PaperProps={{
        sx: {
          width: { xs: '100%', sm: 460 },
          maxWidth: '100%',
          display: 'flex',
          flexDirection: 'column',
          bgcolor: 'background.paper',
          overflow: 'hidden',
        },
      }}
    >
      <Box
        sx={{
          px: 2,
          py: 1.5,
          display: 'flex',
          alignItems: 'flex-start',
          gap: 1,
          borderBottom: 1,
          borderColor: 'divider',
        }}
      >
        <Box sx={{ flexGrow: 1, minWidth: 0 }}>
          <Typography variant="h6">Pricing</Typography>
          {site && (
            <Typography variant="body2" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>
              {site.domain}
            </Typography>
          )}
        </Box>
        <IconButton aria-label="Close pricing details" onClick={onClose} size="small">
          <CloseIcon fontSize="small" />
        </IconButton>
      </Box>

      <Box sx={{ flex: 1, minHeight: 0, overflow: 'auto', p: 2 }}>
        {!site ? null : visibleSections.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            No pricing columns are visible in the current table view.
          </Typography>
        ) : (
          <Stack spacing={1.5}>
            {visibleSections.map((priceType) =>
              priceType === PRICE_TYPE.Main ? (
                <PricingSection key={priceType} site={site} priceType={priceType} />
              ) : (
                <OptionalServicePricingSection key={priceType} site={site} priceType={priceType} />
              )
            )}
          </Stack>
        )}
      </Box>
    </Drawer>
  );
}

function PricingSection({
  site,
  priceType,
}: {
  site: Site;
  priceType: PriceTypeValue;
}) {
  const prices = getPrices(site, priceType);

  return (
    <Box
      sx={{
        border: 1,
        borderColor: 'divider',
        borderRadius: 1,
        p: 1.5,
        bgcolor: 'background.paper',
      }}
    >
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
        {PRICE_TYPE_LABELS[priceType]}
      </Typography>

      {prices.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          —
        </Typography>
      ) : (
        <Stack divider={<Divider flexItem />} spacing={0}>
          {prices.map((price) => (
            <Box
              key={`${price.priceType}:${price.termKey}`}
              sx={{
                display: 'grid',
                gridTemplateColumns: 'minmax(0, 1fr) auto',
                gap: 1.5,
                py: 0.75,
              }}
            >
              <Typography variant="body2" color="text.secondary">
                {getFullTermLabel(price)}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {formatUsd(price.amountUsd)}
              </Typography>
            </Box>
          ))}
        </Stack>
      )}
    </Box>
  );
}

function OptionalServicePricingSection({
  site,
  priceType,
}: {
  site: Site;
  priceType: PriceTypeValue;
}) {
  const prices = getPrices(site, priceType);
  const status = prices.length > 0
    ? SERVICE_AVAILABILITY_STATUS.Available
    : normalizeServiceAvailabilityStatus(getServiceStatus(site, priceType));
  const isYes = status === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice;

  return (
    <Box
      sx={{
        border: 1,
        borderColor: 'divider',
        borderRadius: 1,
        p: 1.5,
        bgcolor: 'background.paper',
      }}
    >
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
        {PRICE_TYPE_LABELS[priceType]}
      </Typography>

      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: prices.length > 0 || isYes ? 1 : 0 }}>
        <Typography variant="body2" color="text.secondary">
          Status:
        </Typography>
        <Chip
          label={getStatusLabel(status)}
          size="small"
          color={statusChipColor(status)}
          variant="outlined"
        />
      </Stack>

      {prices.length > 0 && (
        <Stack divider={<Divider flexItem />} spacing={0}>
          {prices.map((price) => (
            <Box
              key={`${price.priceType}:${price.termKey}`}
              sx={{
                display: 'grid',
                gridTemplateColumns: 'minmax(0, 1fr) auto',
                gap: 1.5,
                py: 0.75,
              }}
            >
              <Typography variant="body2" color="text.secondary">
                {getFullTermLabel(price)}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {formatUsd(price.amountUsd)}
              </Typography>
            </Box>
          ))}
        </Stack>
      )}

      {isYes && prices.length === 0 && (
        <Typography variant="body2" color="text.secondary">
          Price unknown
        </Typography>
      )}
    </Box>
  );
}
