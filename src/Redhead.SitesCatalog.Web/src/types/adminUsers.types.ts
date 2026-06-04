import type { ExportLimitMode } from '../utils/exportLimit';
import type { GoogleDriveStatus } from './googleDrive.types';

export interface UserListItem {
  id: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  displayName: string;
  mustCompleteProfile: boolean;
  role: string;
  isActive: boolean;
  exportLimitOverrideMode: ExportLimitMode | null;
  exportLimitRowsOverride: number | null;
  effectiveExportLimitMode: ExportLimitMode;
  effectiveExportLimitRows: number | null;
  isExportLimitOverridden: boolean;
  isExportLimitEditable: boolean;
  superAdminNote?: string | null;
}

export type UserTypeFilter = 'all' | 'internal' | 'clients';

export interface UserListQueryParams {
  userType: UserTypeFilter;
  page: number;
  pageSize: number;
}

export interface UserListResponse {
  items: UserListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface AdminUserDetails {
  id: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  displayName: string;
  mustCompleteProfile: boolean;
  mustChangePassword: boolean;
  role: string;
  isActive: boolean;
  exportLimitOverrideMode: ExportLimitMode | null;
  exportLimitRowsOverride: number | null;
  effectiveExportLimitMode: ExportLimitMode | null;
  effectiveExportLimitRows: number | null;
  isExportLimitOverridden: boolean;
  isExportLimitEditable: boolean;
  googleDriveConnected: boolean;
  googleDrive: GoogleDriveStatus;
  superAdminNote?: string | null;
}

export interface CreateUserRequest {
  email: string;
  role: string;
  superAdminNote?: string | null;
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

export interface UpdateUserRoleRequest {
  role: string;
}

export interface ReactivateUserRequest {
  role: string;
}

export interface ReactivateUserResponse {
  temporaryPassword: string;
}

export interface UpdateExportLimitRequest {
  overrideMode: ExportLimitMode | null;
  overrideRows: number | null;
}

export interface UpdateSuperAdminNoteRequest {
  superAdminNote: string | null;
}

export const ROLES = ['SuperAdmin', 'Admin', 'Internal', 'Client'] as const;
export type Role = (typeof ROLES)[number];
export const NON_SUPER_ADMIN_ROLES = ['Admin', 'Internal', 'Client'] as const;
export type NonSuperAdminRole = (typeof NON_SUPER_ADMIN_ROLES)[number];
