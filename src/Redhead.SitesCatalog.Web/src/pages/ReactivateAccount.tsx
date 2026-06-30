import React, { useEffect, useMemo, useState } from 'react';
import Check from '@mui/icons-material/Check';
import Close from '@mui/icons-material/Close';
import {
  Alert,
  Box,
  Card,
  CardContent,
  CircularProgress,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useNavigate, useSearchParams } from 'react-router-dom';

import { BrandButton } from '../components/common/BrandButton';
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

export const ReactivateAccount: React.FC = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { refreshUser } = useAuth();
  const token = searchParams.get('token') ?? '';

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const loadReactivation = async () => {
      if (!token) {
        setError('This reactivation link is invalid.');
        setLoading(false);
        return;
      }

      try {
        const reactivation = await authService.getReactivation(token);
        if (!cancelled) setEmail(reactivation.email);
      } catch (loadError) {
        if (!cancelled) {
          setError(
            loadError instanceof ApiClientError
              ? loadError.message
              : 'Could not validate this reactivation link.'
          );
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    void loadReactivation();
    return () => {
      cancelled = true;
    };
  }, [token]);

  const passwordRules = useMemo(() => validatePassword(password), [password]);
  const passwordValid = Object.values(passwordRules).every(Boolean);
  const passwordsMatch = password === confirmPassword && confirmPassword.length > 0;
  const canSubmit = !saving && passwordValid && passwordsMatch;

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canSubmit) return;

    setSaving(true);
    setError(null);
    try {
      await authService.reactivateAccount({ token, password });
      await refreshUser();
      navigate('/sites', { replace: true });
    } catch (reactivationError) {
      setError(
        reactivationError instanceof ApiClientError
          ? reactivationError.message
          : 'Account reactivation failed. Please try again.'
      );
    } finally {
      setSaving(false);
    }
  };

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'grid',
        placeItems: 'center',
        p: 3,
        bgcolor: 'grey.50',
      }}
    >
      <Card sx={{ width: '100%', maxWidth: 560 }}>
        <CardContent sx={{ p: 4 }}>
          <Stack spacing={3}>
            <Typography variant="h5" component="h1">
              Reactivate your account
            </Typography>

            {loading ? (
              <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
                <CircularProgress />
              </Box>
            ) : error && !email ? (
              <>
                <Alert severity="error">{error}</Alert>
                <BrandButton kind="outline" onClick={() => navigate('/login')}>
                  Go to sign in
                </BrandButton>
              </>
            ) : (
              <Box component="form" onSubmit={handleSubmit}>
                <Stack spacing={2}>
                  {error ? <Alert severity="error">{error}</Alert> : null}
                  <Alert severity="info">
                    Choose a new password to restore access to your account.
                  </Alert>
                  <TextField label="Email" value={email} disabled fullWidth />
                  <TextField
                    label="New password"
                    type="password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    autoComplete="new-password"
                    required
                    autoFocus
                    fullWidth
                  />
                  {password ? (
                    <Box sx={{ p: 2, bgcolor: 'grey.50', borderRadius: 1 }}>
                      <List dense disablePadding>
                        <PasswordRule met={passwordRules.minLength}>At least 8 characters</PasswordRule>
                        <PasswordRule met={passwordRules.hasDigit}>Contains a number</PasswordRule>
                        <PasswordRule met={passwordRules.hasUpper}>Contains an uppercase letter</PasswordRule>
                        <PasswordRule met={passwordRules.hasLower}>Contains a lowercase letter</PasswordRule>
                        <PasswordRule met={passwordRules.hasSpecial}>Contains a special character</PasswordRule>
                      </List>
                    </Box>
                  ) : null}
                  <TextField
                    label="Confirm password"
                    type="password"
                    value={confirmPassword}
                    onChange={(event) => setConfirmPassword(event.target.value)}
                    error={confirmPassword.length > 0 && !passwordsMatch}
                    helperText={confirmPassword.length > 0 && !passwordsMatch ? 'Passwords do not match.' : ''}
                    autoComplete="new-password"
                    required
                    fullWidth
                  />
                  <BrandButton kind="primary" type="submit" disabled={!canSubmit} fullWidth size="large">
                    {saving ? <CircularProgress size={22} color="inherit" /> : 'Reactivate account'}
                  </BrandButton>
                </Stack>
              </Box>
            )}
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
};

const PasswordRule: React.FC<{ met: boolean; children: React.ReactNode }> = ({ met, children }) => (
  <ListItem disablePadding sx={{ py: 0.25 }}>
    <ListItemIcon sx={{ minWidth: 32 }}>
      {met ? <Check fontSize="small" color="success" /> : <Close fontSize="small" color="error" />}
    </ListItemIcon>
    <ListItemText
      primary={children}
      primaryTypographyProps={{ variant: 'caption', color: met ? 'success.main' : 'error.main' }}
    />
  </ListItem>
);
