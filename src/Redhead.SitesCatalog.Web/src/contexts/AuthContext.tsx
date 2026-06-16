import React, { createContext, useContext, useState, useEffect, useCallback, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { authService } from '../services/auth.service';
import type { UserInfo, LoginRequest } from '../types/auth.types';
import { ApiClientError } from '../services/api.client';
import { registerSessionExpiredHandler, type SessionExpiredRoute } from '../services/sessionExpired';

interface AuthContextValue {
  user: UserInfo | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  sessionExpiredRedirect: SessionExpiredRoute | null;
  login: (credentials: LoginRequest) => Promise<UserInfo>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

/**
 * Auth Provider component
 * Manages authentication state and provides auth methods to the app
 */
export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const navigate = useNavigate();
  const [user, setUser] = useState<UserInfo | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [sessionExpiredRedirect, setSessionExpiredRedirect] =
    useState<SessionExpiredRoute | null>(null);

  useEffect(() => {
    return registerSessionExpiredHandler(({ from }) => {
      setSessionExpiredRedirect(from);
      setUser(null);
      navigate('/login', {
        replace: true,
        state: { from, sessionExpired: true },
      });
    });
  }, [navigate]);

  /**
   * Fetch current user on mount
   */
  const fetchCurrentUser = useCallback(async () => {
    try {
      const userData = await authService.getCurrentUser();
      setUser(userData);
    } catch (error) {
      // User not authenticated or session expired
      if (error instanceof ApiClientError && error.statusCode === 401) {
        setUser(null);
      } else {
        console.error('Failed to fetch current user:', error);
        setUser(null);
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchCurrentUser();
  }, [fetchCurrentUser]);

  /**
   * Login user. Returns user info so caller can redirect when setup is required.
   */
  const login = useCallback(async (credentials: LoginRequest): Promise<UserInfo> => {
    await authService.login(credentials);
    const userData = await authService.getCurrentUser();
    setSessionExpiredRedirect(null);
    setUser(userData);
    return userData;
  }, []);

  /**
   * Logout user
   */
  const logout = useCallback(async () => {
    try {
      await authService.logout();
    } finally {
      setSessionExpiredRedirect(null);
      setUser(null);
    }
  }, []);

  /**
   * Refresh user data (useful after password change)
   */
  const refreshUser = useCallback(async () => {
    await fetchCurrentUser();
  }, [fetchCurrentUser]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isLoading,
      isAuthenticated: !!user,
      sessionExpiredRedirect,
      login,
      logout,
      refreshUser,
    }),
    [user, isLoading, sessionExpiredRedirect, login, logout, refreshUser]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

/**
 * Hook to use auth context
 * @throws Error if used outside AuthProvider
 */
// eslint-disable-next-line react-refresh/only-export-components
export const useAuth = (): AuthContextValue => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return context;
};
