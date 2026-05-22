import type { MouseEvent } from 'react';
import { Button, Tooltip } from '@mui/material';
import { toolbarPillSx } from './SitesTableViewToolbar.helpers';

interface SitesToolbarPillProps {
  label: string;
  tooltip?: string;
  onClick: (event: MouseEvent<HTMLElement>) => void;
}

export function SitesToolbarPill({ label, tooltip, onClick }: SitesToolbarPillProps) {
  const button = (
    <Button
      size="small"
      variant="outlined"
      onClick={onClick}
      aria-haspopup="dialog"
      sx={toolbarPillSx}
    >
      {label}
    </Button>
  );

  return tooltip ? <Tooltip title={tooltip}>{button}</Tooltip> : button;
}
