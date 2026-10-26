import { Chip, Stack } from '@mui/material';
import type { WebmasterOfferStatus } from '../../types/webmasterOffers.types';
import { OfferStatusChip } from './OfferStatusChip';

export function OfferTermChip({ label }: { readonly label: string }) {
  return (
    <Chip
      size="small"
      label={label || 'No term'}
      variant="outlined"
      sx={(theme) => ({
        height: theme.custom.table.statusHeight,
        borderColor: 'divider',
        bgcolor: 'background.paper',
        fontSize: theme.custom.table.secondaryFontSize,
        fontWeight: 400,
      })}
    />
  );
}

export function OfferTermStatus({
  termLabel,
  status,
}: {
  readonly termLabel: string;
  readonly status: WebmasterOfferStatus;
}) {
  return (
    <Stack
      spacing={0.5}
      alignItems="stretch"
      sx={{ width: 'max-content', minWidth: 96, maxWidth: '100%', '& > *': { width: '100%' } }}
    >
      <OfferStatusChip status={status} />
      <OfferTermChip label={termLabel} />
    </Stack>
  );
}
