import { Alert } from '@mui/material';

export interface ImportErrorAlertProps {
  error: string | null;
  onClose?: () => void;
}

export function ImportErrorAlert({ error, onClose }: ImportErrorAlertProps) {
  if (!error) {
    return null;
  }

  return (
    <Alert
      severity="error"
      sx={{ mb: 2 }}
      onClose={onClose}
    >
      {error}
    </Alert>
  );
}

