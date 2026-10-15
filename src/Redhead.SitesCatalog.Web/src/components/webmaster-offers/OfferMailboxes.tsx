import { Stack, Typography } from '@mui/material';
import type { WebmasterOfferMailbox } from '../../types/webmasterOffers.types';

export function OfferMailboxes({
  mailboxes,
}: {
  readonly mailboxes: readonly WebmasterOfferMailbox[];
}) {
  return (
    <Stack spacing={1.25} sx={{ minWidth: 0, overflowWrap: 'anywhere' }}>
      {mailboxes.map((mailbox) => (
        <Stack key={mailbox.id} spacing={0.25}>
          {mailbox.displayName && mailbox.displayName !== mailbox.email && (
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {mailbox.displayName}
            </Typography>
          )}
          <Typography variant="body2" sx={{ overflowWrap: 'anywhere', whiteSpace: 'pre-wrap' }}>
            {mailbox.email}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {mailbox.source === 'Manual' || mailbox.source === 2 ? 'Manual' : 'Import'}
          </Typography>
        </Stack>
      ))}
    </Stack>
  );
}
