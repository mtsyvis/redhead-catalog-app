import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Box,
  Button,
  Chip,
  IconButton,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import type { ServiceAvailabilityStatusValue } from '../../../types/sites.types';
import {
  SERVICE_AVAILABILITY_STATUS,
  SERVICE_AVAILABILITY_STATUS_OPTIONS,
} from '../../../utils/serviceAvailability';
import {
  PRICE_TYPE,
  TERM_KEY_OPTIONS,
  type PriceTypeValue,
  formatTermFilterLabel,
} from '../../../utils/pricing';
import {
  getPriceRowsForType,
  pricingAmountErrorKey,
  pricingStatusErrorKey,
  pricingTermErrorKey,
  PRICING_SECTIONS,
  type EditSiteFormState,
  type PricingPriceRow,
} from './EditSiteDialog.helpers';
import {
  hasPricingSectionErrors,
  type PricingExpansionState,
} from './EditSitePricingTab.utils';

interface Props {
  readonly form: EditSiteFormState;
  readonly fieldErrors: Record<string, string[]>;
  readonly expandedSections: PricingExpansionState;
  readonly onSectionChange: (priceType: PriceTypeValue, expanded: boolean) => void;
  readonly onServiceStatusChange: (
    priceType: PriceTypeValue,
    status: ServiceAvailabilityStatusValue
  ) => void;
  readonly onPriceRowChange: (
    rowId: string,
    key: 'termKey' | 'amountUsd',
    value: string
  ) => void;
  readonly onDeletePrice: (rowId: string) => void;
  readonly onAddPrice: (priceType: PriceTypeValue) => void;
}

function getTermOptionsForRow(row: PricingPriceRow) {
  if (TERM_KEY_OPTIONS.some((option) => option.termKey === row.termKey)) {
    return TERM_KEY_OPTIONS;
  }

  return [
    ...TERM_KEY_OPTIONS,
    { termKey: row.termKey, label: formatTermFilterLabel(row.termKey) },
  ];
}

function getAvailabilityLabel(status: ServiceAvailabilityStatusValue): string {
  return (
    SERVICE_AVAILABILITY_STATUS_OPTIONS.find((option) => option.value === status)?.label ??
    'Unknown'
  );
}

function getPricingHeaderSummary(
  priceType: PriceTypeValue,
  rows: PricingPriceRow[],
  status: ServiceAvailabilityStatusValue
): string {
  const termLabel = rows.length === 1 ? '1 term' : `${rows.length} terms`;

  if (priceType === PRICE_TYPE.Main) {
    return rows.length === 1 ? '1 price' : `${rows.length} prices`;
  }

  if (rows.length > 0 || status === SERVICE_AVAILABILITY_STATUS.Available) {
    return `Has price · ${termLabel}`;
  }

  return getAvailabilityLabel(status);
}

function getAvailabilityMessage(status: ServiceAvailabilityStatusValue): string | null {
  if (status === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice) return 'Price unknown.';
  if (status === SERVICE_AVAILABILITY_STATUS.NotAvailable) return 'Service is not available.';
  if (status === SERVICE_AVAILABILITY_STATUS.Unknown) return 'No availability information.';
  return null;
}

export function EditSitePricingTab({
  form,
  fieldErrors,
  expandedSections,
  onSectionChange,
  onServiceStatusChange,
  onPriceRowChange,
  onDeletePrice,
  onAddPrice,
}: Props) {
  const pricingColumns = [PRICING_SECTIONS.slice(0, 3), PRICING_SECTIONS.slice(3)];

  const renderSection = (section: (typeof PRICING_SECTIONS)[number]) => {
    const rows = getPriceRowsForType(form, section.priceType);
    const status =
      form.pricingStatuses[section.priceType] ?? SERVICE_AVAILABILITY_STATUS.Unknown;
    const rowsVisible =
      section.priceType === PRICE_TYPE.Main ||
      status === SERVICE_AVAILABILITY_STATUS.Available;
    const hasSectionErrors = hasPricingSectionErrors(
      section.priceType,
      rows,
      fieldErrors
    );
    const availabilityMessage = getAvailabilityMessage(status);

    return (
      <Accordion
        key={section.priceType}
        variant="outlined"
        disableGutters
        expanded={Boolean(expandedSections[section.priceType])}
        onChange={(_event, expanded) => onSectionChange(section.priceType, expanded)}
        sx={{
          borderRadius: 1,
          bgcolor: 'background.paper',
          '&::before': { display: 'none' },
          '& + &': { mt: 1 },
        }}
      >
        <AccordionSummary
          expandIcon={<ExpandMoreIcon />}
          sx={{
            minHeight: 44,
            px: 1.5,
            '&.Mui-expanded': { minHeight: 44 },
            '& .MuiAccordionSummary-content': {
              my: 0.75,
              alignItems: 'center',
              minWidth: 0,
            },
          }}
        >
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: 1.5,
              width: '100%',
              minWidth: 0,
            }}
          >
            <Typography variant="body2" sx={{ fontWeight: 700 }}>
              {section.label}
            </Typography>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
              {hasSectionErrors && (
                <Chip
                  label="Needs attention"
                  size="small"
                  color="error"
                  variant="outlined"
                />
              )}
              <Typography variant="body2" color="text.secondary" sx={{ whiteSpace: 'nowrap' }}>
                {getPricingHeaderSummary(section.priceType, rows, status)}
              </Typography>
            </Box>
          </Box>
        </AccordionSummary>

        <AccordionDetails sx={{ px: 1.5, pt: 0, pb: 1.25 }}>
          <Stack spacing={1}>
            {section.isOptional && (
              <TextField
                select
                label="Availability"
                value={status}
                onChange={(event) =>
                  onServiceStatusChange(
                    section.priceType,
                    Number(event.target.value) as ServiceAvailabilityStatusValue
                  )
                }
                size="small"
                inputProps={{ 'aria-label': `${section.label} availability` }}
                sx={{ maxWidth: 240 }}
                error={Boolean(fieldErrors[pricingStatusErrorKey(section.priceType)]?.length)}
                helperText={fieldErrors[pricingStatusErrorKey(section.priceType)]?.[0]}
              >
                {SERVICE_AVAILABILITY_STATUS_OPTIONS.map((option) => (
                  <MenuItem key={option.value} value={option.value}>
                    {option.label}
                  </MenuItem>
                ))}
              </TextField>
            )}

            {rowsVisible && rows.length > 0 && (
              <Stack spacing={0.5}>
                <Box
                  sx={{
                    display: { xs: 'none', sm: 'grid' },
                    gridTemplateColumns: 'minmax(160px, 1fr) 130px 32px',
                    gap: 1,
                    alignItems: 'center',
                    px: 0.25,
                  }}
                >
                  <Typography variant="caption" color="text.secondary">Term</Typography>
                  <Typography variant="caption" color="text.secondary">Amount USD</Typography>
                </Box>
                {rows.map((row) => (
                  <Box
                    key={row.id}
                    sx={{
                      display: 'grid',
                      gridTemplateColumns: {
                        xs: 'minmax(0, 1fr) minmax(92px, 120px) 32px',
                        sm: 'minmax(160px, 1fr) 130px 32px',
                      },
                      gap: 0.75,
                      py: 0.25,
                      alignItems: 'flex-start',
                    }}
                  >
                    <TextField
                      select
                      value={row.termKey}
                      onChange={(event) =>
                        onPriceRowChange(row.id, 'termKey', event.target.value)
                      }
                      size="small"
                      inputProps={{ 'aria-label': `${section.label} term` }}
                      error={Boolean(fieldErrors[pricingTermErrorKey(row.id)]?.length)}
                      helperText={fieldErrors[pricingTermErrorKey(row.id)]?.[0]}
                    >
                      {getTermOptionsForRow(row).map((option) => (
                        <MenuItem key={option.termKey} value={option.termKey}>
                          {option.label}
                        </MenuItem>
                      ))}
                    </TextField>
                    <TextField
                      type="number"
                      inputProps={{
                        min: 1,
                        step: '1',
                        'aria-label': `${section.label} amount USD`,
                      }}
                      value={row.amountUsd}
                      onChange={(event) =>
                        onPriceRowChange(row.id, 'amountUsd', event.target.value)
                      }
                      size="small"
                      error={Boolean(fieldErrors[pricingAmountErrorKey(row.id)]?.length)}
                      helperText={fieldErrors[pricingAmountErrorKey(row.id)]?.[0]}
                    />
                    <IconButton
                      aria-label={`Delete ${section.label} price`}
                      onClick={() => onDeletePrice(row.id)}
                      size="small"
                      sx={{ mt: 0.25, width: 32, height: 32 }}
                    >
                      <DeleteOutlineIcon fontSize="small" />
                    </IconButton>
                  </Box>
                ))}
              </Stack>
            )}

            {!rowsVisible && availabilityMessage && (
              <Typography variant="body2" color="text.secondary">
                {availabilityMessage}
              </Typography>
            )}

            <Box>
              <Button
                variant="text"
                color="primary"
                size="small"
                startIcon={<AddIcon fontSize="small" />}
                aria-label={`Add ${section.label} term price`}
                onClick={() => onAddPrice(section.priceType)}
                sx={{ textTransform: 'none', px: 0.5 }}
              >
                Add term price
              </Button>
            </Box>
          </Stack>
        </AccordionDetails>
      </Accordion>
    );
  };

  return (
    <Box
      sx={{
        display: 'grid',
        gridTemplateColumns: { xs: '1fr', md: 'minmax(0, 1fr) minmax(0, 1fr)' },
        gap: 2,
      }}
    >
      {pricingColumns.map((sections, index) => (
        <Box key={index}>{sections.map(renderSection)}</Box>
      ))}
    </Box>
  );
}
