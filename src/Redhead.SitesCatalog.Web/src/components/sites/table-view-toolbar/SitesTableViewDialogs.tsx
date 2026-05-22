import { Dialog, DialogActions, DialogContent, DialogTitle, TextField, Typography } from '@mui/material';
import { BrandButton } from '../../common/BrandButton';
import type { NameDialogState } from './SitesTableViewToolbar.types';

interface SitesViewNameDialogProps {
  actionLoading: boolean;
  error: string | null;
  nameDialog: NameDialogState | null;
  onClose: () => void;
  onChangeName: (name: string) => void;
  onSubmit: () => void;
}

interface SitesDeleteViewDialogProps {
  actionLoading: boolean;
  open: boolean;
  viewName: string;
  onClose: () => void;
  onDelete: () => void;
}

export function SitesViewNameDialog({
  actionLoading,
  error,
  nameDialog,
  onClose,
  onChangeName,
  onSubmit,
}: SitesViewNameDialogProps) {
  const title =
    nameDialog?.mode === 'rename'
      ? 'Rename view'
      : nameDialog?.mode === 'duplicate'
        ? 'Duplicate view'
        : 'Save as custom view';
  const action =
    nameDialog?.mode === 'rename'
      ? 'Rename'
      : nameDialog?.mode === 'duplicate'
        ? 'Duplicate'
        : 'Save as custom view';

  return (
    <Dialog open={Boolean(nameDialog)} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <TextField
          autoFocus
          fullWidth
          margin="dense"
          label="View name"
          value={nameDialog?.name ?? ''}
          error={Boolean(error)}
          helperText={error}
          onChange={(event) => onChangeName(event.target.value)}
          slotProps={{ htmlInput: { maxLength: 80 } }}
        />
      </DialogContent>
      <DialogActions>
        <BrandButton size="small" onClick={onClose} disabled={actionLoading}>
          Cancel
        </BrandButton>
        <BrandButton size="small" kind="primary" onClick={onSubmit} disabled={actionLoading}>
          {action}
        </BrandButton>
      </DialogActions>
    </Dialog>
  );
}

export function SitesDeleteViewDialog({
  actionLoading,
  open,
  viewName,
  onClose,
  onDelete,
}: SitesDeleteViewDialogProps) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Delete “{viewName}”?</DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary">
          This custom view will be permanently deleted.
        </Typography>
      </DialogContent>
      <DialogActions>
        <BrandButton size="small" onClick={onClose} disabled={actionLoading}>
          Cancel
        </BrandButton>
        <BrandButton size="small" kind="primary" onClick={onDelete} disabled={actionLoading}>
          Delete
        </BrandButton>
      </DialogActions>
    </Dialog>
  );
}
