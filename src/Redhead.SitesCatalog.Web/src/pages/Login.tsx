import React, { useState } from 'react';
import {
  Alert,
  Box,
  Card,
  CardContent,
  Checkbox,
  CircularProgress,
  FormControlLabel,
  IconButton,
  InputAdornment,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';
import { useLocation, useNavigate } from 'react-router-dom';

import { useAuth } from '../contexts/AuthContext';
import { ApiClientError } from '../services/api.client';
import { BrandButton } from '../components/common/BrandButton';

import mark from '../assets/brand/redhead-lockup.svg';

export const Login: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const { login } = useAuth();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const from = (location.state as { from?: { pathname: string } })?.from?.pathname || '/';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsLoading(true);

    try {
      const userData = await login({ email, password, rememberMe });
      if (userData.mustChangePassword) navigate('/change-password', { replace: true });
      else navigate(from, { replace: true });
    } catch (err) {
      if (err instanceof ApiClientError) setError(err.message);
      else setError('An unexpected error occurred. Please try again.');
    } finally {
      setIsLoading(false);
    }
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

          {error && (
            <Alert severity="error" sx={{ mb: 3, borderRadius: 2 }}>
              {error}
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
        </CardContent>
      </Card>
    </Box>
  );
};
