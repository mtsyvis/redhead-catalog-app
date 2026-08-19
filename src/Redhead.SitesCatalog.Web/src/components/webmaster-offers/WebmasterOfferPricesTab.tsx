import { Box, MenuItem, TextField, Typography } from '@mui/material';
import { SERVICE_AVAILABILITY_STATUS } from '../../utils/serviceAvailability';
import {
  AVAILABILITY_OPTIONS,
  PRICE_TYPES,
  type OfferFormState,
  type PriceFormRow,
} from './editWebmasterOfferForm';

interface Props {
  readonly form: OfferFormState;
  readonly fieldErrors: Record<string, string[]>;
  readonly updatePrice: (priceType: number, patch: Partial<PriceFormRow>) => void;
}

export function WebmasterOfferPricesTab({ form, fieldErrors, updatePrice }: Props) {
  return (
    <Box sx={{ overflowX: 'auto' }}>
      <Box sx={{ minWidth: 760 }}>
        <Box sx={{ display: 'grid', gridTemplateColumns: '180px 180px 140px minmax(240px, 1fr)', gap: 1, px: 1, pb: 1 }}>
          {['Service', 'Availability', 'Price USD', 'Price details'].map((label) => (
            <Typography key={label} variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
              {label}
            </Typography>
          ))}
        </Box>
        {PRICE_TYPES.map((type) => {
          const row = form.prices[type.value];
          const amountError = fieldErrors[`prices.${type.value}.webmasterPriceUsd`]?.[0];
          const availabilityError = fieldErrors[`prices.${type.value}.availabilityStatus`]?.[0];
          const detailsError = fieldErrors[`prices.${type.value}.webmasterPriceDetails`]?.[0];
          return (
            <Box key={type.value} sx={{ display: 'grid', gridTemplateColumns: '180px 180px 140px minmax(240px, 1fr)', gap: 1, p: 1, borderTop: 1, borderColor: 'divider', alignItems: 'start' }}>
              <Typography variant="body2" sx={{ pt: 1 }}>{type.label}</Typography>
              <TextField
                select
                size="small"
                value={row.availabilityStatus}
                onChange={(event) => {
                  const status = Number(event.target.value);
                  updatePrice(type.value, {
                    availabilityStatus: status,
                    amount: status === SERVICE_AVAILABILITY_STATUS.Available ? row.amount : '',
                  });
                }}
                error={Boolean(availabilityError)}
                helperText={availabilityError}
              >
                {AVAILABILITY_OPTIONS
                  .filter((option) =>
                    type.value !== 0 ||
                    option.value === SERVICE_AVAILABILITY_STATUS.Available ||
                    option.value === SERVICE_AVAILABILITY_STATUS.Unknown)
                  .map((option) => (
                    <MenuItem key={option.value} value={option.value}>{option.label}</MenuItem>
                  ))}
              </TextField>
              <TextField size="small" type="number" inputProps={{ min: 0.01, step: 0.01 }} value={row.amount} disabled={row.availabilityStatus !== SERVICE_AVAILABILITY_STATUS.Available} onChange={(event) => updatePrice(type.value, { amount: event.target.value })} error={Boolean(amountError)} helperText={amountError} />
              <TextField size="small" value={row.details} onChange={(event) => updatePrice(type.value, { details: event.target.value })} multiline minRows={1} error={Boolean(detailsError)} helperText={detailsError} />
            </Box>
          );
        })}
      </Box>
    </Box>
  );
}
