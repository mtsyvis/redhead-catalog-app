import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Navigate, useNavigate, useParams } from 'react-router-dom';
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
  Snackbar,
  Stack,
  Typography,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';

import { BrandButton } from '../components/common/BrandButton';
import { PageShell } from '../components/layout/PageShell';
import { ApiClientError } from '../services/api.client';
import { adminUsersService } from '../services/adminUsers.service';
import type { AdminUserDetails as AdminUserDetailsType } from '../types/adminUsers.types';
import type { ExportLimitMode } from '../utils/exportLimit';
import { formatExportLimit } from '../utils/exportLimit';
import { formatUsageLimitPair } from '../utils/exportUsageLimits';
import { useUserRoles } from '../hooks/useUserRoles';

const emptyValue = 'Not completed yet';

function cleanText(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

function formatDateTime(value: string | null | undefined): string | null {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return date.toLocaleString();
}

function formatMaybeExportLimit(mode: ExportLimitMode | null | undefined, rows: number | null | undefined): string | null {
  if (!mode) return null;
  return formatExportLimit(mode, rows ?? null);
}

const CLIENT_USAGE_LIMIT_ROWS: Array<{
  label: string;
  helperText: string;
  getEffective: (user: AdminUserDetailsType) => number | null;
  getOverride: (user: AdminUserDetailsType) => number | null;
}> = [
  {
    label: 'Daily unique exported domains',
    helperText: '24h window',
    getEffective: (user) => user.effectiveDailyUniqueExportedDomainsLimit,
    getOverride: (user) => user.dailyUniqueExportedDomainsLimitOverride,
  },
  {
    label: 'Weekly unique exported domains',
    helperText: '7d window',
    getEffective: (user) => user.effectiveWeeklyUniqueExportedDomainsLimit,
    getOverride: (user) => user.weeklyUniqueExportedDomainsLimitOverride,
  },
  {
    label: 'Daily export operations',
    helperText: '24h window',
    getEffective: (user) => user.effectiveDailyExportOperationsLimit,
    getOverride: (user) => user.dailyExportOperationsLimitOverride,
  },
  {
    label: 'Weekly export operations',
    helperText: '7d window',
    getEffective: (user) => user.effectiveWeeklyExportOperationsLimit,
    getOverride: (user) => user.weeklyExportOperationsLimitOverride,
  },
];

const CURRENT_CLIENT_USAGE_ROWS: Array<{
  label: string;
  getUsed: (user: AdminUserDetailsType) => number | null | undefined;
  getLimit: (user: AdminUserDetailsType) => number | null | undefined;
}> = [
  {
    label: 'Daily exported domains',
    getUsed: (user) => user.clientExportUsage?.dailyUniqueExportedDomainsUsed,
    getLimit: (user) => user.clientExportUsage?.dailyUniqueExportedDomainsLimit,
  },
  {
    label: 'Weekly exported domains',
    getUsed: (user) => user.clientExportUsage?.weeklyUniqueExportedDomainsUsed,
    getLimit: (user) => user.clientExportUsage?.weeklyUniqueExportedDomainsLimit,
  },
  {
    label: 'Daily exports',
    getUsed: (user) => user.clientExportUsage?.dailyExportOperationsUsed,
    getLimit: (user) => user.clientExportUsage?.dailyExportOperationsLimit,
  },
  {
    label: 'Weekly exports',
    getUsed: (user) => user.clientExportUsage?.weeklyExportOperationsUsed,
    getLimit: (user) => user.clientExportUsage?.weeklyExportOperationsLimit,
  },
];

function formatClientUsageLimit(value: number | null | undefined): string {
  return value == null ? 'Not available' : value.toLocaleString();
}

function getPerExportLimitSettingChip(user: AdminUserDetailsType): string {
  if (user.isExportLimitEditable === false) {
    return 'Fixed setting';
  }

  return user.exportLimitOverrideMode == null
    ? 'Uses role default'
    : 'Custom setting';
}

function getPerExportLimitSettingValue(
  user: AdminUserDetailsType,
  effectiveLimit: string | null
): string {
  if (user.exportLimitOverrideMode != null) {
    return formatExportLimit(user.exportLimitOverrideMode, user.exportLimitRowsOverride ?? null);
  }

  return effectiveLimit ?? 'Not available';
}

function getClientUsageLimitsChip(user: AdminUserDetailsType): string {
  return CLIENT_USAGE_LIMIT_ROWS.some((row) => row.getOverride(user) != null)
    ? 'Custom settings'
    : 'Uses role defaults';
}

function getErrorMessage(error: unknown): string {
  if (error instanceof ApiClientError) {
    if (error.statusCode === 403) return 'You do not have access to this user.';
    if (error.statusCode === 404) return 'User not found.';
    return error.message;
  }

  return 'Failed to load user details.';
}

interface DetailRowProps {
  label: string;
  value?: React.ReactNode;
}

const DetailRow: React.FC<DetailRowProps> = ({ label, value }) => (
  <Box>
    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.25 }}>
      {label}
    </Typography>
    {typeof value === 'string' || value === null || value === undefined ? (
      <Typography variant="body2">{value || emptyValue}</Typography>
    ) : (
      value
    )}
  </Box>
);

export const AdminUserDetails: React.FC = () => {
  const { canReadUsers, canManageUsers, isSuperAdmin } = useUserRoles();
  const { userId } = useParams<{ userId: string }>();
  const navigate = useNavigate();
  const [user, setUser] = useState<AdminUserDetailsType | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reissuing, setReissuing] = useState(false);
  const [invitationLink, setInvitationLink] = useState<string | null>(null);
  const [copyNotification, setCopyNotification] = useState<{
    open: boolean;
    message: string;
    severity: 'success' | 'error';
  }>({
    open: false,
    message: '',
    severity: 'success',
  });

  const loadUser = useCallback(async () => {
    if (!userId) {
      setError('User not found.');
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      setUser(await adminUsersService.getDetails(userId));
    } catch (loadError) {
      setUser(null);
      setError(getErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, [userId]);

  useEffect(() => {
    void loadUser();
  }, [loadUser]);

  const handleReissueInvitation = async () => {
    if (!user) return;

    setReissuing(true);
    setError(null);
    try {
      const response = await adminUsersService.reissueInvitation(user.id);
      setInvitationLink(`${window.location.origin}${response.activationPath}`);
      await loadUser();
    } catch (reissueError) {
      setError(reissueError instanceof ApiClientError ? reissueError.message : 'Failed to reissue invitation.');
    } finally {
      setReissuing(false);
    }
  };

  const handleCopyInvitation = async () => {
    if (!invitationLink) return;

    try {
      await navigator.clipboard.writeText(invitationLink);
      setCopyNotification({ open: true, message: 'Copied', severity: 'success' });
    } catch {
      setCopyNotification({
        open: true,
        message: 'Could not copy. Please copy the value manually.',
        severity: 'error',
      });
    }
  };

  const displayName = useMemo(() => {
    if (!user) return '';
    return cleanText(user.displayName) ?? cleanText(user.email) ?? 'User';
  }, [user]);

  const profileStatus = user?.mustCompleteProfile ? 'Incomplete' : 'Complete';
  const effectiveLimit = user
    ? formatMaybeExportLimit(user.effectiveExportLimitMode, user.effectiveExportLimitRows)
    : null;
  const clientUsageLimitRows = user?.role === 'Client' ? CLIENT_USAGE_LIMIT_ROWS : [];
  const showCurrentClientUsage = user?.role === 'Client'
    && CURRENT_CLIENT_USAGE_ROWS.some((row) => row.getLimit(user) != null);
  const googleDrive = user?.googleDrive;
  const googleDriveConnected = user ? user.googleDriveConnected : null;
  const connectedAt = formatDateTime(googleDrive?.connectedAtUtc);

  if (!canReadUsers) {
    return <Navigate to="/sites" replace />;
  }

  return (
    <PageShell title="User details" maxWidth="lg">
      <Stack spacing={3}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap' }}>
          <BrandButton kind="outline" startIcon={<ArrowBackIcon />} onClick={() => navigate('/admin/users')}>
            Back to users
          </BrandButton>
          {canManageUsers && user && (
            user.accountStatus === 'PendingActivation' || user.accountStatus === 'InvitationExpired'
          ) && (
            <BrandButton kind="primary" onClick={() => void handleReissueInvitation()} disabled={reissuing}>
              {reissuing ? <CircularProgress size={20} color="inherit" /> : 'Reissue invitation'}
            </BrandButton>
          )}
        </Box>

        {loading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
            <CircularProgress />
          </Box>
        ) : error ? (
          <Alert
            severity={error === 'User not found.' ? 'warning' : 'error'}
            action={
              <BrandButton kind="outline" size="small" onClick={() => navigate('/admin/users')}>
                Back
              </BrandButton>
            }
          >
            {error}
          </Alert>
        ) : user ? (
          <>
            <Card>
              <CardContent sx={{ p: 3 }}>
                <Box
                  sx={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'flex-start',
                    gap: 2,
                    flexWrap: 'wrap',
                  }}
                >
                  <Box sx={{ minWidth: 0 }}>
                    <Typography variant="h5" component="h2" sx={{ mb: 0.5, wordBreak: 'break-word' }}>
                      {displayName}
                    </Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ wordBreak: 'break-word' }}>
                      {user.email}
                    </Typography>
                  </Box>
                  <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                    <Chip label={user.role || 'Role unavailable'} color="primary" size="small" />
                    {user.accountStatus === 'PendingActivation' && (
                      <Chip label="Pending activation" color="info" variant="outlined" size="small" />
                    )}
                    {user.accountStatus === 'InvitationExpired' && (
                      <Chip label="Invitation expired" color="warning" variant="outlined" size="small" />
                    )}
                    {user.mustCompleteProfile && user.accountStatus === 'Active' && (
                      <Chip label="Profile incomplete" color="warning" variant="outlined" size="small" />
                    )}
                    {user.isActive === false && (
                      <Chip label="Disabled" color="default" variant="outlined" size="small" />
                    )}
                  </Box>
                </Box>
              </CardContent>
            </Card>

            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 3 }}>
              <Card>
                <CardContent sx={{ p: 3 }}>
                  <Typography variant="h6" sx={{ mb: 2 }}>
                    Account information
                  </Typography>
                  <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2 }}>
                    <DetailRow label="Display name" value={cleanText(user.displayName)} />
                    <DetailRow label="Email" value={cleanText(user.email)} />
                    <DetailRow label="Role" value={cleanText(user.role)} />
                    <DetailRow label="Account status" value={user.accountStatus} />
                    {user.invitationExpiresAtUtc && (
                      <DetailRow
                        label="Invitation expires"
                        value={formatDateTime(user.invitationExpiresAtUtc)}
                      />
                    )}
                    <DetailRow label="Profile status" value={profileStatus} />
                    <DetailRow label="Password change required" value={user.mustChangePassword ? 'Yes' : 'No'} />
                    {isSuperAdmin && <DetailRow label="Super Admin note" value={cleanText(user.superAdminNote) ?? '—'} />}
                  </Box>
                </CardContent>
              </Card>

              <Card>
                <CardContent sx={{ p: 3 }}>
                  <Typography variant="h6" sx={{ mb: 2 }}>
                    Limits
                  </Typography>

                  <Stack spacing={2}>
                    <Box
                      sx={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'flex-start',
                        gap: 2,
                        flexWrap: 'wrap',
                      }}
                    >
                      <Box>
                        <Typography variant="subtitle2">Per-export row limit</Typography>
                        <Typography variant="body2" color="text.secondary">
                          Controls how many site rows this user can export in one file.
                        </Typography>
                      </Box>
                      <Chip
                        label={getPerExportLimitSettingChip(user)}
                        color={user.exportLimitOverrideMode == null ? 'default' : 'warning'}
                        variant="outlined"
                        size="small"
                      />
                    </Box>

                    <Box sx={{ px: 0.25 }}>
                      <Typography variant="h6" sx={{ fontWeight: 700 }}>
                        {getPerExportLimitSettingValue(user, effectiveLimit)}
                      </Typography>
                    </Box>

                    {user.role === 'Client' && clientUsageLimitRows.length > 0 && (
                      <>
                        <Divider />

                        <Box
                          sx={{
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'flex-start',
                            gap: 2,
                            flexWrap: 'wrap',
                          }}
                        >
                          <Box>
                            <Typography variant="subtitle2">Client usage limits</Typography>
                            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
                              Daily and weekly quotas. Blank fields use the Client role default.
                            </Typography>
                          </Box>
                          <Chip
                            label={getClientUsageLimitsChip(user)}
                            color={clientUsageLimitRows.some((row) => row.getOverride(user) != null) ? 'warning' : 'default'}
                            variant="outlined"
                            size="small"
                          />
                        </Box>

                        <Box
                          sx={{
                            display: 'grid',
                            gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' },
                            gap: 1.5,
                          }}
                        >
                          {clientUsageLimitRows.map((row) => (
                            <Box key={row.label}>
                              <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                                {row.label}
                              </Typography>
                              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                                {formatClientUsageLimit(row.getEffective(user))}
                              </Typography>
                              <Typography variant="caption" color="text.secondary">
                                {row.helperText} · {row.getOverride(user) == null ? 'Role default' : 'Custom setting'}
                              </Typography>
                            </Box>
                          ))}
                        </Box>
                      </>
                    )}
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
                    <Typography variant="h6">Google Drive</Typography>
                    {googleDriveConnected !== null && (
                      <Chip
                        label={googleDriveConnected ? 'Connected' : 'Not connected'}
                        color={googleDriveConnected ? 'success' : 'default'}
                        size="small"
                        variant={googleDriveConnected ? 'filled' : 'outlined'}
                      />
                    )}
                  </Box>

                  <Divider sx={{ mb: 2 }} />

                  {googleDriveConnected === null ? (
                    <Typography variant="body2" color="text.secondary">
                      Google Drive status is not available.
                    </Typography>
                  ) : googleDriveConnected ? (
                    <Stack spacing={1.5}>
                      <DetailRow label="Status" value="Connected" />
                      {googleDrive?.googleEmail && (
                        <DetailRow label="Connected account" value={googleDrive.googleEmail} />
                      )}
                      {connectedAt && <DetailRow label="Connected" value={connectedAt} />}
                      {googleDrive?.needsReconnect && (
                        <Alert severity="warning">
                          This user's Google Drive access needs to be reconnected by the user.
                        </Alert>
                      )}
                    </Stack>
                  ) : (
                    <Typography variant="body2" color="text.secondary">
                      Google Drive is not connected.
                    </Typography>
                  )}
                </CardContent>
              </Card>

              {showCurrentClientUsage && user && (
                <Card>
                  <CardContent sx={{ p: 3 }}>
                    <Typography variant="h6" sx={{ mb: 1 }}>
                      Current client usage
                    </Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                      Calculated from the current rolling 24-hour and 7-day windows.
                    </Typography>
                    <Box
                      sx={{
                        display: 'grid',
                        gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' },
                        gap: 1.5,
                      }}
                    >
                      {CURRENT_CLIENT_USAGE_ROWS.map((row) => {
                        const value = formatUsageLimitPair(row.getUsed(user), row.getLimit(user));
                        if (!value) {
                          return null;
                        }

                        return (
                          <Box key={row.label}>
                            <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                              {row.label}
                            </Typography>
                            <Typography variant="body2" sx={{ fontWeight: 600 }}>
                              {value}
                            </Typography>
                          </Box>
                        );
                      })}
                    </Box>
                  </CardContent>
                </Card>
              )}

            </Box>
          </>
        ) : null}
      </Stack>
      <Dialog open={Boolean(invitationLink)} onClose={() => setInvitationLink(null)} maxWidth="sm" fullWidth>
        <DialogTitle>Invitation reissued</DialogTitle>
        <DialogContent>
          <Alert severity="warning" sx={{ mb: 2 }}>
            This activation link is shown only once. Copy it now and share it securely.
          </Alert>
          <Box sx={{ p: 2, bgcolor: 'grey.100', borderRadius: 1, wordBreak: 'break-all' }}>
            {invitationLink}
          </Box>
        </DialogContent>
        <DialogActions>
          <BrandButton
            kind="outline"
            onClick={() => void handleCopyInvitation()}
          >
            Copy link
          </BrandButton>
          <BrandButton onClick={() => setInvitationLink(null)}>Done</BrandButton>
        </DialogActions>
      </Dialog>
      <Snackbar
        open={copyNotification.open}
        autoHideDuration={2000}
        onClose={() => setCopyNotification((current) => ({ ...current, open: false }))}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          severity={copyNotification.severity}
          onClose={() => setCopyNotification((current) => ({ ...current, open: false }))}
          sx={{ width: '100%' }}
        >
          {copyNotification.message}
        </Alert>
      </Snackbar>
    </PageShell>
  );
};
