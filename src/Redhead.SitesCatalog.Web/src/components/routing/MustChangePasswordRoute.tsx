import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

export interface MustChangePasswordRouteProps {
  children: React.ReactNode;
}

/**
 * Route wrapper that redirects to change password if mustChangePassword is true
 * Used to protect routes from users who must change their password
 */
export const MustChangePasswordRoute: React.FC<MustChangePasswordRouteProps> = ({
  children,
}) => {
  const { user } = useAuth();
  const location = useLocation();

  // If user must change password and not already on change password page
  if (user?.mustChangePassword && location.pathname !== '/change-password') {
    return <Navigate to="/change-password" replace />;
  }

  return <>{children}</>;
};
