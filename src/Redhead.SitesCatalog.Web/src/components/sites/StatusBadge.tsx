import { memo } from 'react';
import { Box } from '@mui/material';
import { alpha } from '@mui/material/styles';
import type { Theme } from '@mui/material/styles';

const getStatusBadgeSx = (theme: Theme, isAvailable: boolean) => ({
  display: 'inline-flex',
  alignItems: 'center',
  justifyContent: 'center',
  minWidth: 92,
  px: 1.25,
  py: 0.5,
  borderRadius: 999,
  fontSize: 12,
  fontWeight: 600,
  lineHeight: 1.2,
  whiteSpace: 'nowrap',
  color: isAvailable ? theme.palette.success.dark : theme.palette.warning.dark,
  backgroundColor: isAvailable
    ? alpha(theme.palette.success.main, 0.14)
    : alpha(theme.palette.warning.main, 0.18),
  border: `1px solid ${
    isAvailable
      ? alpha(theme.palette.success.main, 0.28)
      : alpha(theme.palette.warning.main, 0.32)
  }`,
});

interface StatusBadgeProps {
  isAvailable: boolean;
}

export const StatusBadge = memo(function StatusBadge({ isAvailable }: StatusBadgeProps) {
  return (
    <Box component="span" sx={(theme) => getStatusBadgeSx(theme, isAvailable)}>
      {isAvailable ? 'Available' : 'Unavailable'}
    </Box>
  );
});
