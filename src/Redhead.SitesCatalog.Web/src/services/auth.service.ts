import { ApiClient } from './api.client';
import type {
  LoginRequest,
  LoginResponse,
  UserInfo,
  ChangePasswordRequest,
  CompleteAccountSetupRequest,
  CompleteAccountSetupResponse,
  CurrentUserProfile,
  UpdateCurrentUserProfileRequest,
} from '../types/auth.types';

/**
 * Authentication service for API calls
 */
export const authService = {
  /**
   * Login with email and password
   */
  async login(credentials: LoginRequest): Promise<LoginResponse> {
    return ApiClient.post<LoginResponse, LoginRequest>('/api/auth/login', credentials);
  },

  /**
   * Logout current user
   */
  async logout(): Promise<void> {
    await ApiClient.post<void>('/api/auth/logout');
  },

  /**
   * Get current user information
   */
  async getCurrentUser(): Promise<UserInfo> {
    return ApiClient.get<UserInfo>('/api/auth/me');
  },

  /**
   * Change password
   */
  async changePassword(data: ChangePasswordRequest): Promise<void> {
    await ApiClient.post<void, ChangePasswordRequest>('/api/auth/change-password', data);
  },

  /**
   * Complete mandatory first-login account setup
   */
  async completeAccountSetup(data: CompleteAccountSetupRequest): Promise<CompleteAccountSetupResponse> {
    return ApiClient.post<CompleteAccountSetupResponse, CompleteAccountSetupRequest>(
      '/api/auth/complete-account-setup',
      data
    );
  },

  /**
   * Get current user's self-service profile
   */
  async getCurrentProfile(): Promise<CurrentUserProfile> {
    return ApiClient.get<CurrentUserProfile>('/api/profile');
  },

  /**
   * Update current user's self-service profile
   */
  async updateCurrentProfile(data: UpdateCurrentUserProfileRequest): Promise<CurrentUserProfile> {
    return ApiClient.put<CurrentUserProfile, UpdateCurrentUserProfileRequest>('/api/profile', data);
  },
};
