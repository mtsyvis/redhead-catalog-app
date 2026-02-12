import React, { useState } from 'react';
import {
  Card,
  CardContent,
  TextField,
  Typography,
  Alert,
  CircularProgress,
  Box,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
} from '@mui/material';
import { Check, Close } from '@mui/icons-material';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { BrandButton } from '../components/common/BrandButton';
import { PageShell } from '../components/layout/PageShell';
import { ApiClientError } from '../services/api.client';
import { authService } from '../services/auth.service';

/**
 * Password validation rules
 */
const validatePassword = (password: string) => {
  return {
    minLength: password.length >= 8,
    hasDigit: /\d/.test(password),
    hasLower: /[a-z]/.test(password),
    hasUpper: /[A-Z]/.test(password),
    hasSpecial: /[^a-zA-Z0-9]/.test(password),
  };
};

/**
 * Change password page
 * Blocks navigation if mustChangePassword is true
 */
export const ChangePassword: React.FC = () => {
  const navigate = useNavigate();
  const { user, refreshUser } = useAuth();

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const passwordRules = validatePassword(newPassword);
  const isPasswordValid = Object.values(passwordRules).every(Boolean);
  const passwordsMatch = newPassword === confirmPassword && confirmPassword.length > 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(false);

    if (!isPasswordValid) {
      setError('Password does not meet requirements');
      return;
    }

    if (!passwordsMatch) {
      setError('Passwords do not match');
      return;
    }

    setIsLoading(true);

    try {
      await authService.changePassword({ currentPassword, newPassword });
      await refreshUser();
      setSuccess(true);
      
      // Clear form
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');

      // Navigate to home after successful change if not forced
      if (!user?.mustChangePassword) {
        setTimeout(() => navigate('/'), 2000);
      } else {
        // If mustChangePassword was true, refresh and redirect
        setTimeout(() => navigate('/', { replace: true }), 2000);
      }
    } catch (err) {
      if (err instanceof ApiClientError) {
        // Handle validation errors from API
        if (err.errors && err.errors.length > 0) {
          setError(err.errors.join('. '));
        } else {
          setError(err.message);
        }
      } else {
        setError('An unexpected error occurred. Please try again.');
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <PageShell title={user?.mustChangePassword ? 'Password Change Required' : 'Change Password'} maxWidth="sm">
      <Card>
        <CardContent sx={{ p: 4 }}>
          {user?.mustChangePassword && (
            <Alert severity="warning" sx={{ mb: 3 }}>
              You must change your password before continuing.
            </Alert>
          )}

          {success && (
            <Alert severity="success" sx={{ mb: 3 }}>
              Password changed successfully! Redirecting...
            </Alert>
          )}

          {error && (
            <Alert severity="error" sx={{ mb: 3 }}>
              {error}
            </Alert>
          )}

          <form onSubmit={handleSubmit}>
            <TextField
              label="Current Password"
              type="password"
              fullWidth
              required
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              disabled={isLoading}
              sx={{ mb: 2 }}
              autoComplete="current-password"
              autoFocus
            />

            <TextField
              label="New Password"
              type="password"
              fullWidth
              required
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              disabled={isLoading}
              sx={{ mb: 1 }}
              autoComplete="new-password"
            />

            {newPassword && (
              <Box sx={{ mb: 2, p: 2, bgcolor: 'grey.50', borderRadius: 1 }}>
                <Typography variant="caption" sx={{ fontWeight: 600, mb: 1, display: 'block' }}>
                  Password Requirements:
                </Typography>
                <List dense disablePadding>
                  <PasswordRule met={passwordRules.minLength}>
                    At least 8 characters
                  </PasswordRule>
                  <PasswordRule met={passwordRules.hasDigit}>
                    Contains a number
                  </PasswordRule>
                  <PasswordRule met={passwordRules.hasUpper}>
                    Contains an uppercase letter
                  </PasswordRule>
                  <PasswordRule met={passwordRules.hasLower}>
                    Contains a lowercase letter
                  </PasswordRule>
                  <PasswordRule met={passwordRules.hasSpecial}>
                    Contains a special character
                  </PasswordRule>
                </List>
              </Box>
            )}

            <TextField
              label="Confirm New Password"
              type="password"
              fullWidth
              required
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              disabled={isLoading}
              error={confirmPassword.length > 0 && !passwordsMatch}
              helperText={
                confirmPassword.length > 0 && !passwordsMatch
                  ? 'Passwords do not match'
                  : ''
              }
              sx={{ mb: 3 }}
              autoComplete="new-password"
            />

            <BrandButton
              type="submit"
              fullWidth
              size="large"
              disabled={isLoading || !isPasswordValid || !passwordsMatch}
              sx={{ position: 'relative' }}
            >
              {isLoading ? (
                <CircularProgress size={24} color="inherit" />
              ) : (
                'Change Password'
              )}
            </BrandButton>
          </form>
        </CardContent>
      </Card>
    </PageShell>
  );
};

/**
 * Password rule display component
 */
const PasswordRule: React.FC<{ met: boolean; children: React.ReactNode }> = ({
  met,
  children,
}) => (
  <ListItem disablePadding sx={{ py: 0.25 }}>
    <ListItemIcon sx={{ minWidth: 32 }}>
      {met ? (
        <Check fontSize="small" color="success" />
      ) : (
        <Close fontSize="small" color="error" />
      )}
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
