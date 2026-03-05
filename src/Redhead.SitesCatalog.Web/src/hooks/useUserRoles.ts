import { useAuth } from '../contexts/AuthContext';

export function useUserRoles() {
  const { user } = useAuth();
  const roles = user?.roles ?? [];

  const hasRole = (role: string) => roles.includes(role);
  const hasAnyRole = (candidates: string[]) => candidates.some((role) => roles.includes(role));

  const isSuperAdmin = hasRole('SuperAdmin');
  const isAdmin = hasAnyRole(['Admin', 'SuperAdmin']);
  const isInternal = hasRole('Internal');
  const isClient = hasRole('Client');

  return {
    roles,
    isSuperAdmin,
    isAdmin,
    isInternal,
    isClient,
    hasRole,
    hasAnyRole,
  };
}

