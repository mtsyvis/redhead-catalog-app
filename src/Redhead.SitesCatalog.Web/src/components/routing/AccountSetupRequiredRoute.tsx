import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

export interface AccountSetupRequiredRouteProps {
  children: React.ReactNode;
}

/**
 * Route wrapper that redirects to account setup while mandatory setup is incomplete.
 * Used to protect normal app routes from users who must change password or complete profile.
 */
export const AccountSetupRequiredRoute: React.FC<AccountSetupRequiredRouteProps> = ({
  children,
}) => {
  const { user } = useAuth();
  const location = useLocation();

  if (
    (user?.mustChangePassword || user?.mustCompleteProfile) &&
    location.pathname !== '/account-setup'
  ) {
    return <Navigate to="/account-setup" replace state={{ from: location }} />;
  }

  return <>{children}</>;
};
