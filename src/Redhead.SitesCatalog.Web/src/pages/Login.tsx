import React, { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Card,
  CardContent,
  Checkbox,
  CircularProgress,
  Divider,
  FormControlLabel,
  IconButton,
  InputAdornment,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';
import Google from '@mui/icons-material/Google';
import { useLocation, useNavigate } from 'react-router-dom';

import { useAuth } from '../contexts/AuthContext';
import { ApiClientError } from '../services/api.client';
import { BrandButton } from '../components/common/BrandButton';
import { authService } from '../services/auth.service';

import mark from '../assets/brand/redhead-lockup.svg';

interface LoginRouteState {
  from?: {
    pathname?: string;
    search?: string;
    hash?: string;
  };
  sessionExpired?: boolean;
}

const googleErrorMessages: Record<string, string> = {
  unavailable: 'Google sign-in is currently unavailable. Please use your email and password.',
  cancelled: 'Google sign-in was cancelled. Please try again.',
  invalid: 'Google could not provide a verified email for this account.',
  'email-conflict': 'An account with this email already exists. Sign in with your existing credentials or contact an administrator.',
  disabled: 'This account has been disabled. Please contact an administrator.',
  failed: 'Google sign-in could not be completed. Please try again.',
};

export const Login: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const { login } = useAuth();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [isGoogleEnabled, setIsGoogleEnabled] = useState(false);

  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const routeState = location.state as LoginRouteState | null;
  const fromPathname = routeState?.from?.pathname || '/';
  const from = `${fromPathname}${routeState?.from?.search ?? ''}${routeState?.from?.hash ?? ''}`;
  const googleErrorCode = new URLSearchParams(location.search).get('googleAuth');
  const displayedError = error || (googleErrorCode ? googleErrorMessages[googleErrorCode] : null);

  useEffect(() => {
    let cancelled = false;

    void authService.getGoogleAuthenticationStatus()
      .then((status) => {
        if (!cancelled) setIsGoogleEnabled(status.enabled);
      })
      .catch(() => {
        if (!cancelled) setIsGoogleEnabled(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsLoading(true);

    try {
      const userData = await login({ email, password, rememberMe });
      if (userData.mustChangePassword || userData.mustCompleteProfile) {
        navigate('/account-setup', {
          replace: true,
          state: { currentPassword: userData.mustChangePassword ? password : undefined },
        });
      } else {
        navigate(from, { replace: true });
      }
    } catch (err) {
      if (err instanceof ApiClientError) setError(err.message);
      else setError('An unexpected error occurred. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleGoogleSignIn = () => {
    window.location.assign(`/api/auth/google/start?returnUrl=${encodeURIComponent(from)}`);
  };

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'grid',
        placeItems: 'center',
        p: 3,
        background: `
          radial-gradient(circle at 18% 12%, rgba(255,69,91,0.12), transparent 45%),
          radial-gradient(circle at 82% 88%, rgba(255,124,50,0.10), transparent 50%),
          linear-gradient(180deg, #ffffff 0%, #F6F7FB 100%)
        `,
      }}
    >
      <Card sx={{ width: '100%', maxWidth: 560 }}>
        <CardContent sx={{ p: 5 }}>
          <Stack spacing={1.25} alignItems="center" sx={{ mb: 3 }}>
            <Box component="img" src={mark} alt="Readhead" sx={{ height: 44 }} />

            <Box sx={{paddingTop: 1.5}}>
              <Typography
                variant="h6"
                component="h1"
                align="center"
                sx={{ fontWeight: 400, color: 'text.primary' }}
              >
                Sign in to the Websites Catalog
              </Typography>
            </Box>

          </Stack>

          {displayedError && (
            <Alert severity="error" sx={{ mb: 3 }}>
              {displayedError}
            </Alert>
          )}

          {routeState?.sessionExpired && !displayedError && (
            <Alert severity="info" sx={{ mb: 3 }}>
              Your session has expired. Please sign in again.
            </Alert>
          )}

          <form onSubmit={handleSubmit}>
            <Stack spacing={2}>
              <TextField
                label="Email"
                type="email"
                fullWidth
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                disabled={isLoading}
                autoComplete="email"
                autoFocus
              />

              <TextField
                label="Password"
                type={showPassword ? 'text' : 'password'}
                fullWidth
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                disabled={isLoading}
                autoComplete="current-password"
                slotProps={{
                  input: {
                    endAdornment: (
                      <InputAdornment position="end">
                        <IconButton
                          size="small"
                          onClick={() => setShowPassword((v) => !v)}
                          edge="end"
                          aria-label={showPassword ? 'Hide password' : 'Show password'}
                          sx={{ color: 'rgba(38,38,38,0.55)' }}
                        >
                          {showPassword ? <VisibilityOff fontSize="small" /> : <Visibility fontSize="small" />}
                        </IconButton>
                      </InputAdornment>
                    ),
                  },
                }}
              />

              <FormControlLabel
                control={
                  <Checkbox
                    checked={rememberMe}
                    onChange={(e) => setRememberMe(e.target.checked)}
                    disabled={isLoading}
                    size="small"
                  />
                }
                label={<Typography variant="body2">Remember me</Typography>}
                sx={{ mt: 0.5 }}
              />

              <BrandButton kind="primary" type="submit" fullWidth size="large" disabled={isLoading} sx={{ height: 52 }}>
                {isLoading ? <CircularProgress size={22} color="inherit" /> : 'Sign In'}
              </BrandButton>
            </Stack>
          </form>

          {isGoogleEnabled ? (
            <>
              <Divider sx={{ my: 3 }}>or</Divider>
              <BrandButton
                kind="outline"
                type="button"
                fullWidth
                size="large"
                startIcon={<Google />}
                onClick={handleGoogleSignIn}
                disabled={isLoading}
                sx={{ height: 52 }}
              >
                Continue with Google
              </BrandButton>
              <Typography variant="caption" color="text.secondary" align="center" sx={{ display: 'block', mt: 1.5 }}>
                New Google accounts are registered with Lite access.
              </Typography>
            </>
          ) : null}
        </CardContent>
      </Card>
    </Box>
  );
};
