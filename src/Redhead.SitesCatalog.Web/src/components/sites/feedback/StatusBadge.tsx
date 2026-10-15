import { memo } from 'react';
import { StatusBadge as SharedStatusBadge } from '../../common/StatusBadge';

interface StatusBadgeProps {
  isAvailable: boolean;
}

export const StatusBadge = memo(function StatusBadge({ isAvailable }: StatusBadgeProps) {
  return (
    <SharedStatusBadge
      label={isAvailable ? 'Available' : 'Unavailable'}
      tone={isAvailable ? 'success' : 'error'}
      minWidth={92}
    />
  );
});
