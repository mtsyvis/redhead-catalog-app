import { Accordion, AccordionDetails, AccordionSummary, Stack, Typography } from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import type { LinkDetectiveSyncStatus } from '../../types/linkDetectiveSync.types';
import { formatInteger } from '../../utils/numberFormat';

export function LinkDetectiveSyncConfiguration({ status }: { status: LinkDetectiveSyncStatus }) {
  return (
    <Accordion disableGutters variant="outlined" sx={{ '&:before': { display: 'none' } }}>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        Schedule &amp; configuration
      </AccordionSummary>
      <AccordionDetails>
        <Stack direction="row" gap={3} flexWrap="wrap">
          {[
            `Scheduler: ${status.enabled ? 'enabled' : 'disabled'}`,
            `Daily selection: ${status.dailySelectionTimeUtc} UTC`,
            `Cache lifetime: ${status.cacheLifetimeDays} days`,
            `Batch size: ${status.batchSize}`,
            `On-demand reserve: ${formatInteger(status.onDemandReserve)}`,
            `Maximum attempts: ${status.maxAttempts}`,
            `Retry delay: ${status.retryDelayMinutes} min`,
          ].map((value) => (
            <Typography variant="body2" key={value}>
              {value}
            </Typography>
          ))}
        </Stack>
      </AccordionDetails>
    </Accordion>
  );
}
