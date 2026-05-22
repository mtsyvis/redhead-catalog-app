import { Box, Button, Popover, Stack, Typography } from '@mui/material';
import { BrandButton } from '../../common/BrandButton';
import { popoverPaperSx } from './SitesTableViewToolbar.helpers';
import type { ViewColumnChanges } from './SitesTableViewToolbar.types';

interface SitesViewChangesPopoverProps {
  anchorEl: HTMLElement | null;
  activeViewName: string;
  actionLoading: boolean;
  isCustomView: boolean;
  changes: ViewColumnChanges;
  hasOnlyDisplaySettingChanges: boolean;
  onClose: () => void;
  onReset: () => void;
  onSave: () => void;
}

export function SitesViewChangesPopover({
  anchorEl,
  activeViewName,
  actionLoading,
  isCustomView,
  changes,
  hasOnlyDisplaySettingChanges,
  onClose,
  onReset,
  onSave,
}: SitesViewChangesPopoverProps) {
  return (
    <Popover
      open={Boolean(anchorEl)}
      anchorEl={anchorEl}
      onClose={onClose}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
      transformOrigin={{ vertical: 'top', horizontal: 'left' }}
      PaperProps={{ sx: popoverPaperSx }}
    >
      <Typography variant="subtitle2" sx={{ mb: 1 }}>
        Unsaved view changes
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
        Changes to “{activeViewName}” are not saved yet.
      </Typography>

      <ColumnChangeList title="Added columns" labels={changes.added} />
      <ColumnChangeList title="Hidden columns" labels={changes.hidden} />

      {changes.reordered && (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          Column order was changed.
        </Typography>
      )}

      {hasOnlyDisplaySettingChanges && (
        <Typography variant="body2" color="text.secondary">
          Display settings were changed.
        </Typography>
      )}

      <Stack direction="row" spacing={1} sx={{ mt: 2, justifyContent: 'flex-end' }}>
        <Button size="small" variant="text" onClick={onReset}>
          Reset changes
        </Button>
        <BrandButton size="small" kind="primary" onClick={onSave} disabled={actionLoading}>
          {isCustomView ? 'Save changes' : 'Save as custom view'}
        </BrandButton>
      </Stack>
    </Popover>
  );
}

function ColumnChangeList({ title, labels }: { title: string; labels: string[] }) {
  if (labels.length === 0) return null;

  return (
    <Box sx={{ mb: 1.5 }}>
      <Typography variant="caption" color="text.secondary">
        {title}
      </Typography>
      <Stack component="ul" spacing={0.5} sx={{ m: 0, mt: 0.5, pl: 2.25 }}>
        {labels.map((label) => (
          <Typography key={label} component="li" variant="body2">
            {label}
          </Typography>
        ))}
      </Stack>
    </Box>
  );
}
