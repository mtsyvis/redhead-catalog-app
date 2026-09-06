import type { InvitationEmailDeliveryStatus } from '../../types/adminUsers.types';
import { InvitationDeliveryNotice } from './InvitationDeliveryNotice';
import { OneTimeValueDialog } from './OneTimeValueDialog';

interface InvitationResultDialogProps {
  title: 'Invitation created' | 'Invitation reissued';
  email: string;
  activationUrl: string;
  emailDeliveryStatus: InvitationEmailDeliveryStatus;
  invitationAction: 'created' | 'reissued';
  onClose: () => void;
}

export const InvitationResultDialog: React.FC<InvitationResultDialogProps> = ({
  title,
  email,
  activationUrl,
  emailDeliveryStatus,
  invitationAction,
  onClose,
}) => {
  return (
    <OneTimeValueDialog
      title={title}
      email={email}
      notice={
        <InvitationDeliveryNotice
          status={emailDeliveryStatus}
          invitationAction={invitationAction}
        />
      }
      valueLabel="Activation link"
      value={activationUrl}
      helperText="This link is shown only once and expires in 48 hours."
      copyLabel="Copy link"
      copyErrorMessage="Could not copy. Please copy the link manually."
      onClose={onClose}
    />
  );
};
