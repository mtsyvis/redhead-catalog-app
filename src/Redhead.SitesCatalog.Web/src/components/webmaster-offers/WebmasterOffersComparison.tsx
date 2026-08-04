import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Box,
  Chip,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { alpha } from '@mui/material/styles';
import type { WebmasterOffer, WebmasterOfferStatus } from '../../types/webmasterOffers.types';
import {
  WebmasterOfferDetails,
  WebmasterOfferTextBlock,
} from './WebmasterOfferDetails';
import {
  PRICE_GRID_TEMPLATE,
  PRICE_MATRIX_MIN_WIDTH,
  WebmasterOfferPriceCells,
  WebmasterOfferPriceHeader,
} from './WebmasterOfferPriceMatrix';

const OFFER_WIDTH = 112;
const STATUS_TERM_WIDTH = 168;
const PRIMARY_EMAIL_WIDTH = 220;
const METADATA_WIDTH = OFFER_WIDTH + STATUS_TERM_WIDTH + PRIMARY_EMAIL_WIDTH;
const COMPARISON_GRID_TEMPLATE = `${OFFER_WIDTH}px ${STATUS_TERM_WIDTH}px ${PRIMARY_EMAIL_WIDTH}px ${PRICE_GRID_TEMPLATE}`;
const COMPARISON_MIN_WIDTH = METADATA_WIDTH + PRICE_MATRIX_MIN_WIDTH;

const STATUS_LABELS: Record<number | string, string> = {
  1: 'Active',
  2: 'Inactive',
  Active: 'Active',
  Inactive: 'Inactive',
};

function statusLabel(status: WebmasterOfferStatus) {
  return STATUS_LABELS[status] ?? String(status);
}

function OfferStatusChip({ status }: { readonly status: WebmasterOfferStatus }) {
  const inactive = status === 2 || status === 'Inactive';

  return (
    <Chip
      size="small"
      label={statusLabel(status)}
      variant="outlined"
      sx={(theme) => ({
        height: 24,
        borderColor: inactive
          ? alpha(theme.palette.error.main, 0.32)
          : alpha(theme.palette.success.main, 0.28),
        bgcolor: inactive
          ? alpha(theme.palette.error.main, 0.18)
          : alpha(theme.palette.success.main, 0.14),
        color: inactive ? theme.palette.error.dark : theme.palette.success.dark,
        fontSize: 12,
        fontWeight: 600,
      })}
    />
  );
}

function TermChip({ label }: { readonly label: string }) {
  return (
    <Chip
      size="small"
      label={label}
      variant="outlined"
      sx={{ height: 24, borderColor: 'divider', fontSize: 12, fontWeight: 400 }}
    />
  );
}

function ComparisonHeaderCell({ children }: { readonly children: string }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', px: 1.5, py: 1, minWidth: 0 }}>
      <Typography variant="body2" sx={{ fontWeight: 600 }}>
        {children}
      </Typography>
    </Box>
  );
}

function DesktopComparisonHeader() {
  return (
    <Box
      sx={{
        display: 'grid',
        gridTemplateColumns: COMPARISON_GRID_TEMPLATE,
        minWidth: COMPARISON_MIN_WIDTH,
        borderBottom: 1,
        borderColor: 'divider',
        bgcolor: 'grey.100',
      }}
    >
      <ComparisonHeaderCell>Offer</ComparisonHeaderCell>
      <ComparisonHeaderCell>Status &amp; Term</ComparisonHeaderCell>
      <ComparisonHeaderCell>Primary Email</ComparisonHeaderCell>
      <WebmasterOfferPriceHeader />
    </Box>
  );
}

interface OfferRowProps {
  readonly offer: WebmasterOffer;
  readonly index: number;
  readonly expanded: boolean;
  readonly onChange: (expanded: boolean) => void;
}

function DesktopOfferRow({ offer, index, expanded, onChange }: OfferRowProps) {
  return (
    <Accordion
      expanded={expanded}
      onChange={(_event, isExpanded) => onChange(isExpanded)}
      disableGutters
      square
      elevation={0}
      sx={{
        minWidth: COMPARISON_MIN_WIDTH,
        '&::before': { display: 'none' },
        '&:not(:last-of-type)': { borderBottom: 1, borderColor: 'divider' },
      }}
    >
      <AccordionSummary
        aria-controls={`webmaster-offer-${offer.id}-details`}
        id={`webmaster-offer-${offer.id}-summary`}
        sx={{
          p: 0,
          minHeight: 0,
          '&.Mui-expanded': { minHeight: 0 },
          '& .MuiAccordionSummary-content': {
            display: 'grid',
            gridTemplateColumns: COMPARISON_GRID_TEMPLATE,
            width: '100%',
            minWidth: COMPARISON_MIN_WIDTH,
            m: 0,
          },
          '& .MuiAccordionSummary-content.Mui-expanded': { m: 0 },
          '&:hover': { bgcolor: 'action.hover' },
        }}
      >
        <Box
          sx={{
            px: 1.5,
            py: 1,
            minWidth: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 0.5,
          }}
        >
          <Typography variant="body2" sx={{ fontWeight: 600, whiteSpace: 'nowrap' }}>
            Offer {index + 1}
          </Typography>
          <ExpandMoreIcon
            fontSize="small"
            sx={{ transform: expanded ? 'rotate(180deg)' : 'none', transition: 'transform 150ms' }}
          />
        </Box>
        <Box
          sx={{
            px: 1.25,
            py: 1,
            minWidth: 0,
            display: 'flex',
            alignItems: 'center',
            gap: 0.75,
            flexWrap: 'wrap',
          }}
        >
          <OfferStatusChip status={offer.status} />
          <TermChip label={offer.termLabel || 'No term'} />
        </Box>
        <Box sx={{ px: 1.5, py: 1, minWidth: 0, display: 'flex', alignItems: 'center' }}>
          <Typography
            variant="body2"
            color={offer.primaryEmail?.trim() ? 'text.primary' : 'text.secondary'}
            sx={{ overflowWrap: 'anywhere' }}
          >
            {offer.primaryEmail || '—'}
          </Typography>
        </Box>
        <WebmasterOfferPriceCells offer={offer} />
      </AccordionSummary>

      <AccordionDetails
        id={`webmaster-offer-${offer.id}-details`}
        aria-labelledby={`webmaster-offer-${offer.id}-summary`}
        sx={{ p: 0, borderTop: 1, borderColor: 'divider' }}
      >
        <Box
          sx={{
            position: 'sticky',
            left: 0,
            width: 1120,
            maxWidth: 'calc(100vw - 64px)',
            px: 3,
            py: 2.5,
            bgcolor: 'background.paper',
          }}
        >
          <WebmasterOfferDetails offer={offer} />
        </Box>
      </AccordionDetails>
    </Accordion>
  );
}

function MobileOfferRow({ offer, index, expanded, onChange }: OfferRowProps) {
  return (
    <Accordion
      expanded={expanded}
      onChange={(_event, isExpanded) => onChange(isExpanded)}
      variant="outlined"
      disableGutters
      sx={{ '&::before': { display: 'none' }, '& + &': { mt: 1 } }}
    >
      <AccordionSummary
        aria-controls={`mobile-webmaster-offer-${offer.id}-details`}
        id={`mobile-webmaster-offer-${offer.id}-summary`}
        sx={{
          px: 1.5,
          py: 1,
          '& .MuiAccordionSummary-content': { display: 'block', minWidth: 0, my: 0 },
          '& .MuiAccordionSummary-content.Mui-expanded': { my: 0 },
        }}
      >
        <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 1, mb: 1 }}>
          <Stack direction="row" spacing={0.75} alignItems="center" flexWrap="wrap" useFlexGap>
            <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
              Offer {index + 1}
            </Typography>
            <OfferStatusChip status={offer.status} />
            <TermChip label={offer.termLabel || 'No term'} />
          </Stack>
          <ExpandMoreIcon
            fontSize="small"
            sx={{ flexShrink: 0, transform: expanded ? 'rotate(180deg)' : 'none' }}
          />
        </Box>
        <Box sx={{ mb: 1.5 }}>
          <WebmasterOfferTextBlock label="Primary Email" value={offer.primaryEmail || '—'} />
        </Box>
        <Box
          sx={{ overflowX: 'auto', mx: -1.5, px: 1.5, pb: 0.5 }}
          onClick={(event) => event.stopPropagation()}
          onKeyDown={(event) => event.stopPropagation()}
        >
          <Box sx={{ minWidth: PRICE_MATRIX_MIN_WIDTH }}>
            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: PRICE_GRID_TEMPLATE,
                borderBottom: 1,
                borderColor: 'divider',
              }}
            >
              <WebmasterOfferPriceHeader />
            </Box>
            <Box sx={{ display: 'grid', gridTemplateColumns: PRICE_GRID_TEMPLATE }}>
              <WebmasterOfferPriceCells offer={offer} />
            </Box>
          </Box>
        </Box>
      </AccordionSummary>
      <AccordionDetails
        id={`mobile-webmaster-offer-${offer.id}-details`}
        aria-labelledby={`mobile-webmaster-offer-${offer.id}-summary`}
        sx={{ px: 1.5, pt: 1.5, pb: 2, borderTop: 1, borderColor: 'divider' }}
      >
        <WebmasterOfferDetails offer={offer} />
      </AccordionDetails>
    </Accordion>
  );
}

export interface WebmasterOffersComparisonProps {
  readonly offers: readonly WebmasterOffer[];
  readonly expandedOfferIds: ReadonlySet<string>;
  readonly onExpandedOfferChange: (offerId: string, expanded: boolean) => void;
}

export function WebmasterOffersComparison({
  offers,
  expandedOfferIds,
  onExpandedOfferChange,
}: WebmasterOffersComparisonProps) {
  return (
    <>
      <Paper variant="outlined" sx={{ display: { xs: 'none', md: 'block' }, overflowX: 'auto' }}>
        <DesktopComparisonHeader />
        {offers.map((offer, index) => (
          <DesktopOfferRow
            key={offer.id}
            offer={offer}
            index={index}
            expanded={expandedOfferIds.has(offer.id)}
            onChange={(expanded) => onExpandedOfferChange(offer.id, expanded)}
          />
        ))}
      </Paper>

      <Box sx={{ display: { xs: 'block', md: 'none' } }}>
        {offers.map((offer, index) => (
          <MobileOfferRow
            key={offer.id}
            offer={offer}
            index={index}
            expanded={expandedOfferIds.has(offer.id)}
            onChange={(expanded) => onExpandedOfferChange(offer.id, expanded)}
          />
        ))}
      </Box>
    </>
  );
}
