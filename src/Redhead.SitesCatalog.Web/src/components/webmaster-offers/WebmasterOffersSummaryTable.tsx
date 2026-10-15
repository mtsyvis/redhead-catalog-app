import {
  Chip,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
} from '@mui/material';
import dayjs from 'dayjs';
import type { WebmasterOfferSummary } from '../../types/webmasterOffers.types';

const textColumns = [
  ['linkPolicyText', 'Link policy'],
  ['dfLinksRawText', 'DF links'],
  ['sponsoredTagRawText', 'Sponsored tag'],
  ['outreachSenderRawText', 'Outreach sender'],
  ['linkbuilderMailboxRawText', 'Linkbuilder mailbox (raw)'],
] as const;

function formatDate(value: string) {
  return dayjs(value).isValid() ? dayjs(value).format('DD MMM YYYY, HH:mm') : '—';
}

export function WebmasterOffersSummaryTable({
  offers,
}: {
  readonly offers: WebmasterOfferSummary[];
}) {
  return (
    <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 1 }}>
      <Table
        size="small"
        aria-label="Webmaster offer conditions"
        sx={{
          minWidth: 2500,
          tableLayout: 'fixed',
          '& th': { bgcolor: 'action.hover', color: 'text.secondary', fontWeight: 500 },
          '& td': { verticalAlign: 'top', whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' },
        }}
      >
        <TableHead>
          <TableRow>
            <TableCell sx={{ width: 100 }}>Offer</TableCell>
            <TableCell sx={{ width: 110 }}>Status</TableCell>
            <TableCell sx={{ width: 120 }}>Term</TableCell>
            {textColumns.map(([field, label]) => (
              <TableCell key={field} sx={{ width: 180 }}>{label}</TableCell>
            ))}
            <TableCell sx={{ width: 260 }}>Linkbuilder mailboxes</TableCell>
            <TableCell sx={{ width: 280 }}>Comments</TableCell>
            <TableCell sx={{ width: 180 }}>Client</TableCell>
            <TableCell sx={{ width: 180 }}>Imported term</TableCell>
            <TableCell sx={{ width: 170 }}>Created</TableCell>
            <TableCell sx={{ width: 170 }}>Updated</TableCell>
            <TableCell sx={{ width: 200 }}>Updated by</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {offers.map((offer, index) => {
            const inactive = offer.status === 'Inactive' || offer.status === 2;
            return (
              <TableRow key={offer.id} hover>
                <TableCell sx={{ fontWeight: 500 }}>Offer {index + 1}</TableCell>
                <TableCell>
                  <Chip
                    size="small"
                    variant="outlined"
                    color={inactive ? 'error' : 'success'}
                    label={inactive ? 'Inactive' : 'Active'}
                  />
                </TableCell>
                <TableCell>{offer.termLabel || '—'}</TableCell>
                {textColumns.map(([field]) => (
                  <TableCell key={field}>{offer[field]?.trim() || '—'}</TableCell>
                ))}
                <TableCell>
                  {offer.linkbuilderMailboxes.map((mailbox) => {
                    const label = mailbox.displayName && mailbox.displayName !== mailbox.email
                      ? `${mailbox.displayName} <${mailbox.email}>`
                      : mailbox.email;
                    const source = mailbox.source === 'Manual' || mailbox.source === 2 ? 'Manual' : 'Import';
                    return `${label} (${source})`;
                  }).join('\n') || '—'}
                </TableCell>
                <TableCell>{offer.commentText?.trim() || '—'}</TableCell>
                <TableCell>{offer.clientRawText?.trim() || '—'}</TableCell>
                <TableCell>{offer.termRawText?.trim() || '—'}</TableCell>
                <TableCell>{formatDate(offer.createdAtUtc)}</TableCell>
                <TableCell>{formatDate(offer.updatedAtUtc)}</TableCell>
                <TableCell>{offer.updatedBy || '—'}</TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
