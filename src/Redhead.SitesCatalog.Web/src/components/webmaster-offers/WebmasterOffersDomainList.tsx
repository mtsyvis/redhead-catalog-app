import { useState } from 'react';
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Typography,
} from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { CopyToClipboardIconButton } from '../common/CopyToClipboardIconButton';

export function WebmasterOffersDomainList({
  title,
  domains,
}: {
  readonly title: string;
  readonly domains: string[];
}) {
  const [copyError, setCopyError] = useState(false);
  if (domains.length === 0) return null;

  return (
    <Accordion variant="outlined" disableGutters sx={{ '&::before': { display: 'none' } }}>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Typography variant="body2">
          {title} — {domains.length}
        </Typography>
      </AccordionSummary>
      <AccordionDetails>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
          <Typography variant="body2" color="text.secondary">
            Copy domains
          </Typography>
          <CopyToClipboardIconButton
            value={domains.join('\n')}
            copyLabel={`Copy ${title.toLowerCase()} domains`}
            copiedLabel="Copied"
            onCopyError={() => setCopyError(true)}
            onCopySuccess={() => setCopyError(false)}
          />
        </Box>
        {copyError && (
          <Alert severity="error" sx={{ mb: 1 }}>
            Could not copy domains
          </Alert>
        )}
        <Typography
          variant="body2"
          sx={{
            whiteSpace: 'pre-wrap',
            overflowWrap: 'anywhere',
            maxHeight: 240,
            overflowY: 'auto',
          }}
        >
          {domains.join('\n')}
        </Typography>
      </AccordionDetails>
    </Accordion>
  );
}
