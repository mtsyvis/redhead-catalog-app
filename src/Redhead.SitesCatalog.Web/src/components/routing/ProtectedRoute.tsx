import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { Box, CircularProgress } from '@mui/material';
import { useAuth } from '../../contexts/AuthContext';

export interface ProtectedRouteProps {
  children: React.ReactNode;
}

/**
 * Protected route component
 * Redirects to login if not authenticated
 * Shows loading state while checking auth
 */
export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ children }) => {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return (
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          minHeight: '100vh',
        }}
      >
        <CircularProgress />
      </Box>
    );
  }

  if (!isAuthenticated) {
    // Redirect to login, preserving the location they tried to visit
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <>{children}</>;
};
