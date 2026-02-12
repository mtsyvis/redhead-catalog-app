/**
 * User information from /api/auth/me endpoint
 */
export interface UserInfo {
  id: string;
  email: string;
  mustChangePassword: boolean;
  isActive: boolean;
  roles: string[];
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
 * API error response
 */
export interface ApiError {
  message: string;
  errors?: string[];
}
