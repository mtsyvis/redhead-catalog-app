import React, { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Card,
  CardContent,
  CircularProgress,
  IconButton,
  InputAdornment,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import Check from '@mui/icons-material/Check';
import Close from '@mui/icons-material/Close';
import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';
import { useLocation, useNavigate } from 'react-router-dom';

import { BrandButton } from '../components/common/BrandButton';
import { PageShell } from '../components/layout/PageShell';
import { useAuth } from '../contexts/AuthContext';
import { ApiClientError } from '../services/api.client';
import { authService } from '../services/auth.service';

const validatePassword = (password: string) => ({
  minLength: password.length >= 8,
  hasDigit: /\d/.test(password),
  hasLower: /[a-z]/.test(password),
  hasUpper: /[A-Z]/.test(password),
  hasSpecial: /[^a-zA-Z0-9]/.test(password),
});

function getFieldError(errors: Record<string, string[]> | undefined, field: string): string | undefined {
  return errors?.[field]?.[0];
}

export const AccountSetup: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, refreshUser } = useAuth();
  const routeState = location.state as { currentPassword?: string } | null;

  const mustChangePassword = Boolean(user?.mustChangePassword);
  const mustCompleteProfile = Boolean(user?.mustCompleteProfile);
  const loginCurrentPassword = routeState?.currentPassword ?? '';
  const needsCurrentPasswordInput = mustChangePassword && !loginCurrentPassword;
  const passwordOnly = mustChangePassword && !mustCompleteProfile;
  const profileOnly = mustCompleteProfile && !mustChangePassword;
  const pageTitle = passwordOnly
    ? 'Create a new password'
    : profileOnly
      ? 'Complete your profile'
      : 'Complete your account';
  const pageDescription = passwordOnly
    ? needsCurrentPasswordInput
      ? 'Your password was reset by an administrator. Enter the temporary password you received and create a new password.'
      : 'Your password was reset by an administrator. Create a new password to continue.'
    : profileOnly
      ? 'Add your display name before continuing to the catalog.'
      : 'Finish the required account details before continuing to the catalog.';
  const submitLabel = passwordOnly ? 'Save password' : 'Continue';

  const [displayName, setDisplayName] = useState(user?.displayName ?? '');
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [saving, setSaving] = useState(false);
  const [generalError, setGeneralError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (user && !mustChangePassword && !mustCompleteProfile) {
      navigate('/sites', { replace: true });
    }
  }, [mustChangePassword, mustCompleteProfile, navigate, user]);

  useEffect(() => {
    setDisplayName(user?.displayName ?? '');
  }, [user?.displayName]);

  const passwordRules = useMemo(() => validatePassword(newPassword), [newPassword]);
  const isPasswordValid = Object.values(passwordRules).every(Boolean);
  const passwordsMatch = newPassword === confirmPassword && confirmPassword.length > 0;

  const trimmedDisplayName = displayName.trim();
  const canSubmit =
    !saving &&
    (!mustCompleteProfile || trimmedDisplayName.length > 0) &&
    (!mustChangePassword ||
      (isPasswordValid && passwordsMatch && (!needsCurrentPasswordInput || currentPassword.length > 0)));

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (saving) return;

    setGeneralError(null);
    const nextFieldErrors: Record<string, string[]> = {};

    if (mustCompleteProfile) {
      if (!trimmedDisplayName) nextFieldErrors.displayName = ['Display name is required.'];
    }

    if (mustChangePassword) {
      if (needsCurrentPasswordInput && !currentPassword) {
        nextFieldErrors.currentPassword = ['Temporary password is required.'];
      }
      if (!isPasswordValid) {
        nextFieldErrors.newPassword = ['Password does not meet requirements.'];
      }
      if (!passwordsMatch) {
        nextFieldErrors.confirmPassword = ['Passwords do not match.'];
      }
    }

    setFieldErrors(nextFieldErrors);
    if (Object.keys(nextFieldErrors).length > 0) return;

    setSaving(true);
    try {
      await authService.completeAccountSetup({
        currentPassword: mustChangePassword ? loginCurrentPassword || currentPassword : null,
        newPassword: mustChangePassword ? newPassword : null,
        displayName: mustCompleteProfile ? trimmedDisplayName : null,
      });
      await refreshUser();
      navigate('/sites', { replace: true });
    } catch (error) {
      if (error instanceof ApiClientError) {
        setFieldErrors(error.fieldErrors ?? {});
        if (error.errors?.length) {
          setGeneralError(error.errors.join(' '));
        } else if (!error.fieldErrors || Object.keys(error.fieldErrors).length === 0) {
          setGeneralError(error.message);
        }
      } else {
        setGeneralError('Account setup could not be completed. Please try again.');
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <PageShell title={pageTitle} maxWidth="sm">
      <Card>
        <CardContent sx={{ p: 4 }}>
          <Stack spacing={3}>
            <Typography variant="body2" color="text.secondary">
              {pageDescription}
            </Typography>

            {generalError && <Alert severity="error">{generalError}</Alert>}

            <Box component="form" onSubmit={handleSubmit}>
              <Stack spacing={2}>
                {mustCompleteProfile && (
                  <TextField
                    label="Display name"
                    required
                    fullWidth
                    value={displayName}
                    onChange={(event) => {
                      setDisplayName(event.target.value);
                      setFieldErrors((prev) => ({ ...prev, displayName: [] }));
                    }}
                    disabled={saving}
                    error={Boolean(getFieldError(fieldErrors, 'displayName'))}
                    helperText={getFieldError(fieldErrors, 'displayName')}
                    autoComplete="name"
                    autoFocus
                  />
                )}

                {mustChangePassword && (
                  <>
                    {needsCurrentPasswordInput && (
                      <TextField
                        label="Temporary password"
                        type={showCurrentPassword ? 'text' : 'password'}
                        required
                        fullWidth
                        value={currentPassword}
                        onChange={(event) => {
                          setCurrentPassword(event.target.value);
                          setFieldErrors((prev) => ({ ...prev, currentPassword: [] }));
                        }}
                        disabled={saving}
                        error={Boolean(getFieldError(fieldErrors, 'currentPassword'))}
                        helperText={getFieldError(fieldErrors, 'currentPassword')}
                        autoComplete="current-password"
                        autoFocus={!mustCompleteProfile}
                        slotProps={{
                          input: {
                            endAdornment: (
                              <InputAdornment position="end">
                                <IconButton
                                  size="small"
                                  onClick={() => setShowCurrentPassword((value) => !value)}
                                  edge="end"
                                  aria-label={showCurrentPassword ? 'Hide temporary password' : 'Show temporary password'}
                                  sx={{ color: 'rgba(38,38,38,0.55)' }}
                                >
                                  {showCurrentPassword ? (
                                    <VisibilityOff fontSize="small" />
                                  ) : (
                                    <Visibility fontSize="small" />
                                  )}
                                </IconButton>
                              </InputAdornment>
                            ),
                          },
                        }}
                      />
                    )}

                    <TextField
                      label="New password"
                      type={showNewPassword ? 'text' : 'password'}
                      required
                      fullWidth
                      value={newPassword}
                      onChange={(event) => {
                        setNewPassword(event.target.value);
                        setFieldErrors((prev) => ({ ...prev, newPassword: [] }));
                      }}
                      disabled={saving}
                      error={Boolean(getFieldError(fieldErrors, 'newPassword'))}
                      helperText={getFieldError(fieldErrors, 'newPassword')}
                      autoComplete="new-password"
                      autoFocus={!mustCompleteProfile && !needsCurrentPasswordInput}
                      slotProps={{
                        input: {
                          endAdornment: (
                            <InputAdornment position="end">
                              <IconButton
                                size="small"
                                onClick={() => setShowNewPassword((value) => !value)}
                                edge="end"
                                aria-label={showNewPassword ? 'Hide new password' : 'Show new password'}
                                sx={{ color: 'rgba(38,38,38,0.55)' }}
                              >
                                {showNewPassword ? (
                                  <VisibilityOff fontSize="small" />
                                ) : (
                                  <Visibility fontSize="small" />
                                )}
                              </IconButton>
                            </InputAdornment>
                          ),
                        },
                      }}
                    />

                    {newPassword && (
                      <Box sx={{ p: 2, bgcolor: 'grey.50', borderRadius: 1 }}>
                        <Typography variant="caption" sx={{ fontWeight: 600, mb: 1, display: 'block' }}>
                          Password requirements
                        </Typography>
                        <List dense disablePadding>
                          <PasswordRule met={passwordRules.minLength}>At least 8 characters</PasswordRule>
                          <PasswordRule met={passwordRules.hasDigit}>Contains a number</PasswordRule>
                          <PasswordRule met={passwordRules.hasUpper}>Contains an uppercase letter</PasswordRule>
                          <PasswordRule met={passwordRules.hasLower}>Contains a lowercase letter</PasswordRule>
                          <PasswordRule met={passwordRules.hasSpecial}>Contains a special character</PasswordRule>
                        </List>
                      </Box>
                    )}

                    <TextField
                      label="Confirm password"
                      type={showConfirmPassword ? 'text' : 'password'}
                      required
                      fullWidth
                      value={confirmPassword}
                      onChange={(event) => {
                        setConfirmPassword(event.target.value);
                        setFieldErrors((prev) => ({ ...prev, confirmPassword: [] }));
                      }}
                      disabled={saving}
                      error={Boolean(getFieldError(fieldErrors, 'confirmPassword'))}
                      helperText={getFieldError(fieldErrors, 'confirmPassword')}
                      autoComplete="new-password"
                      slotProps={{
                        input: {
                          endAdornment: (
                            <InputAdornment position="end">
                              <IconButton
                                size="small"
                                onClick={() => setShowConfirmPassword((value) => !value)}
                                edge="end"
                                aria-label={showConfirmPassword ? 'Hide confirmation password' : 'Show confirmation password'}
                                sx={{ color: 'rgba(38,38,38,0.55)' }}
                              >
                                {showConfirmPassword ? (
                                  <VisibilityOff fontSize="small" />
                                ) : (
                                  <Visibility fontSize="small" />
                                )}
                              </IconButton>
                            </InputAdornment>
                          ),
                        },
                      }}
                    />
                  </>
                )}

                <BrandButton
                  kind="primary"
                  type="submit"
                  size="large"
                  fullWidth
                  disabled={!canSubmit}
                  sx={{ height: 52 }}
                >
                  {saving ? <CircularProgress size={22} color="inherit" /> : submitLabel}
                </BrandButton>
              </Stack>
            </Box>
          </Stack>
        </CardContent>
      </Card>
    </PageShell>
  );
};

const PasswordRule: React.FC<{ met: boolean; children: React.ReactNode }> = ({ met, children }) => (
  <ListItem disablePadding sx={{ py: 0.25 }}>
    <ListItemIcon sx={{ minWidth: 32 }}>
      {met ? <Check fontSize="small" color="success" /> : <Close fontSize="small" color="error" />}
    </ListItemIcon>
    <ListItemText
      primary={children}
      primaryTypographyProps={{
        variant: 'caption',
        color: met ? 'success.main' : 'error.main',
      }}
    />
  </ListItem>
);
