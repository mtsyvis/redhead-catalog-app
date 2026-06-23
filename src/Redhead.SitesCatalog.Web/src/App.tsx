import React, { Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Box, CircularProgress, ThemeProvider, CssBaseline } from '@mui/material';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { AuthProvider } from './contexts/AuthContext';
import { theme } from './theme/theme';
import { ProtectedRoute } from './components/routing/ProtectedRoute';
import { AccountSetupRequiredRoute } from './components/routing/AccountSetupRequiredRoute';
import { OAuthHome, PrivacyPolicy, TermsOfService } from './pages/OAuthVerificationPages';

const Login = React.lazy(() => import('./pages/Login').then((module) => ({ default: module.Login })));
const ActivateAccount = React.lazy(() =>
  import('./pages/ActivateAccount').then((module) => ({ default: module.ActivateAccount }))
);
const ChangePassword = React.lazy(() =>
  import('./pages/ChangePassword').then((module) => ({ default: module.ChangePassword }))
);
const AccountSetup = React.lazy(() =>
  import('./pages/AccountSetup').then((module) => ({ default: module.AccountSetup }))
);
const Profile = React.lazy(() => import('./pages/Profile').then((module) => ({ default: module.Profile })));
const Sites = React.lazy(() => import('./pages/Sites').then((module) => ({ default: module.Sites })));
const Imports = React.lazy(() => import('./pages/Imports').then((module) => ({ default: module.Imports })));
const AdminUsers = React.lazy(() =>
  import('./pages/AdminUsers').then((module) => ({ default: module.AdminUsers }))
);
const AdminUserDetails = React.lazy(() =>
  import('./pages/AdminUserDetails').then((module) => ({ default: module.AdminUserDetails }))
);
const RoleSettings = React.lazy(() =>
  import('./pages/RoleSettings').then((module) => ({ default: module.RoleSettings }))
);
const Analytics = React.lazy(() => import('./pages/Analytics').then((module) => ({ default: module.Analytics })));
const AhrefsSync = React.lazy(() =>
  import('./pages/AhrefsSync').then((module) => ({ default: module.AhrefsSync }))
);
const AhrefsSyncRunDetails = React.lazy(() =>
  import('./pages/AhrefsSyncRunDetails').then((module) => ({
    default: module.AhrefsSyncRunDetails,
  }))
);

const PageLoadingFallback: React.FC = () => (
  <Box
    sx={{
      alignItems: 'center',
      display: 'flex',
      minHeight: '100vh',
      justifyContent: 'center',
    }}
  >
    <CircularProgress size={32} />
  </Box>
);

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
          <Suspense fallback={<PageLoadingFallback />}>
            <Routes>
              {/* Public routes */}
              <Route path="/login" element={<Login />} />
              <Route path="/activate-account" element={<ActivateAccount />} />
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
                element={<Navigate to="/imports/sites-import" replace />}
              />

              <Route
                path="/imports/:importType"
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
                path="/admin/users/:userId"
                element={
                  <ProtectedRoute>
                    <AccountSetupRequiredRoute>
                      <AdminUserDetails />
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

              <Route
                path="/admin/analytics"
                element={
                  <ProtectedRoute>
                    <AccountSetupRequiredRoute>
                      <Analytics />
                    </AccountSetupRequiredRoute>
                  </ProtectedRoute>
                }
              />

              <Route
                path="/admin/ahrefs-sync"
                element={
                  <ProtectedRoute>
                    <AccountSetupRequiredRoute>
                      <AhrefsSync />
                    </AccountSetupRequiredRoute>
                  </ProtectedRoute>
                }
              />

              <Route
                path="/admin/ahrefs-sync/runs/:id"
                element={
                  <ProtectedRoute>
                    <AccountSetupRequiredRoute>
                      <AhrefsSyncRunDetails />
                    </AccountSetupRequiredRoute>
                  </ProtectedRoute>
                }
              />

              <Route path="/import/sites" element={<Navigate to="/imports/sites-import" replace />} />

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
          </Suspense>
        </AuthProvider>
      </BrowserRouter>
      </LocalizationProvider>
    </ThemeProvider>
  );
};

export default App;
