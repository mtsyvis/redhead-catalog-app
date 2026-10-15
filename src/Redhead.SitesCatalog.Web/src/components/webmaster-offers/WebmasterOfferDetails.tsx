import { Box, Stack, Typography } from '@mui/material';
import type { WebmasterOffer } from '../../types/webmasterOffers.types';
import { OfferMailboxes } from './OfferMailboxes';

export function WebmasterOfferTextBlock({
  label,
  value,
}: {
  readonly label: string;
  readonly value: string | null | undefined;
}) {
  if (!value?.trim()) {
    return null;
  }

  return (
    <Box>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
        {label}
      </Typography>
      <Typography
        variant="body2"
        color={value.trim() === '—' ? 'text.secondary' : 'text.primary'}
        sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', lineHeight: 1.45 }}
      >
        {value}
      </Typography>
    </Box>
  );
}

export function WebmasterOfferDetails({ offer }: { readonly offer: WebmasterOffer }) {
  return (
    <Box
      sx={{
        display: 'grid',
        gridTemplateColumns: {
          xs: '1fr',
          sm: 'repeat(2, minmax(0, 1fr))',
          lg: 'repeat(3, minmax(0, 1fr))',
        },
        gap: 3,
      }}
    >
      <Stack spacing={1.5}>
        <WebmasterOfferTextBlock label="Contact" value={offer.contactRawText} />
        <WebmasterOfferTextBlock label="Outreach Sender" value={offer.outreachSenderRawText} />
        {(offer.linkbuilderMailboxes.length > 0 || Boolean(offer.linkbuilderMailboxRawText?.trim())) && (
          <Box>
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{ display: 'block', mb: 0.75 }}
            >
              Linkbuilder
            </Typography>
            {offer.linkbuilderMailboxes.length > 0 && (
              <OfferMailboxes mailboxes={offer.linkbuilderMailboxes} />
            )}
            {offer.linkbuilderMailboxes.length === 0 && (
              <Typography
                variant="body2"
                sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', lineHeight: 1.45 }}
              >
                {offer.linkbuilderMailboxRawText}
              </Typography>
            )}
          </Box>
        )}
      </Stack>

      <Stack spacing={1.5}>
        <WebmasterOfferTextBlock label="Sponsored Tag" value={offer.sponsoredTagRawText} />
        <WebmasterOfferTextBlock label="Link Policy" value={offer.linkPolicyText} />
        <WebmasterOfferTextBlock label="DF Links" value={offer.dfLinksRawText} />
        <WebmasterOfferTextBlock label="Imported Term" value={offer.termRawText} />
      </Stack>

      <Stack spacing={1.5}>
        <WebmasterOfferTextBlock label="Comments" value={offer.commentText} />
        <WebmasterOfferTextBlock label="Client" value={offer.clientRawText} />
      </Stack>
    </Box>
  );
}
