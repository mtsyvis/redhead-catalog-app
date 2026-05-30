import { useCallback, useEffect, useRef, useState } from 'react';
import { Box, Tooltip } from '@mui/material';

interface TruncatedTextCellProps {
  value: string;
}

function canShowTooltip(value: string): boolean {
  const trimmed = value.trim();
  return trimmed !== '' && trimmed !== '—';
}

export function TruncatedTextCell({ value }: Readonly<TruncatedTextCellProps>) {
  const textRef = useRef<HTMLSpanElement | null>(null);
  const [isOverflowing, setIsOverflowing] = useState(false);
  const tooltipEnabled = canShowTooltip(value) && isOverflowing;

  const updateOverflowState = useCallback(() => {
    const element = textRef.current;
    setIsOverflowing(Boolean(element && element.scrollWidth > element.clientWidth));
  }, []);

  useEffect(() => {
    const element = textRef.current;
    if (!element || typeof ResizeObserver === 'undefined') {
      return undefined;
    }

    const observer = new ResizeObserver(() => updateOverflowState());
    observer.observe(element);

    return () => observer.disconnect();
  }, [updateOverflowState, value]);

  return (
    <Tooltip title={tooltipEnabled ? value : ''} enterDelay={500} disableInteractive>
      <Box
        component="span"
        ref={textRef}
        onMouseEnter={updateOverflowState}
        sx={{
          display: 'block',
          width: '100%',
          minWidth: 0,
          maxWidth: '100%',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
        }}
      >
        {value}
      </Box>
    </Tooltip>
  );
}
