import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Navigate, useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Divider,
  Stack,
  Typography,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import StarIcon from '@mui/icons-material/Star';

import { BrandButton } from '../components/common/BrandButton';
import { InvitationResultDialog } from '../components/admin/InvitationResultDialog';
import { ReactivationResultDialog } from '../components/admin/ReactivationResultDialog';
import { PageShell } from '../components/layout/PageShell';
import { ApiClientError } from '../services/api.client';
import { adminUsersService } from '../services/adminUsers.service';
import type {
  AdminUserDetails as AdminUserDetailsType,
  InvitationEmailDeliveryStatus,
} from '../types/adminUsers.types';
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
  getUsed: (user: AdminUserDetailsType) => number | null | undefined;
  getLimit: (user: AdminUserDetailsType) => number | null | undefined;
  getOverride: (user: AdminUserDetailsType) => number | null;
}> = [
  {
    label: 'Daily exported domains',
    helperText: '24-hour rolling window',
    getUsed: (user) => user.clientExportUsage?.dailyUniqueExportedDomainsUsed,
    getLimit: (user) => user.clientExportUsage?.dailyUniqueExportedDomainsLimit,
    getOverride: (user) => user.dailyUniqueExportedDomainsLimitOverride,
  },
  {
    label: 'Weekly exported domains',
    helperText: '7-day rolling window',
    getUsed: (user) => user.clientExportUsage?.weeklyUniqueExportedDomainsUsed,
    getLimit: (user) => user.clientExportUsage?.weeklyUniqueExportedDomainsLimit,
    getOverride: (user) => user.weeklyUniqueExportedDomainsLimitOverride,
  },
  {
    label: 'Daily exports',
    helperText: '24-hour rolling window',
    getUsed: (user) => user.clientExportUsage?.dailyExportOperationsUsed,
    getLimit: (user) => user.clientExportUsage?.dailyExportOperationsLimit,
    getOverride: (user) => user.dailyExportOperationsLimitOverride,
  },
  {
    label: 'Weekly exports',
    helperText: '7-day rolling window',
    getUsed: (user) => user.clientExportUsage?.weeklyExportOperationsUsed,
    getLimit: (user) => user.clientExportUsage?.weeklyExportOperationsLimit,
    getOverride: (user) => user.weeklyExportOperationsLimitOverride,
  },
];

function getPerExportLimitSettingChip(user: AdminUserDetailsType): string {
  if (user.isExportLimitEditable === false) {
    return 'Fixed';
  }

  return user.exportLimitOverrideMode == null
    ? 'Role default'
    : 'Custom';
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
    ? 'Custom'
    : 'Role defaults';
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

interface MetricItemProps {
  label: string;
  value: React.ReactNode;
  helperText?: React.ReactNode;
}

const MetricItem: React.FC<MetricItemProps> = ({ label, value, helperText }) => (
  <Box>
    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.25 }}>
      {label}
    </Typography>
    <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: 'tabular-nums' }}>
      {value}
    </Typography>
    {helperText && (
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.25 }}>
        {helperText}
      </Typography>
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
  const [invitationResult, setInvitationResult] = useState<{
    activationUrl: string;
    emailDeliveryStatus: InvitationEmailDeliveryStatus;
  } | null>(null);
  const [reactivationResult, setReactivationResult] = useState<{
    fallbackUrl: string | null;
    emailDeliveryStatus: InvitationEmailDeliveryStatus;
  } | null>(null);

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
      setInvitationResult({
        activationUrl: response.activationUrl,
        emailDeliveryStatus: response.emailDeliveryStatus,
      });
      await loadUser();
    } catch (reissueError) {
      setError(reissueError instanceof ApiClientError ? reissueError.message : 'Failed to reissue invitation.');
    } finally {
      setReissuing(false);
    }
  };

  const handleReissueReactivation = async () => {
    if (!user) return;

    setReissuing(true);
    setError(null);
    try {
      const response = await adminUsersService.reissueReactivation(user.id);
      setReactivationResult({
        fallbackUrl: response.fallbackUrl,
        emailDeliveryStatus: response.emailDeliveryStatus,
      });
      await loadUser();
    } catch (reissueError) {
      setError(reissueError instanceof ApiClientError ? reissueError.message : 'Failed to reissue reactivation.');
    } finally {
      setReissuing(false);
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
          ) ? (
            <BrandButton kind="primary" onClick={() => void handleReissueInvitation()} disabled={reissuing}>
              {reissuing ? <CircularProgress size={20} color="inherit" /> : 'Reissue invitation'}
            </BrandButton>
          ) : null}
          {canManageUsers && user && (
            user.accountStatus === 'PendingReactivation' || user.accountStatus === 'ReactivationExpired'
          ) ? (
            <BrandButton kind="primary" onClick={() => void handleReissueReactivation()} disabled={reissuing}>
              {reissuing ? <CircularProgress size={20} color="inherit" /> : 'Reissue reactivation'}
            </BrandButton>
          ) : null}
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
                    {user.role === 'Client' && user.isTrustedClient && (
                      <Chip
                        icon={<StarIcon />}
                        label="Trusted client"
                        color="warning"
                        size="small"
                      />
                    )}
                    {user.accountStatus === 'PendingActivation' && (
                      <Chip label="Pending activation" color="info" variant="outlined" size="small" />
                    )}
                    {user.accountStatus === 'InvitationExpired' && (
                      <Chip label="Invitation expired" color="warning" variant="outlined" size="small" />
                    )}
                    {user.accountStatus === 'PendingReactivation' && (
                      <Chip label="Pending reactivation" color="info" variant="outlined" size="small" />
                    )}
                    {user.accountStatus === 'ReactivationExpired' && (
                      <Chip label="Reactivation expired" color="warning" variant="outlined" size="small" />
                    )}
                    {user.mustCompleteProfile && user.accountStatus === 'Active' && (
                      <Chip label="Profile incomplete" color="warning" variant="outlined" size="small" />
                    )}
                    {user.accountStatus === 'Disabled' && (
                      <Chip
                        label={user.disabledReason === 'ClientCatalogAutoBan' ? 'Auto-disabled' : 'Disabled'}
                        color={user.disabledReason === 'ClientCatalogAutoBan' ? 'error' : 'default'}
                        variant="outlined"
                        size="small"
                      />
                    )}
                  </Box>
                </Box>
              </CardContent>
            </Card>

            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 3 }}>
              {user.latestClientCatalogAutoBan && (
                <Card sx={{ gridColumn: { md: '1 / -1' }, border: 1, borderColor: user.disabledReason === 'ClientCatalogAutoBan' ? 'error.main' : 'divider' }}>
                  <CardContent sx={{ p: 3 }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap', mb: 2 }}>
                      <Box>
                        <Typography variant="h6">Automatic catalog ban</Typography>
                        <Typography variant="body2" color="text.secondary">
                          Triggered when rolling 24-hour unique-site activity reached the configured security threshold.
                        </Typography>
                      </Box>
                      <Chip
                        label={user.latestClientCatalogAutoBan.reviewedAtUtc ? 'Reviewed' : 'Review required'}
                        color={user.latestClientCatalogAutoBan.reviewedAtUtc ? 'default' : 'error'}
                        size="small"
                      />
                    </Box>
                    {user.disabledReason === 'ClientCatalogAutoBan' && (
                      <Alert severity="error" sx={{ mb: 2 }}>
                        This account is currently disabled because suspicious catalog activity reached the automatic-ban threshold.
                      </Alert>
                    )}
                    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', md: 'repeat(4, 1fr)' }, gap: 2 }}>
                      <DetailRow label="Detected" value={formatDateTime(user.latestClientCatalogAutoBan.detectedAtUtc)} />
                      <DetailRow label="24h unique sites" value={user.latestClientCatalogAutoBan.uniqueSites.toLocaleString()} />
                      <DetailRow label="Threshold" value={user.latestClientCatalogAutoBan.threshold.toLocaleString()} />
                      <DetailRow
                        label="Notification email"
                        value={user.latestClientCatalogAutoBan.emailSentAtUtc
                          ? `Sent ${formatDateTime(user.latestClientCatalogAutoBan.emailSentAtUtc)}`
                          : 'Pending'}
                      />
                      {user.latestClientCatalogAutoBan.reviewedAtUtc && (
                        <DetailRow label="Reviewed" value={formatDateTime(user.latestClientCatalogAutoBan.reviewedAtUtc)} />
                      )}
                      {user.disabledAtUtc && (
                        <DetailRow label="Account disabled" value={formatDateTime(user.disabledAtUtc)} />
                      )}
                    </Box>
                  </CardContent>
                </Card>
              )}

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
                    <DetailRow label="Authentication" value={user.isGoogleOnly ? 'Google' : 'Email and password'} />
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

              {user.role === 'Client' && user.clientCatalogActivity && (
                <Card>
                  <CardContent sx={{ p: 3 }}>
                    <Typography variant="h6" sx={{ mb: 1 }}>Catalog activity</Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                      Catalog requests, rate limits and unique sites issued through search, Multi-search and completed exports.
                    </Typography>
                    <Box
                      sx={{
                        display: 'grid',
                        gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, 1fr)' },
                        gap: 2,
                      }}
                    >
                      {user.clientCatalogActivity.map((window) => (
                        <MetricItem
                          key={window.period}
                          label={window.period}
                          value={`${window.uniqueSites.toLocaleString()} unique sites`}
                          helperText={`${window.requests.toLocaleString()} requests · ${window.rateLimitedRequests.toLocaleString()} rate-limited`}
                        />
                      ))}
                    </Box>
                  </CardContent>
                </Card>
              )}

              <Card>
                <CardContent sx={{ p: 3 }}>
                  <Typography variant="h6" sx={{ mb: 2 }}>
                    Export limits and usage
                  </Typography>

                  <Stack spacing={2}>
                    <Box>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                        <Typography variant="subtitle2">Per-export row limit</Typography>
                        <Chip
                          label={getPerExportLimitSettingChip(user)}
                          color={user.exportLimitOverrideMode == null ? 'default' : 'warning'}
                          variant="outlined"
                          size="small"
                          sx={{ height: 22, fontSize: '0.6875rem' }}
                        />
                      </Box>
                      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
                        Controls how many site rows this user can export in one file.
                      </Typography>
                    </Box>

                    <Box sx={{ px: 0.25 }}>
                      <Typography variant="h6" sx={{ fontWeight: 700 }}>
                        {getPerExportLimitSettingValue(user, effectiveLimit)}
                      </Typography>
                    </Box>

                    {user.role === 'Client' && clientUsageLimitRows.length > 0 && (
                      <>
                        <Divider />

                        <Box>
                          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                            <Typography variant="subtitle2">Daily and weekly usage</Typography>
                            <Chip
                              label={getClientUsageLimitsChip(user)}
                              color={clientUsageLimitRows.some((row) => row.getOverride(user) != null) ? 'warning' : 'default'}
                              variant="outlined"
                              size="small"
                              sx={{ height: 22, fontSize: '0.6875rem' }}
                            />
                          </Box>
                          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
                            Current usage against limits in rolling 24-hour and 7-day windows.
                          </Typography>
                        </Box>

                        <Box
                          sx={{
                            display: 'grid',
                            gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' },
                            gap: 1.5,
                          }}
                        >
                          {clientUsageLimitRows.map((row) => (
                            <MetricItem
                              key={row.label}
                              label={row.label}
                              value={formatUsageLimitPair(row.getUsed(user), row.getLimit(user)) ?? 'Not available'}
                              helperText={`${row.helperText} · ${row.getOverride(user) == null ? 'Role default' : 'Custom setting'}`}
                            />
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

            </Box>
          </>
        ) : null}
      </Stack>
      {invitationResult && user ? (
        <InvitationResultDialog
          key={invitationResult.activationUrl}
          title="Invitation reissued"
          email={user.email}
          activationUrl={invitationResult.activationUrl}
          emailDeliveryStatus={invitationResult.emailDeliveryStatus}
          invitationAction="reissued"
          onClose={() => setInvitationResult(null)}
        />
      ) : null}
      {reactivationResult && user ? (
        <ReactivationResultDialog
          key={reactivationResult.fallbackUrl ?? reactivationResult.emailDeliveryStatus}
          title="Reactivation reissued"
          email={user.email}
          fallbackUrl={reactivationResult.fallbackUrl}
          emailDeliveryStatus={reactivationResult.emailDeliveryStatus}
          onClose={() => setReactivationResult(null)}
        />
      ) : null}
    </PageShell>
  );
};
