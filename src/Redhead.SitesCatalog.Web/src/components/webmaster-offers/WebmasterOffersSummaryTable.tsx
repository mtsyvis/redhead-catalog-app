import {
  Box,
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
import { getTableSurfaceStyles } from '../../theme/tableStyles';
import { OfferStatusChip } from './OfferStatusChip';
import { OfferMailboxes } from './OfferMailboxes';

const textColumns = [
  ['linkPolicyText', 'Link policy', 180],
  ['dfLinksRawText', 'DF links', 140],
  ['sponsoredTagRawText', 'Sponsored tag', 180],
  ['outreachSenderRawText', 'Outreach sender', 320],
  ['linkbuilderMailboxRawText', 'Linkbuilder mailbox (raw)', 240],
] as const;

function formatDate(value: string) {
  return dayjs(value).isValid() ? dayjs(value).format('DD MMM YYYY, HH:mm') : null;
}

function textValue(value: string | null | undefined) {
  const text = value?.trim();
  return text && text !== '—' ? (
    text
  ) : (
    <Box component="span" sx={{ color: 'text.secondary' }}>
      —
    </Box>
  );
}

export function WebmasterOffersSummaryTable({
  offers,
}: {
  readonly offers: WebmasterOfferSummary[];
}) {
  return (
    <TableContainer
      component={Paper}
      variant="outlined"
      role="region"
      aria-label="Scrollable webmaster offer conditions"
      tabIndex={0}
      sx={getTableSurfaceStyles}
    >
      <Table
        size="small"
        aria-label="Webmaster offer conditions"
        sx={{
          minWidth: 3200,
          tableLayout: 'fixed',
          borderCollapse: 'separate',
          borderSpacing: 0,
          '& td': { verticalAlign: 'top', whiteSpace: 'pre-wrap', overflowWrap: 'break-word' },
          '& .OfferTable-nowrap': { whiteSpace: 'nowrap' },
        }}
      >
        <TableHead>
          <TableRow>
            <TableCell sx={{ width: 64 }} aria-label="Offer number">#</TableCell>
            <TableCell sx={{ width: 110 }}>Status</TableCell>
            <TableCell sx={{ width: 140 }}>Term</TableCell>
            {textColumns.map(([field, label, width]) => (
              <TableCell key={field} sx={{ width }}>
                {label}
              </TableCell>
            ))}
            <TableCell sx={{ width: 340 }}>Linkbuilder mailboxes</TableCell>
            <TableCell sx={{ width: 420 }}>Comments</TableCell>
            <TableCell sx={{ width: 200 }}>Client</TableCell>
            <TableCell sx={{ width: 220 }}>Imported term</TableCell>
            <TableCell sx={{ width: 190 }}>Created</TableCell>
            <TableCell sx={{ width: 190 }}>Updated</TableCell>
            <TableCell sx={{ width: 260 }}>Updated by</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {offers.map((offer, index) => (
            <TableRow key={offer.id} hover>
              <TableCell
                className="OfferTable-nowrap"
                sx={{ fontWeight: 600 }}
              >
                #{index + 1}
              </TableCell>
              <TableCell>
                <OfferStatusChip status={offer.status} />
              </TableCell>
              <TableCell className="OfferTable-nowrap">{textValue(offer.termLabel)}</TableCell>
              {textColumns.map(([field]) => (
                <TableCell key={field}>{textValue(offer[field])}</TableCell>
              ))}
              <TableCell>
                {offer.linkbuilderMailboxes.length > 0 ? (
                  <OfferMailboxes mailboxes={offer.linkbuilderMailboxes} />
                ) : (
                  textValue(null)
                )}
              </TableCell>
              <TableCell>{textValue(offer.commentText)}</TableCell>
              <TableCell>{textValue(offer.clientRawText)}</TableCell>
              <TableCell>{textValue(offer.termRawText)}</TableCell>
              <TableCell className="OfferTable-nowrap">
                {textValue(formatDate(offer.createdAtUtc))}
              </TableCell>
              <TableCell className="OfferTable-nowrap">
                {textValue(formatDate(offer.updatedAtUtc))}
              </TableCell>
              <TableCell>{textValue(offer.updatedBy)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
