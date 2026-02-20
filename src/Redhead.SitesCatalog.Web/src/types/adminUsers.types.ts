export interface UserListItem {
  id: string;
  email: string;
  role: string;
  isActive: boolean;
}

export interface CreateUserRequest {
  email: string;
  role: string;
}

export interface CreateUserResponse {
  id: string;
  email: string;
  role: string;
  temporaryPassword: string;
}

export interface ResetPasswordResponse {
  temporaryPassword: string;
}

export const ROLES = ['SuperAdmin', 'Admin', 'Internal', 'Client'] as const;
export type Role = (typeof ROLES)[number];
