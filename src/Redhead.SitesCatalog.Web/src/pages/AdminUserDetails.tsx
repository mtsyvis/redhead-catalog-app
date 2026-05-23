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

import { BrandButton } from '../components/common/BrandButton';
import { PageShell } from '../components/layout/PageShell';
import { useAuth } from '../contexts/AuthContext';
import { ApiClientError } from '../services/api.client';
import { adminUsersService } from '../services/adminUsers.service';
import type { AdminUserDetails as AdminUserDetailsType } from '../types/adminUsers.types';
import type { ExportLimitMode } from '../utils/exportLimit';
import { formatExportLimit } from '../utils/exportLimit';

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

function getLimitSource(user: AdminUserDetailsType): string | null {
  if (user.isExportLimitEditable === false) return 'Fixed system setting';
  if (user.isExportLimitOverridden === true) return 'Personal override';
  if (user.isExportLimitOverridden === false) return 'Inherited from role';
  return null;
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
  const { user: currentUser } = useAuth();
  const { userId } = useParams<{ userId: string }>();
  const navigate = useNavigate();
  const [user, setUser] = useState<AdminUserDetailsType | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const isAdmin = currentUser?.roles?.some((role) => role === 'Admin' || role === 'SuperAdmin');
  const isSuperAdmin = currentUser?.roles?.includes('SuperAdmin');

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

  const displayName = useMemo(() => {
    if (!user) return '';
    return cleanText(user.displayName) ?? cleanText(user.email) ?? 'User';
  }, [user]);

  const profileStatus = user?.mustCompleteProfile ? 'Incomplete' : 'Complete';
  const effectiveLimit = user
    ? formatMaybeExportLimit(user.effectiveExportLimitMode, user.effectiveExportLimitRows)
    : null;
  const overrideLimit = user
    ? formatMaybeExportLimit(user.exportLimitOverrideMode, user.exportLimitRowsOverride)
    : null;
  const limitSource = user ? getLimitSource(user) : null;
  const googleDrive = user?.googleDrive;
  const googleDriveConnected = user ? user.googleDriveConnected : null;
  const connectedAt = formatDateTime(googleDrive?.connectedAtUtc);

  if (!isAdmin) {
    return <Navigate to="/sites" replace />;
  }

  return (
    <PageShell title="User details" maxWidth="lg">
      <Stack spacing={3}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap' }}>
          <BrandButton kind="outline" startIcon={<ArrowBackIcon />} onClick={() => navigate('/admin/users')}>
            Back to users
          </BrandButton>
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
                    {user.mustCompleteProfile && (
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
                    <DetailRow label="First name" value={cleanText(user.firstName)} />
                    <DetailRow label="Last name" value={cleanText(user.lastName)} />
                    <DetailRow label="Name" value={cleanText(user.displayName)} />
                    <DetailRow label="Email" value={cleanText(user.email)} />
                    <DetailRow label="Role" value={cleanText(user.role)} />
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
                  {effectiveLimit || overrideLimit || limitSource ? (
                    <Stack spacing={2}>
                      <DetailRow label="Effective export limit" value={effectiveLimit ?? 'Not available'} />
                      {limitSource && <DetailRow label="Source" value={limitSource} />}
                      <DetailRow label="User override" value={overrideLimit ?? 'No custom limit'} />
                    </Stack>
                  ) : (
                    <Typography variant="body2" color="text.secondary">
                      Limit information is not available for this user.
                    </Typography>
                  )}
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
    </PageShell>
  );
};
