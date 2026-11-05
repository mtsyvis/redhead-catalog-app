import { Tooltip, Typography } from '@mui/material';
import { formatUsd } from '../../utils/numberFormat';

export function MarginAmount({
  amount,
  label = true,
  tooltip,
}: {
  amount: number | null | undefined;
  label?: boolean;
  tooltip?: string;
}) {
  const value = (
    <Typography
      tabIndex={tooltip ? 0 : undefined}
      variant="caption"
      component="div"
      sx={{
        color: amount == null ? 'text.secondary' : amount <= 0 ? 'error.main' : 'success.main',
        fontWeight: 500,
        fontVariantNumeric: 'tabular-nums',
        whiteSpace: 'nowrap',
        mt: 0.25,
      }}
    >
      {amount == null
        ? '—'
        : `${label ? 'Margin ' : ''}${amount > 0 ? '+' : amount < 0 ? '−' : ''}${amount === 0 ? '$0' : formatUsd(Math.abs(amount))}`}
    </Typography>
  );
  return tooltip ? <Tooltip title={tooltip}>{value}</Tooltip> : value;
}
