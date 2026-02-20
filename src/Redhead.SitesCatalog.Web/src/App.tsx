import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ThemeProvider, CssBaseline } from '@mui/material';
import { AuthProvider } from './contexts/AuthContext';
import { theme } from './theme/theme';
import { ProtectedRoute } from './components/routing/ProtectedRoute';
import { MustChangePasswordRoute } from './components/routing/MustChangePasswordRoute';
import { Login } from './pages/Login';
import { ChangePassword } from './pages/ChangePassword';
import { Home } from './pages/Home';
import { Sites } from './pages/Sites';
import { Imports } from './pages/Imports';
import { AdminUsers } from './pages/AdminUsers';
import { RoleSettings } from './pages/RoleSettings';

/**
 * Main App component
 */
const App: React.FC = () => {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            {/* Public route */}
            <Route path="/login" element={<Login />} />

            {/* Protected routes */}
            <Route
              path="/"
              element={<Navigate to="/sites" replace />}
            />

            <Route
              path="/sites"
              element={
                <ProtectedRoute>
                  <MustChangePasswordRoute>
                    <Sites />
                  </MustChangePasswordRoute>
                </ProtectedRoute>
              }
            />

            <Route
              path="/dashboard"
              element={
                <ProtectedRoute>
                  <MustChangePasswordRoute>
                    <Home />
                  </MustChangePasswordRoute>
                </ProtectedRoute>
              }
            />

            <Route
              path="/imports"
              element={
                <ProtectedRoute>
                  <MustChangePasswordRoute>
                    <Imports />
                  </MustChangePasswordRoute>
                </ProtectedRoute>
              }
            />

            <Route
              path="/admin/users"
              element={
                <ProtectedRoute>
                  <MustChangePasswordRoute>
                    <AdminUsers />
                  </MustChangePasswordRoute>
                </ProtectedRoute>
              }
            />

            <Route
              path="/admin/role-settings"
              element={
                <ProtectedRoute>
                  <MustChangePasswordRoute>
                    <RoleSettings />
                  </MustChangePasswordRoute>
                </ProtectedRoute>
              }
            />

            <Route path="/import/sites" element={<Navigate to="/imports" replace />} />

            <Route
              path="/change-password"
              element={
                <ProtectedRoute>
                  <ChangePassword />
                </ProtectedRoute>
              }
            />

            {/* Catch-all redirect */}
            <Route path="*" element={<Navigate to="/sites" replace />} />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </ThemeProvider>
  );
};

export default App;
