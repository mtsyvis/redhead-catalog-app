import { useEffect, useState } from 'react';
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Typography,
} from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import type { EntityChangeHistoryItem } from '../../types/changeHistory.types';
import { useChangeHistory } from '../../hooks/useChangeHistory';
import { ChangeHistoryContent } from './ChangeHistoryContent';

interface Props {
  readonly entityKey: string;
  readonly loadHistory: () => Promise<EntityChangeHistoryItem[]>;
}

export function ChangeHistoryAccordion({ entityKey, loadHistory }: Props) {
  const [expanded, setExpanded] = useState(false);
  const { items, loading, error, load } = useChangeHistory(entityKey, loadHistory);

  useEffect(() => {
    if (expanded) void load();
  }, [expanded, load]);

  return (
    <Accordion
      expanded={expanded}
      onChange={(_event, nextExpanded) => setExpanded(nextExpanded)}
      variant="outlined"
      disableGutters
      sx={{ '&::before': { display: 'none' } }}
    >
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Typography variant="subtitle2">Change history</Typography>
      </AccordionSummary>
      <AccordionDetails sx={{ pt: 0 }}>
        <ChangeHistoryContent
          items={items}
          loading={loading}
          error={error}
        />
      </AccordionDetails>
    </Accordion>
  );
}
