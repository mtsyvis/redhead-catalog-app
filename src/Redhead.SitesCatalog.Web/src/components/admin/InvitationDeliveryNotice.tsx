import { Alert } from '@mui/material';

import type { InvitationEmailDeliveryStatus } from '../../types/adminUsers.types';

interface InvitationDeliveryNoticeProps {
  status: InvitationEmailDeliveryStatus;
  invitationAction: 'created' | 'reissued';
}

export const InvitationDeliveryNotice: React.FC<InvitationDeliveryNoticeProps> = ({
  status,
  invitationAction,
}) => {
  if (status === 'Sent') {
    return <Alert severity="success">Invitation email sent.</Alert>;
  }

  if (status === 'Failed') {
    return (
      <Alert severity="warning">
        Invitation {invitationAction}, but the email could not be sent.
        <br />
        The account is still pending activation.{' '}
        Copy the activation link or reissue the invitation after mail delivery is fixed.
      </Alert>
    );
  }

  return (
    <Alert severity="info">
      Email wasn’t sent because email delivery is disabled. The invitation is active—copy the link and share it
      securely.
    </Alert>
  );
};
