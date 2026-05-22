import { useState } from 'react';
import {
  Alert,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { BrandButton } from '../../common/BrandButton';
import {
  formatStopListInput,
  parseStopListInput,
  STOP_LIST_MAX_DOMAINS,
} from '../../../utils/stopList';

interface StopListDialogProps {
  open: boolean;
  domains: string[];
  onClose: () => void;
  onApply: (domains: string[]) => void;
  onClear: () => void;
}

export function StopListDialog({
  open,
  domains,
  onClose,
  onApply,
  onClear,
}: StopListDialogProps) {
  const [draft, setDraft] = useState(() => formatStopListInput(domains));
  const [error, setError] = useState<string | null>(null);

  const parsedDraft = parseStopListInput(draft);

  const handleApply = () => {
    const result = parseStopListInput(draft);

    if (result.invalidValues.length > 0) {
      setError(formatInvalidStopListError(result.invalidValues));
      return;
    }

    if (result.domains.length > STOP_LIST_MAX_DOMAINS) {
      setError(
        `Stop list accepts at most ${STOP_LIST_MAX_DOMAINS} unique domains. Current input has ${result.domains.length}.`
      );
      return;
    }

    onApply(result.domains);
  };

  const handleClear = () => {
    setDraft('');
    setError(null);
    onClear();
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Stop list</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Typography variant="body2" color="text.secondary">
            Paste domains or URLs to exclude from search results. One per line, or separated by spaces, commas, semicolons, or tabs.
          </Typography>
          <TextField
            multiline
            minRows={8}
            maxRows={14}
            value={draft}
            onChange={(e) => {
              setDraft(e.target.value);
              setError(null);
            }}
            placeholder="example.com&#10;https://example.com/page?x=1"
            fullWidth
            error={Boolean(error)}
          />
          <Typography variant="caption" color="text.secondary">
            {parsedDraft.domains.length} / {STOP_LIST_MAX_DOMAINS} domains
          </Typography>
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <BrandButton onClick={onClose}>Cancel</BrandButton>
        <BrandButton onClick={handleClear} disabled={domains.length === 0 && draft.trim() === ''}>
          Clear
        </BrandButton>
        <BrandButton kind="primary" onClick={handleApply}>
          Apply
        </BrandButton>
      </DialogActions>
    </Dialog>
  );
}

function formatInvalidStopListError(invalidValues: string[]): string {
  const visibleValues = invalidValues.slice(0, 10).join(', ');
  const remainingCount = invalidValues.length - 10;
  return remainingCount > 0
    ? `Invalid domains: ${visibleValues}, and ${remainingCount} more. Enter valid domains or URLs.`
    : `Invalid domains: ${visibleValues}. Enter valid domains or URLs.`;
}
