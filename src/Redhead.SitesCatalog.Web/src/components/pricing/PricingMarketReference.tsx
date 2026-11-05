import { marketDescription } from '../../utils/pricingDisplay';
import { Box, Stack, Typography } from '@mui/material';
import type { PricingMarket } from '../../types/pricing.types';
import { formatUsd } from '../../utils/numberFormat';

export function PricingMarketReference({ market }: { market: PricingMarket | null }) {
  return (
    <Box sx={{ p: 1.5, border: 1, borderColor: 'divider', borderRadius: 1 }}>
      <Stack direction="row" flexWrap="wrap" gap={2} alignItems="baseline">
        <Typography variant="body2" fontWeight={600}>
          LinkDetective
        </Typography>
        {market?.checkedAtUtc &&
          (
            [
              ['Minimum', market.minimumUsd],
              ['Average', market.averageUsd],
              ['Median', market.medianUsd],
            ] as const
          ).map(([label, amount]) => (
            <Typography key={label} variant="body2">
              <Typography component="span" variant="caption" color="text.secondary">
                {label}{' '}
              </Typography>
              {formatUsd(amount)}
            </Typography>
          ))}
      </Stack>
      <Typography variant="caption" color={market?.lastError ? 'warning.dark' : 'text.secondary'}>
        {marketDescription(market)}
      </Typography>
    </Box>
  );
}
