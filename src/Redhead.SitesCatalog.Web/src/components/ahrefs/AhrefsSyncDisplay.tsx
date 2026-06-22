import { Chip } from '@mui/material';
import {
  formatItemStatus,
  formatRunStatus,
} from '../../utils/ahrefsSyncDisplay';

export function RunStatusChip({ status }: { status: number }) {
  const color =
    status === 2
      ? 'success'
      : status === 1
        ? 'info'
        : status === 3 || status === 5 || status === 6 || status === 7
          ? 'warning'
          : 'error';
  return (
    <Chip
      size="small"
      label={formatRunStatus(status)}
      color={color}
      variant="outlined"
    />
  );
}

export function ItemStatusChip({ status }: { status: number }) {
  const color = status === 1 ? 'success' : status === 2 ? 'error' : 'warning';
  return (
    <Chip
      size="small"
      label={formatItemStatus(status)}
      color={color}
      variant="outlined"
    />
  );
}
