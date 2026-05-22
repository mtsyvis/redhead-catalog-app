import { Alert, Box, Button, Snackbar, Typography } from '@mui/material';
import type { AlertColor } from '@mui/material';
import DownloadIcon from '@mui/icons-material/Download';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';

export type SitesSnackbarState = {
  open: boolean;
  message: string;
  detail?: string;
  severity: AlertColor;
  actionLabel?: 'Open file' | 'Download Excel';
  onAction?: () => void;
};

interface SitesSnackbarProps {
  snackbar: SitesSnackbarState;
  onClose: () => void;
}

export function SitesSnackbar({ snackbar, onClose }: Readonly<SitesSnackbarProps>) {
  return (
    <Snackbar
      open={snackbar.open}
      autoHideDuration={6000}
      onClose={onClose}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
    >
      <Alert
        onClose={onClose}
        severity={snackbar.severity}
        sx={{ width: '100%' }}
        action={
          snackbar.actionLabel && snackbar.onAction ? (
            <Button
              color="inherit"
              size="small"
              startIcon={
                snackbar.actionLabel === 'Open file' ? (
                  <OpenInNewIcon fontSize="small" />
                ) : (
                  <DownloadIcon fontSize="small" />
                )
              }
              onClick={snackbar.onAction}
            >
              {snackbar.actionLabel}
            </Button>
          ) : undefined
        }
      >
        <Box>
          <Typography variant="body2">{snackbar.message}</Typography>
          {snackbar.detail && (
            <Typography variant="caption" sx={{ display: 'block', mt: 0.5 }}>
              {snackbar.detail}
            </Typography>
          )}
        </Box>
      </Alert>
    </Snackbar>
  );
}
