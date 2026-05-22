import type { GoogleDriveStatus } from './googleDrive.types';
import type { ExportLimitMode } from '../utils/exportLimit';

/**
 * User information from /api/auth/me endpoint
 */
export interface UserInfo {
  id: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  displayName: string;
  mustCompleteProfile: boolean;
  mustChangePassword: boolean;
  isActive: boolean;
  roles: string[];
  isExportDisabled: boolean;
}

/**
 * Login request payload
 */
export interface LoginRequest {
  email: string;
  password: string;
  rememberMe: boolean;
}

/**
 * Login response from API
 */
export interface LoginResponse {
  email: string;
  mustChangePassword: boolean;
  mustCompleteProfile: boolean;
  firstName: string | null;
  lastName: string | null;
  displayName: string;
  roles: string[];
}

/**
 * Change password request payload
 */
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

/**
 * Complete account setup request payload
 */
export interface CompleteAccountSetupRequest {
  currentPassword?: string | null;
  newPassword?: string | null;
  firstName?: string | null;
  lastName?: string | null;
}

export type CompleteAccountSetupResponse = Omit<UserInfo, 'id' | 'isActive' | 'isExportDisabled'>;

/**
 * Current user's self-service profile
 */
export interface CurrentUserProfile {
  email: string;
  role: string;
  firstName: string | null;
  lastName: string | null;
  displayName: string;
  mustCompleteProfile: boolean;
  googleDrive: GoogleDriveStatus;
  limits: CurrentUserProfileLimits | null;
}

export interface CurrentUserProfileLimits {
  exportLimitMode: ExportLimitMode;
  exportLimitRows: number | null;
  isUnlimited: boolean;
}

export interface UpdateCurrentUserProfileRequest {
  firstName: string;
  lastName: string;
}

/**
 * API error response
 */
export interface ApiError {
  message: string;
  title?: string;
  detail?: string;
  errors?: string[] | Record<string, string[]>;
  fieldErrors?: Record<string, string[]>;
}
