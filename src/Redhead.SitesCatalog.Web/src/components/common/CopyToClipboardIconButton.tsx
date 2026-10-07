import { useEffect, useRef, useState, type MouseEvent } from 'react';
import CheckIcon from '@mui/icons-material/Check';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import { IconButton, Tooltip } from '@mui/material';

import { writeTextToClipboard } from '../../utils/clipboard';

interface CopyToClipboardIconButtonProps {
  readonly value: string;
  readonly copyLabel: string;
  readonly copiedLabel: string;
  readonly onCopySuccess?: () => void;
  readonly onCopyError: () => void;
}

export function CopyToClipboardIconButton({
  value,
  copyLabel,
  copiedLabel,
  onCopySuccess,
  onCopyError,
}: CopyToClipboardIconButtonProps) {
  const [copied, setCopied] = useState(false);
  const feedbackTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(
    () => () => {
      if (feedbackTimer.current !== null) {
        clearTimeout(feedbackTimer.current);
      }
    },
    [],
  );

  const handleCopy = async (event: MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();
    event.stopPropagation();

    try {
      await writeTextToClipboard(value);
      setCopied(true);
      onCopySuccess?.();

      if (feedbackTimer.current !== null) {
        clearTimeout(feedbackTimer.current);
      }

      feedbackTimer.current = setTimeout(() => {
        setCopied(false);
        feedbackTimer.current = null;
      }, 2000);
    } catch {
      setCopied(false);
      onCopyError();
    }
  };

  return (
    <Tooltip title={copied ? copiedLabel : copyLabel}>
      <IconButton
        aria-label={copied ? copiedLabel : copyLabel}
        size="small"
        onClick={(event) => void handleCopy(event)}
        onMouseDown={(event) => {
          event.preventDefault();
          event.stopPropagation();
        }}
        sx={{ color: copied ? 'success.main' : 'text.secondary', flexShrink: 0, p: 0.5 }}
      >
        {copied ? <CheckIcon fontSize="inherit" /> : <ContentCopyIcon fontSize="inherit" />}
      </IconButton>
    </Tooltip>
  );
}
