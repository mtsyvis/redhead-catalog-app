import React, { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Stack,
  TextField,
  Typography,
} from '@mui/material';

import { BrandButton } from '../components/common/BrandButton';
import { PageShell } from '../components/layout/PageShell';
import { useAuth } from '../contexts/AuthContext';
import { ApiClientError } from '../services/api.client';
import { authService } from '../services/auth.service';
import { googleDriveService } from '../services/googleDrive.service';
import type { CurrentUserProfile, CurrentUserProfileLimits } from '../types/auth.types';

function getFieldError(errors: Record<string, string[]> | undefined, field: string): string | undefined {
  return errors?.[field]?.[0];
}

function formatConnectedAt(value: string | null): string | null {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return date.toLocaleString();
}

function formatExportLimitValue(limits: CurrentUserProfileLimits | null | undefined): string {
  if (!limits) return 'Not configured';
  if (limits.isUnlimited || limits.exportLimitMode === 'Unlimited') return 'Unlimited';
  if (limits.exportLimitMode === 'Disabled') return 'Disabled';
  if (limits.exportLimitMode === 'Limited' && limits.exportLimitRows != null) {
    return `${limits.exportLimitRows.toLocaleString()} rows`;
  }

  return 'Not configured';
}

function getExportLimitDescription(limits: CurrentUserProfileLimits | null | undefined): string {
  if (!limits) return 'No export limit is configured for this account.';
  if (limits.isUnlimited || limits.exportLimitMode === 'Unlimited') {
    return 'You can export all matching site rows.';
  }
  if (limits.exportLimitMode === 'Disabled') {
    return 'Exports are disabled for this account.';
  }
  if (limits.exportLimitMode === 'Limited' && limits.exportLimitRows != null) {
    return `Each export can include up to ${limits.exportLimitRows.toLocaleString()} matching site rows.`;
  }

  return 'No export limit is configured for this account.';
}

export const Profile: React.FC = () => {
  const { refreshUser } = useAuth();
  const [profile, setProfile] = useState<CurrentUserProfile | null>(null);
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [connecting, setConnecting] = useState(false);
  const [disconnecting, setDisconnecting] = useState(false);
  const [disconnectConfirmOpen, setDisconnectConfirmOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});

  const applyProfile = useCallback((nextProfile: CurrentUserProfile) => {
    setProfile(nextProfile);
    setFirstName(nextProfile.firstName ?? '');
    setLastName(nextProfile.lastName ?? '');
  }, []);

  const loadProfile = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      applyProfile(await authService.getCurrentProfile());
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : 'Failed to load profile.');
    } finally {
      setLoading(false);
    }
  }, [applyProfile]);

  useEffect(() => {
    void loadProfile();
  }, [loadProfile]);

  const handleSaveProfile = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (saving) return;

    const trimmedFirstName = firstName.trim();
    const trimmedLastName = lastName.trim();
    const nextFieldErrors: Record<string, string[]> = {};
    if (!trimmedFirstName) nextFieldErrors.firstName = ['First name is required.'];
    if (!trimmedLastName) nextFieldErrors.lastName = ['Last name is required.'];

    setSuccess(null);
    setError(null);
    setFieldErrors(nextFieldErrors);
    if (Object.keys(nextFieldErrors).length > 0) return;

    setSaving(true);
    try {
      applyProfile(await authService.updateCurrentProfile({
        firstName: trimmedFirstName,
        lastName: trimmedLastName,
      }));
      await refreshUser();
      setSuccess('Profile updated.');
    } catch (saveError) {
      if (saveError instanceof ApiClientError) {
        setFieldErrors(saveError.fieldErrors ?? {});
        if (!saveError.fieldErrors || Object.keys(saveError.fieldErrors).length === 0) {
          setError(saveError.message);
        }
      } else {
        setError('Profile could not be saved. Please try again.');
      }
    } finally {
      setSaving(false);
    }
  };

  const handleConnectGoogleDrive = async () => {
    setConnecting(true);
    setError(null);
    try {
      const response = await googleDriveService.startConnect();
      window.location.href = response.authorizationUrl;
    } catch {
      setConnecting(false);
      setError('Could not start Google Drive connection. Try again later.');
    }
  };

  const handleDisconnectGoogleDrive = async () => {
    setDisconnecting(true);
    setError(null);
    try {
      await googleDriveService.disconnect();
      setDisconnectConfirmOpen(false);
      applyProfile(await authService.getCurrentProfile());
      setSuccess('Google Drive disconnected.');
    } catch {
      setError('Could not disconnect Google Drive. Try again later.');
    } finally {
      setDisconnecting(false);
    }
  };

  const googleDrive = profile?.googleDrive;
  const limits = profile?.limits;
  const connectedAt = formatConnectedAt(googleDrive?.connectedAtUtc ?? null);
  const trimmedFirstName = firstName.trim();
  const trimmedLastName = lastName.trim();
  const profileChanged =
    trimmedFirstName !== (profile?.firstName ?? '') ||
    trimmedLastName !== (profile?.lastName ?? '');
  const canSaveProfile = profileChanged && trimmedFirstName.length > 0 && trimmedLastName.length > 0;

  return (
    <PageShell title="Profile" maxWidth="md">
      <Stack spacing={3}>
        {error && (
          <Alert severity="error" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}
        {success && (
          <Alert severity="success" onClose={() => setSuccess(null)}>
            {success}
          </Alert>
        )}

        {loading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
            <CircularProgress />
          </Box>
        ) : (
          <>
            <Card>
              <CardContent sx={{ p: 3 }}>
                <Typography variant="h6" sx={{ mb: 2 }}>
                  Account information
                </Typography>
                <Box component="form" onSubmit={handleSaveProfile}>
                  <Stack spacing={2}>
                    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2 }}>
                      <TextField
                        label="First name"
                        required
                        value={firstName}
                        onChange={(event) => {
                          setFirstName(event.target.value);
                          setFieldErrors((prev) => ({ ...prev, firstName: [] }));
                        }}
                        disabled={saving}
                        error={Boolean(getFieldError(fieldErrors, 'firstName'))}
                        helperText={getFieldError(fieldErrors, 'firstName')}
                        autoComplete="given-name"
                      />
                      <TextField
                        label="Last name"
                        required
                        value={lastName}
                        onChange={(event) => {
                          setLastName(event.target.value);
                          setFieldErrors((prev) => ({ ...prev, lastName: [] }));
                        }}
                        disabled={saving}
                        error={Boolean(getFieldError(fieldErrors, 'lastName'))}
                        helperText={getFieldError(fieldErrors, 'lastName')}
                        autoComplete="family-name"
                      />
                      <TextField label="Email" value={profile?.email ?? ''} disabled />
                      <TextField label="Role" value={profile?.role ?? ''} disabled />
                    </Box>

                    <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
                      <BrandButton kind="primary" type="submit" disabled={saving || !canSaveProfile}>
                        {saving ? <CircularProgress size={22} color="inherit" /> : 'Save changes'}
                      </BrandButton>
                    </Box>
                  </Stack>
                </Box>
              </CardContent>
            </Card>

            <Card>
              <CardContent sx={{ p: 3 }}>
                <Stack spacing={1.25}>
                  <Typography variant="h6">Export limits</Typography>
                  <Typography variant="body2">
                    Export row limit: <strong>{formatExportLimitValue(limits)}</strong>
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    {getExportLimitDescription(limits)}
                  </Typography>
                </Stack>
              </CardContent>
            </Card>

            <Card>
              <CardContent sx={{ p: 3 }}>
                <Box
                  sx={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'flex-start',
                    gap: 2,
                    flexWrap: 'wrap',
                    mb: 2,
                  }}
                >
                  <Box>
                    <Typography variant="h6">Google Drive</Typography>
                    <Typography variant="body2" color="text.secondary">
                      Save catalog exports to your connected Google Drive account.
                    </Typography>
                  </Box>
                  <Chip
                    label={googleDrive?.connected ? 'Connected' : 'Not connected'}
                    color={googleDrive?.connected ? 'success' : 'default'}
                    size="small"
                  />
                </Box>

                <Divider sx={{ mb: 2 }} />

                <Stack spacing={1.25}>
                  {googleDrive?.connected ? (
                    <>
                      <Typography variant="body2">
                        Connected account: <strong>{googleDrive.googleEmail ?? 'Google Drive'}</strong>
                      </Typography>
                      {connectedAt && (
                        <Typography variant="body2" color="text.secondary">
                          Connected {connectedAt}
                        </Typography>
                      )}
                      {googleDrive.needsReconnect && (
                        <Alert severity="warning">
                          Google Drive access needs to be reconnected before saving exports.
                        </Alert>
                      )}
                      <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1, flexWrap: 'wrap' }}>
                        {googleDrive.needsReconnect && (
                          <BrandButton onClick={handleConnectGoogleDrive} disabled={connecting || disconnecting}>
                            {connecting ? 'Connecting...' : 'Connect Google Drive'}
                          </BrandButton>
                        )}
                        <BrandButton
                          kind="outline"
                          onClick={() => setDisconnectConfirmOpen(true)}
                          disabled={connecting || disconnecting}
                        >
                          Disconnect
                        </BrandButton>
                      </Box>
                    </>
                  ) : (
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap' }}>
                      <Typography variant="body2" color="text.secondary">
                        Google Drive is not connected.
                      </Typography>
                      <BrandButton kind="primary" onClick={handleConnectGoogleDrive} disabled={connecting}>
                        {connecting ? 'Connecting...' : 'Connect Google Drive'}
                      </BrandButton>
                    </Box>
                  )}
                </Stack>
              </CardContent>
            </Card>
          </>
        )}
      </Stack>

      <Dialog
        open={disconnectConfirmOpen}
        onClose={() => !disconnecting && setDisconnectConfirmOpen(false)}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle>Disconnect Google Drive</DialogTitle>
        <DialogContent>
          <Typography variant="body2" color="text.secondary">
            Disconnecting removes this account's Google Drive export connection. Existing exported files are not
            deleted, but future saves to Google Drive will require reconnecting.
          </Typography>
        </DialogContent>
        <DialogActions>
          <BrandButton kind="outline" onClick={() => setDisconnectConfirmOpen(false)} disabled={disconnecting}>
            Cancel
          </BrandButton>
          <BrandButton kind="primary" onClick={handleDisconnectGoogleDrive} disabled={disconnecting}>
            {disconnecting ? <CircularProgress size={22} color="inherit" /> : 'Disconnect'}
          </BrandButton>
        </DialogActions>
      </Dialog>
    </PageShell>
  );
};
