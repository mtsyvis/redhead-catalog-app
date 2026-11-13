import { DEFAULT_CURRENCY } from '../../constants/currency.constants';
import { formatOfferRateSource } from '../../utils/currencyDisplay';
import { Box, Button, MenuItem, TextField, Tooltip, Typography } from '@mui/material';
import type { OfferPriceConversion, WebmasterOfferEdit } from '../../types/webmasterOffers.types';
import { SERVICE_AVAILABILITY_STATUS } from '../../utils/serviceAvailability';
import {
  AVAILABILITY_OPTIONS,
  PRICE_TYPES,
  quoteAmountKey,
  type OfferFormState,
  type PriceFormRow,
} from './editWebmasterOfferForm';

interface Props {
  readonly form: OfferFormState;
  readonly fieldErrors: Record<string, string[]>;
  readonly updatePrice: (priceType: number, patch: Partial<PriceFormRow>) => void;
  readonly edit: WebmasterOfferEdit;
  readonly currencies: string[];
  readonly previews: {
    conversions: Record<number, OfferPriceConversion | undefined>;
    errors: Record<number, string | undefined>;
    pending: boolean;
    retry: () => void;
  };
}

const PRICE_COLUMNS = '160px 160px 230px minmax(280px, 1fr)';

export function WebmasterOfferPricesTab({
  form,
  fieldErrors,
  updatePrice,
  edit,
  currencies,
  previews,
}: Props) {
  return (
    <Box sx={{ overflowX: 'auto' }}>
      <Box sx={{ minWidth: 860 }}>
        <Box sx={{ display: 'grid', gridTemplateColumns: PRICE_COLUMNS, gap: 1, px: 1, pb: 1 }}>
          {['Service', 'Availability', 'Price', 'Price details'].map((label) => (
            <Typography
              key={label}
              variant="caption"
              color="text.secondary"
              sx={{ fontWeight: 600 }}
            >
              {label}
            </Typography>
          ))}
        </Box>
        {PRICE_TYPES.map((type) => {
          const row = form.prices[type.value];
          const amountError =
            fieldErrors[`prices.${type.value}.sourceAmount`]?.[0] ??
            fieldErrors[`prices.${type.value}.sourceCurrency`]?.[0] ??
            fieldErrors[`prices.${type.value}.webmasterPriceUsd`]?.[0];
          const saved = edit.offer.prices.find(
            (price) => price.priceType === type.value || price.priceType === type.enumName
          );
          const unchanged =
            saved?.sourceCurrency === row.currency &&
            quoteAmountKey(saved.sourceAmount) === quoteAmountKey(row.amount);
          const preview = previews.conversions[type.value];
          const conversion = unchanged ? saved : preview;
          const numeric = row.availabilityStatus === SERVICE_AVAILABILITY_STATUS.Available;
          const foreign =
            numeric &&
            row.currency !== DEFAULT_CURRENCY &&
            /^\d+(?:\.\d{1,2})?$/.test(row.amount) &&
            Number(row.amount) > 0;
          const rateDetail = conversion
            ? `${conversion.appliedUsdRate} USD per ${row.currency}`
            : '';
          const availabilityError = fieldErrors[`prices.${type.value}.availabilityStatus`]?.[0];
          const detailsError = fieldErrors[`prices.${type.value}.webmasterPriceDetails`]?.[0];
          return (
            <Box
              key={type.value}
              sx={{
                display: 'grid',
                gridTemplateColumns: PRICE_COLUMNS,
                gap: 1,
                p: 1,
                borderTop: 1,
                borderColor: 'divider',
                alignItems: 'start',
              }}
            >
              <Typography variant="body2" sx={{ pt: 1 }}>
                {type.value === 0 ? 'Main' : type.label}
              </Typography>
              <TextField
                select
                size="small"
                value={row.availabilityStatus}
                slotProps={{
                  select: { inputProps: { 'aria-label': `${type.enumName} availability` } },
                }}
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
                {AVAILABILITY_OPTIONS.filter(
                  (option) =>
                    type.value !== 0 ||
                    option.value === SERVICE_AVAILABILITY_STATUS.Available ||
                    option.value === SERVICE_AVAILABILITY_STATUS.Unknown
                ).map((option) => (
                  <MenuItem key={option.value} value={option.value}>
                    {option.label}
                  </MenuItem>
                ))}
              </TextField>
              <Box>
                <Box sx={{ display: 'flex', gap: 0.5 }}>
                  <TextField
                    size="small"
                    type="number"
                    inputProps={{
                      min: 0.01,
                      step: 0.01,
                      'aria-label': `${type.enumName} quote amount`,
                    }}
                    value={row.amount}
                    disabled={!numeric}
                    onChange={(event) => updatePrice(type.value, { amount: event.target.value })}
                    error={Boolean(amountError)}
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    select
                    size="small"
                    value={row.currency}
                    disabled={!numeric}
                    onChange={(event) => updatePrice(type.value, { currency: event.target.value })}
                    slotProps={{
                      select: { inputProps: { 'aria-label': `${type.enumName} quote currency` } },
                    }}
                    sx={{ minWidth: 85 }}
                  >
                    {[...new Set([...currencies, row.currency])].map((code) => (
                      <MenuItem key={code} value={code}>
                        {code}
                      </MenuItem>
                    ))}
                  </TextField>
                </Box>
                {amountError && (
                  <Typography variant="caption" color="error">
                    {amountError}
                  </Typography>
                )}
                {foreign && conversion && (
                  <>
                    <Typography variant="caption" display="block">
                      Price USD: ${conversion.webmasterPriceUsd}
                    </Typography>
                    <Tooltip title={rateDetail}>
                      <Typography
                        variant="caption"
                        color="text.secondary"
                        tabIndex={0}
                        sx={{ cursor: 'help' }}
                      >
                        {formatOfferRateSource(row.currency, conversion.rateDate)}{' '}
                        ⓘ
                      </Typography>
                    </Tooltip>
                  </>
                )}
                {foreign && !unchanged && previews.pending && !conversion && !previews.errors[type.value] && (
                  <Typography variant="caption" color="text.secondary">
                    Loading conversion…
                  </Typography>
                )}
                {foreign && (previews.errors[type.value] || amountError) && (
                  <Box>
                    {previews.errors[type.value] && (
                      <Typography variant="caption" color="error">
                        {previews.errors[type.value]}
                      </Typography>
                    )}
                    <Button size="small" onClick={() => {
                      updatePrice(type.value, {});
                      previews.retry();
                    }}>Refresh conversion</Button>
                  </Box>
                )}
              </Box>
              <TextField
                size="small"
                value={row.details}
                onChange={(event) => updatePrice(type.value, { details: event.target.value })}
                inputProps={{ 'aria-label': `${type.enumName} price details` }}
                multiline
                minRows={1}
                error={Boolean(detailsError)}
                helperText={detailsError}
              />
            </Box>
          );
        })}
      </Box>
    </Box>
  );
}
