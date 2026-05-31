import { useEffect, useRef, useState } from 'react';
import type { ClipboardEvent } from 'react';
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
  const inputRef = useRef<HTMLTextAreaElement | null>(null);
  const [draft, setDraft] = useState(() => formatStopListInput(domains));
  const [error, setError] = useState<string | null>(null);
  const [parsedDraft, setParsedDraft] = useState(() => parseStopListInput(formatStopListInput(domains)));
  const stopListLimitExceeded = parsedDraft.domains.length > STOP_LIST_MAX_DOMAINS;

  useEffect(() => {
    const timeoutId = globalThis.setTimeout(() => {
      setParsedDraft(parseStopListInput(draft));
    }, 300);

    return () => globalThis.clearTimeout(timeoutId);
  }, [draft]);

  const handleApply = () => {
    const result = parseStopListInput(draft);

    if (result.domains.length > STOP_LIST_MAX_DOMAINS) {
      setError(
        `Stop list accepts at most ${STOP_LIST_MAX_DOMAINS} unique domains. Current input has ${result.domains.length}.`
      );
      return;
    }

    if (result.invalidValues.length > 0) {
      setError(formatInvalidStopListError(result.invalidValues));
      return;
    }

    onApply(result.domains);
  };

  const handleClear = () => {
    setDraft('');
    setError(null);
    onClear();
  };

  const handlePaste = (event: ClipboardEvent<HTMLDivElement>) => {
    const pastedText = event.clipboardData.getData('text');
    const trimmedText = trimStopListDraft(pastedText);
    if (trimmedText === pastedText) {
      return;
    }

    event.preventDefault();

    const input = inputRef.current;
    const selectionStart = input?.selectionStart ?? draft.length;
    const selectionEnd = input?.selectionEnd ?? draft.length;
    const nextDraft = trimStopListDraft(
      `${draft.slice(0, selectionStart)}${trimmedText}${draft.slice(selectionEnd)}`
    );

    setDraft(nextDraft);
    setError(null);

    requestAnimationFrame(() => {
      if (!inputRef.current) {
        return;
      }

      const cursorPosition = Math.min(selectionStart + trimmedText.length, nextDraft.length);
      inputRef.current.selectionStart = cursorPosition;
      inputRef.current.selectionEnd = cursorPosition;
    });
  };

  const handleBlur = () => {
    const trimmedDraft = trimStopListDraft(draft);
    if (trimmedDraft !== draft) {
      setDraft(trimmedDraft);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Stop list</DialogTitle>
      <DialogContent sx={{ overflow: 'visible' }}>
        <Stack spacing={1.5} sx={{ pt: 1 }}>
          <Typography variant="body2" color="text.secondary">
            Paste domains or URLs to exclude from search results. One per line, or separated by spaces, commas, semicolons, or tabs.
          </Typography>
          <TextField
            multiline
            rows={12}
            inputRef={inputRef}
            value={draft}
            onChange={(e) => {
              setDraft(e.target.value);
              setError(null);
            }}
            onPaste={handlePaste}
            onBlur={handleBlur}
            placeholder="example.com&#10;https://example.com/page?x=1"
            fullWidth
            error={Boolean(error)}
            sx={{
              '& textarea': {
                overflow: 'auto',
              },
            }}
          />
          <Typography
            variant="caption"
            color={stopListLimitExceeded ? 'error.main' : 'text.secondary'}
          >
            {formatStopListSummary(parsedDraft)}
          </Typography>
          {stopListLimitExceeded && (
            <Alert severity="error">
              Stop list accepts at most {STOP_LIST_MAX_DOMAINS} unique domains.
            </Alert>
          )}
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <BrandButton onClick={onClose}>Cancel</BrandButton>
        <BrandButton onClick={handleClear} disabled={domains.length === 0 && draft.trim() === ''}>
          Clear
        </BrandButton>
        <BrandButton kind="primary" onClick={handleApply} disabled={stopListLimitExceeded}>
          Apply
        </BrandButton>
      </DialogActions>
    </Dialog>
  );
}

function formatStopListSummary(result: ReturnType<typeof parseStopListInput>): string {
  const details = [
    `${result.domains.length} / ${STOP_LIST_MAX_DOMAINS} domains`,
    result.duplicateCount > 0 ? `${result.duplicateCount} duplicates ignored` : null,
    result.invalidValues.length > 0 ? `${result.invalidValues.length} invalid` : null,
  ].filter(Boolean);

  return details.join(' | ');
}

function trimStopListDraft(value: string): string {
  return value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line !== '')
    .join('\n');
}

function formatInvalidStopListError(invalidValues: string[]): string {
  const visibleValues = invalidValues.slice(0, 10).join(', ');
  const remainingCount = invalidValues.length - 10;
  return remainingCount > 0
    ? `Invalid domains: ${visibleValues}, and ${remainingCount} more. Enter valid domains or URLs.`
    : `Invalid domains: ${visibleValues}. Enter valid domains or URLs.`;
}
