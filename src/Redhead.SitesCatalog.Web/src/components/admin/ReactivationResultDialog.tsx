import { Alert, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Typography } from '@mui/material';

import type { InvitationEmailDeliveryStatus } from '../../types/adminUsers.types';
import { BrandButton } from '../common/BrandButton';
import { OneTimeValueDialog } from './OneTimeValueDialog';

interface ReactivationResultDialogProps {
  title: 'Reactivation created' | 'Reactivation reissued';
  email: string;
  fallbackUrl: string | null;
  emailDeliveryStatus: InvitationEmailDeliveryStatus;
  onClose: () => void;
}

export const ReactivationResultDialog: React.FC<ReactivationResultDialogProps> = ({
  title,
  email,
  fallbackUrl,
  emailDeliveryStatus,
  onClose,
}) => {
  if (fallbackUrl) {
    const notice = emailDeliveryStatus === 'Failed'
      ? 'The reactivation was created, but the email could not be sent.'
      : 'Email delivery is disabled, so the reactivation email was not sent.';

    return (
      <OneTimeValueDialog
        title={title}
        email={email}
        notice={<Alert severity="warning">{notice} Copy and share the link securely.</Alert>}
        valueLabel="Reactivation link"
        value={fallbackUrl}
        helperText="This single-use link is shown only once and expires in 48 hours."
        copyLabel="Copy link"
        copyErrorMessage="Could not copy. Please copy the link manually."
        onClose={onClose}
      />
    );
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <Stack spacing={2}>
          <Typography variant="body2" color="text.secondary">
            For {email}
          </Typography>
          <Alert severity="success">
            Reactivation email sent. The user can choose a new password from the email link.
          </Alert>
        </Stack>
      </DialogContent>
      <DialogActions>
        <BrandButton kind="primary" onClick={onClose}>Done</BrandButton>
      </DialogActions>
    </Dialog>
  );
};
