import { useState } from 'react';
import { Alert, Dialog, DialogTitle, DialogContent, DialogActions, Stack, Typography } from '@mui/material';
import type { ClientCatalogAutoBan } from '../../types/adminUsers.types';
import { adminUsersService } from '../../services/adminUsers.service';
import { BrandButton } from '../common/BrandButton';

export function ClientCatalogAutoBanDialog({ autoBan, canReview, onClose, onReviewed, onReactivate }: {
  autoBan: ClientCatalogAutoBan;
  canReview: boolean;
  onClose: () => void;
  onReviewed: () => void;
  onReactivate: (autoBan: ClientCatalogAutoBan) => void;
}) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const review = async (reactivate: boolean) => {
    setSaving(true);
    setError(null);
    try {
      await adminUsersService.reviewCatalogAutoBan(autoBan.id);
      if (reactivate) onReactivate(autoBan);
      else onReviewed();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Could not mark the automatic ban as reviewed.');
    } finally {
      setSaving(false);
    }
  };

  return <Dialog open onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
    <DialogTitle>Account disabled automatically</DialogTitle>
    <DialogContent>
      <Stack spacing={2}>
        {error && <Alert severity="error">{error}</Alert>}
        <Typography>{autoBan.email ?? autoBan.userId}</Typography>
        <Typography>
          Rolling 24-hour activity reached <strong>{autoBan.uniqueSites.toLocaleString()} unique sites</strong>.
          {' '}Automatic-ban threshold: {autoBan.threshold.toLocaleString()}.
        </Typography>
        <Typography variant="body2">Detected: {new Date(autoBan.detectedAtUtc).toLocaleString()}</Typography>
        <Typography variant="body2">{autoBan.emailSentAtUtc
          ? `Email sent: ${new Date(autoBan.emailSentAtUtc).toLocaleString()}`
          : 'Email pending. Delivery requires configured recipients and enabled email.'}</Typography>
        <Alert severity="warning">
          The account is disabled. Marking it as reviewed clears this notification but keeps the account disabled.
          {' '}Reactivate starts the standard secure reactivation flow and also clears this notification.
        </Alert>
      </Stack>
    </DialogContent>
    <DialogActions>
      <BrandButton kind="outline" onClick={onClose} disabled={saving}>Close</BrandButton>
      {canReview && <BrandButton kind="outline" onClick={() => void review(false)} disabled={saving}>
        Mark as reviewed
      </BrandButton>}
      {canReview && <BrandButton kind="primary" onClick={() => void review(true)} disabled={saving}>
        Review and reactivate
      </BrandButton>}
    </DialogActions>
  </Dialog>;
}
