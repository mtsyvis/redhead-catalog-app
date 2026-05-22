import { Button, Popover, Stack, Typography } from '@mui/material';
import { BrandButton } from '../../common/BrandButton';
import { pluralize, popoverPaperSx } from './SitesTableViewToolbar.helpers';
import type { SitesColumnMetadata } from '../table-views/sitesTableColumns';

interface SitesHiddenFiltersPopoverProps {
  anchorEl: HTMLElement | null;
  hiddenFilteredColumns: SitesColumnMetadata[];
  hiddenFiltersCount: number;
  hiddenColumnsLabel: string;
  onClose: () => void;
  onClear: () => void;
  onShow: () => void;
}

export function SitesHiddenFiltersPopover({
  anchorEl,
  hiddenFilteredColumns,
  hiddenFiltersCount,
  hiddenColumnsLabel,
  onClose,
  onClear,
  onShow,
}: SitesHiddenFiltersPopoverProps) {
  return (
    <Popover
      open={Boolean(anchorEl)}
      anchorEl={anchorEl}
      onClose={onClose}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
      transformOrigin={{ vertical: 'top', horizontal: 'left' }}
      PaperProps={{ sx: { ...popoverPaperSx, width: 320 } }}
    >
      <Typography variant="subtitle2" sx={{ mb: 1 }}>
        {pluralize('Filter', hiddenFiltersCount)} on hidden {hiddenColumnsLabel}
      </Typography>
      {hiddenFilteredColumns.length > 0 ? (
        <Stack component="ul" spacing={0.5} sx={{ m: 0, pl: 2.25 }}>
          {hiddenFilteredColumns.map((column) => (
            <Typography key={column.id} component="li" variant="body2">
              {column.label}
            </Typography>
          ))}
        </Stack>
      ) : (
        <Typography variant="body2" color="text.secondary">
          Some active filters use columns that are not visible in the current view.
        </Typography>
      )}
      <Stack direction="row" spacing={1} sx={{ mt: 2, justifyContent: 'flex-end' }}>
        <Button size="small" variant="text" onClick={onClear}>
          Clear {pluralize('filter', hiddenFiltersCount)}
        </Button>
        <BrandButton size="small" kind="primary" onClick={onShow}>
          Show {hiddenColumnsLabel}
        </BrandButton>
      </Stack>
    </Popover>
  );
}
