import { useState } from 'react';
import { Alert, Dialog, DialogTitle, DialogContent, DialogActions, Stack, Typography } from '@mui/material';
import type { ClientCatalogAlert } from '../../types/adminUsers.types';
import { adminUsersService } from '../../services/adminUsers.service';
import { BrandButton } from '../common/BrandButton';

export function ClientCatalogAlertDialog({ alert, canReview, onClose, onReviewed }: {
  alert: ClientCatalogAlert;
  canReview: boolean;
  onClose: () => void;
  onReviewed: () => void;
}) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const review = async () => {
    setSaving(true);
    setError(null);
    try {
      await adminUsersService.reviewCatalogAlert(alert.id);
      onReviewed();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Could not mark activity as reviewed.');
    } finally { setSaving(false); }
  };
  return <Dialog open onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
    <DialogTitle>Suspicious catalog activity</DialogTitle>
    <DialogContent>
      <Stack spacing={2}>
        {error && <Alert severity="error">{error}</Alert>}
        <Typography>{alert.email ?? alert.userId}</Typography>
        <Typography>
          Peak hourly activity: <strong>{alert.uniqueSites.toLocaleString()} unique sites</strong>.
          {' '}Notification threshold: {alert.threshold.toLocaleString()}.
        </Typography>
        <Typography variant="body2">Detected: {new Date(alert.detectedAtUtc).toLocaleString()}</Typography>
        <Typography variant="body2">{alert.emailSentAtUtc
          ? `Email sent: ${new Date(alert.emailSentAtUtc).toLocaleString()}`
          : 'Email pending. Delivery requires configured recipients and enabled email.'}</Typography>
        <Alert severity="info">This flag requests a review; it does not disable the account.
          {' '}Marking it as reviewed clears the flag and does not change catalog or export limits.
          {' '}New activity alerts are muted for 60 minutes after review. After that, reaching the hourly threshold can trigger another flag and email, even if activity never dropped below it.
          {' '}To disable the account, use Disable in the user action menu.</Alert>
      </Stack>
    </DialogContent>
    <DialogActions>
      <BrandButton kind="outline" onClick={onClose} disabled={saving}>Close</BrandButton>
      {canReview && <BrandButton kind="primary" onClick={() => void review()} disabled={saving}>
        {saving ? 'Saving...' : 'Mark as reviewed'}
      </BrandButton>}
    </DialogActions>
  </Dialog>;
}
