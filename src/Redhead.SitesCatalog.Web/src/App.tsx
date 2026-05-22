import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ThemeProvider, CssBaseline } from '@mui/material';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { AuthProvider } from './contexts/AuthContext';
import { theme } from './theme/theme';
import { ProtectedRoute } from './components/routing/ProtectedRoute';
import { AccountSetupRequiredRoute } from './components/routing/AccountSetupRequiredRoute';
import { Login } from './pages/Login';
import { ChangePassword } from './pages/ChangePassword';
import { AccountSetup } from './pages/AccountSetup';
import { Profile } from './pages/Profile';
import { Sites } from './pages/Sites';
import { Imports } from './pages/Imports';
import { AdminUsers } from './pages/AdminUsers';
import { RoleSettings } from './pages/RoleSettings';
import { OAuthHome, PrivacyPolicy, TermsOfService } from './pages/OAuthVerificationPages';

/**
 * Main App component
 */
const App: React.FC = () => {
  return (
    <ThemeProvider theme={theme}>
      <LocalizationProvider dateAdapter={AdapterDayjs}>
      <CssBaseline />
      <BrowserRouter basename="/">
        <AuthProvider>
          <Routes>
            {/* Public routes */}
            <Route path="/login" element={<Login />} />
            <Route path="/oauth-home" element={<OAuthHome />} />
            <Route path="/privacy-policy" element={<PrivacyPolicy />} />
            <Route path="/terms-of-service" element={<TermsOfService />} />

            {/* Protected routes */}
            <Route
              path="/"
              element={<Navigate to="/sites" replace />}
            />

            <Route
              path="/sites"
              element={
                <ProtectedRoute>
                  <AccountSetupRequiredRoute>
                    <Sites />
                  </AccountSetupRequiredRoute>
                </ProtectedRoute>
              }
            />

            <Route
              path="/imports"
              element={
                <ProtectedRoute>
                  <AccountSetupRequiredRoute>
                    <Imports />
                  </AccountSetupRequiredRoute>
                </ProtectedRoute>
              }
            />

            <Route
              path="/admin/users"
              element={
                <ProtectedRoute>
                  <AccountSetupRequiredRoute>
                    <AdminUsers />
                  </AccountSetupRequiredRoute>
                </ProtectedRoute>
              }
            />

            <Route
              path="/admin/role-settings"
              element={
                <ProtectedRoute>
                  <AccountSetupRequiredRoute>
                    <RoleSettings />
                  </AccountSetupRequiredRoute>
                </ProtectedRoute>
              }
            />

            <Route path="/import/sites" element={<Navigate to="/imports" replace />} />

            <Route
              path="/profile"
              element={
                <ProtectedRoute>
                  <AccountSetupRequiredRoute>
                    <Profile />
                  </AccountSetupRequiredRoute>
                </ProtectedRoute>
              }
            />

            <Route
              path="/account-setup"
              element={
                <ProtectedRoute>
                  <AccountSetup />
                </ProtectedRoute>
              }
            />

            <Route
              path="/change-password"
              element={
                <ProtectedRoute>
                  <AccountSetupRequiredRoute>
                    <ChangePassword />
                  </AccountSetupRequiredRoute>
                </ProtectedRoute>
              }
            />

            {/* Catch-all redirect */}
            <Route path="*" element={<Navigate to="/sites" replace />} />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
      </LocalizationProvider>
    </ThemeProvider>
  );
};

export default App;
