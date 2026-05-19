import { Alert, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Typography } from '@mui/material';
import type { GoogleDriveStatus } from '../../types/googleDrive.types';
import { BrandButton } from '../common/BrandButton';

export type GoogleDriveDialogState = { open: boolean; reconnect: boolean };

interface GoogleDriveConnectionDialogProps {
  dialog: GoogleDriveDialogState;
  status: GoogleDriveStatus | null;
  connecting: boolean;
  onClose: () => void;
  onConnect: () => void;
}

export function GoogleDriveConnectionDialog({
  dialog,
  status,
  connecting,
  onClose,
  onConnect,
}: Readonly<GoogleDriveConnectionDialogProps>) {
  return (
    <Dialog open={dialog.open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{dialog.reconnect ? 'Reconnect Google Drive' : 'Connect Google Drive'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {dialog.reconnect && (
            <Alert severity="warning">
              Google Drive access expired or was revoked. Reconnect Google Drive before saving exports.
            </Alert>
          )}
          <Typography variant="body2" color="text.secondary">
            Redhead Catalog needs permission to create export files in your Google Drive. Exports are
            saved to{' '}
            {status?.exportFolderName
              ? `"${status.exportFolderName}"`
              : 'a dedicated Google Drive export folder'}
            .
          </Typography>
        </Stack>
      </DialogContent>
      <DialogActions>
        <BrandButton onClick={onClose} disabled={connecting}>
          Cancel
        </BrandButton>
        <BrandButton kind="primary" onClick={onConnect} disabled={connecting}>
          {connecting ? 'Connecting...' : dialog.reconnect ? 'Reconnect Google Drive' : 'Connect Google Drive'}
        </BrandButton>
      </DialogActions>
    </Dialog>
  );
}
