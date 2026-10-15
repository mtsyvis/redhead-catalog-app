import { useState } from 'react';
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Chip,
  IconButton,
  Paper,
  Snackbar,
  Stack,
  Typography,
} from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import type { WebmasterOffer } from '../../types/webmasterOffers.types';
import {
  getPinnedTableCellStyles,
  getTableCellStyles,
  getTableHeaderStyles,
  getTableSurfaceStyles,
} from '../../theme/tableStyles';
import { CopyToClipboardIconButton } from '../common/CopyToClipboardIconButton';
import { OfferStatusChip } from './OfferStatusChip';
import { WebmasterOfferDetails } from './WebmasterOfferDetails';
import {
  PRICE_GRID_TEMPLATE,
  PRICE_MATRIX_MIN_WIDTH,
  WebmasterOfferPriceCells,
  WebmasterOfferPriceHeader,
} from './WebmasterOfferPriceMatrix';

const OFFER_WIDTH = 120;
const STATUS_TERM_WIDTH = 190;
const PRIMARY_EMAIL_WIDTH = 220;
const METADATA_WIDTH = OFFER_WIDTH + STATUS_TERM_WIDTH + PRIMARY_EMAIL_WIDTH;
const COMPARISON_GRID_TEMPLATE = `${OFFER_WIDTH}px ${STATUS_TERM_WIDTH}px ${PRIMARY_EMAIL_WIDTH}px ${PRICE_GRID_TEMPLATE}`;
const COMPARISON_MIN_WIDTH = METADATA_WIDTH + PRICE_MATRIX_MIN_WIDTH;

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

function PrimaryEmailValue({
  value,
  onCopySuccess,
  onCopyError,
}: {
  readonly value: string | null | undefined;
  readonly onCopySuccess: () => void;
  readonly onCopyError: () => void;
}) {
  const email = value?.trim();

  return (
    <Box
      sx={{ display: 'flex', alignItems: 'center', gap: 0.5, minWidth: 0, width: '100%' }}
      onClick={(event) => event.stopPropagation()}
      onMouseDown={(event) => event.stopPropagation()}
      onKeyDown={(event) => event.stopPropagation()}
    >
      <Typography
        variant="body2"
        color={email ? 'text.primary' : 'text.secondary'}
        sx={{ flex: 1, minWidth: 0, overflowWrap: 'anywhere', whiteSpace: 'pre-wrap', userSelect: 'text' }}
      >
        {email || '—'}
      </Typography>
      {email && (
        <CopyToClipboardIconButton
          key={email}
          value={email}
          copyLabel="Copy Primary Email"
          copiedLabel="Copied"
          onCopySuccess={onCopySuccess}
          onCopyError={onCopyError}
        />
      )}
    </Box>
  );
}

function ComparisonHeaderCell({ children, pinned = false }: {
  readonly children: string;
  readonly pinned?: boolean;
}) {
  return (
    <Box
      sx={(theme) => ({
        ...getTableHeaderStyles(theme),
        ...(pinned ? getPinnedTableCellStyles(theme, true) : {}),
        minHeight: theme.custom.table.headerHeight,
        display: 'flex',
        alignItems: 'center',
        minWidth: 0,
      })}
    >
      <Typography variant="body2" sx={{ fontWeight: 'inherit', lineHeight: 'inherit' }}>
        {children}
      </Typography>
    </Box>
  );
}

function DesktopComparisonHeader({ canEdit }: { readonly canEdit: boolean }) {
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
      <ComparisonHeaderCell pinned={canEdit}>#</ComparisonHeaderCell>
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
  readonly canEdit: boolean;
  readonly onEdit: () => void;
  readonly onPrimaryEmailCopySuccess: () => void;
  readonly onPrimaryEmailCopyError: () => void;
}

function DesktopOfferRow({
  offer,
  index,
  expanded,
  onChange,
  canEdit,
  onEdit,
  onPrimaryEmailCopySuccess,
  onPrimaryEmailCopyError,
}: OfferRowProps) {
  return (
    <Accordion
      expanded={expanded}
      onChange={(_event, isExpanded) => onChange(isExpanded)}
      slots={{ heading: 'div' }}
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
        component="div"
        role="group"
        tabIndex={-1}
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
          '&:hover': { bgcolor: 'grey.50' },
        }}
      >
        <Box
          sx={(theme) => ({
            ...getTableCellStyles(theme),
            ...(canEdit ? getPinnedTableCellStyles(theme) : {}),
            minHeight: theme.custom.table.minRowHeight,
            minWidth: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 0.5,
            ...(canEdit && {
              '.MuiAccordionSummary-root:hover &': { backgroundColor: theme.palette.grey[50] },
            }),
          })}
        >
          <Typography variant="body2" sx={{ fontWeight: 600, whiteSpace: 'nowrap' }}>
            #{index + 1}
          </Typography>
          <Stack direction="row" spacing={0.25} alignItems="center">
            {canEdit && (
              <IconButton
                aria-label={`Edit offer ${index + 1}`}
                size="small"
                onClick={(event) => {
                  event.stopPropagation();
                  onEdit();
                }}
                onFocus={(event) => event.stopPropagation()}
              >
                <EditOutlinedIcon fontSize="small" />
              </IconButton>
            )}
            <IconButton
              aria-label={`${expanded ? 'Collapse' : 'Expand'} offer ${index + 1}`}
              aria-expanded={expanded}
              size="small"
            >
              <ExpandMoreIcon
                fontSize="small"
                sx={{ transform: expanded ? 'rotate(180deg)' : 'none', transition: 'transform 150ms' }}
              />
            </IconButton>
          </Stack>
        </Box>
        <Box
          sx={(theme) => ({
            ...getTableCellStyles(theme),
            minWidth: 0,
            display: 'flex',
            alignItems: 'center',
            gap: 0.75,
            flexWrap: 'wrap',
          })}
        >
          <OfferStatusChip status={offer.status} />
          <TermChip label={offer.termLabel || 'No term'} />
        </Box>
        <Box
          sx={(theme) => ({
            ...getTableCellStyles(theme),
            minWidth: 0,
            display: 'flex',
            alignItems: 'center',
          })}
        >
          <PrimaryEmailValue
            value={offer.primaryEmail}
            onCopySuccess={onPrimaryEmailCopySuccess}
            onCopyError={onPrimaryEmailCopyError}
          />
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

function MobileOfferRow({
  offer,
  index,
  expanded,
  onChange,
  canEdit,
  onEdit,
  onPrimaryEmailCopySuccess,
  onPrimaryEmailCopyError,
}: OfferRowProps) {
  return (
    <Accordion
      expanded={expanded}
      onChange={(_event, isExpanded) => onChange(isExpanded)}
      slots={{ heading: 'div' }}
      variant="outlined"
      disableGutters
      sx={{ '&::before': { display: 'none' }, '& + &': { mt: 1 } }}
    >
      <AccordionSummary
        component="div"
        role="group"
        tabIndex={-1}
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
              #{index + 1}
            </Typography>
            <OfferStatusChip status={offer.status} />
            <TermChip label={offer.termLabel || 'No term'} />
          </Stack>
          <Stack direction="row" spacing={0.25} alignItems="center">
            {canEdit && (
              <IconButton
                aria-label={`Edit offer ${index + 1}`}
                size="small"
                onClick={(event) => {
                  event.stopPropagation();
                  onEdit();
                }}
                onFocus={(event) => event.stopPropagation()}
              >
                <EditOutlinedIcon fontSize="small" />
              </IconButton>
            )}
            <IconButton
              aria-label={`${expanded ? 'Collapse' : 'Expand'} offer ${index + 1}`}
              aria-expanded={expanded}
              size="small"
            >
              <ExpandMoreIcon
                fontSize="small"
                sx={{ flexShrink: 0, transform: expanded ? 'rotate(180deg)' : 'none' }}
              />
            </IconButton>
          </Stack>
        </Box>
        <Box sx={{ mb: 1.5 }}>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
            Primary Email
          </Typography>
          <PrimaryEmailValue
            value={offer.primaryEmail}
            onCopySuccess={onPrimaryEmailCopySuccess}
            onCopyError={onPrimaryEmailCopyError}
          />
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
  readonly canEdit: boolean;
  readonly onEditOffer: (offerId: string) => void;
}

export function WebmasterOffersComparison({
  offers,
  expandedOfferIds,
  onExpandedOfferChange,
  canEdit,
  onEditOffer,
}: WebmasterOffersComparisonProps) {
  const [copyError, setCopyError] = useState<string | null>(null);
  const handlePrimaryEmailCopySuccess = () => setCopyError(null);
  const handlePrimaryEmailCopyError = () => setCopyError('Could not copy Primary Email');

  return (
    <>
      <Paper
        variant="outlined"
        role="region"
        aria-label="Scrollable webmaster offer comparison"
        tabIndex={0}
        sx={(theme) => ({
          ...getTableSurfaceStyles(theme),
          display: { xs: 'none', md: 'block' },
          overflowX: 'auto',
        })}
      >
        <DesktopComparisonHeader canEdit={canEdit} />
        {offers.map((offer, index) => (
          <DesktopOfferRow
            key={offer.id}
            offer={offer}
            index={index}
            expanded={expandedOfferIds.has(offer.id)}
            onChange={(expanded) => onExpandedOfferChange(offer.id, expanded)}
            canEdit={canEdit}
            onEdit={() => onEditOffer(offer.id)}
            onPrimaryEmailCopySuccess={handlePrimaryEmailCopySuccess}
            onPrimaryEmailCopyError={handlePrimaryEmailCopyError}
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
            canEdit={canEdit}
            onEdit={() => onEditOffer(offer.id)}
            onPrimaryEmailCopySuccess={handlePrimaryEmailCopySuccess}
            onPrimaryEmailCopyError={handlePrimaryEmailCopyError}
          />
        ))}
      </Box>

      <Snackbar
        open={copyError !== null}
        autoHideDuration={4000}
        onClose={() => setCopyError(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity="error" onClose={() => setCopyError(null)}>
          {copyError}
        </Alert>
      </Snackbar>
    </>
  );
}
