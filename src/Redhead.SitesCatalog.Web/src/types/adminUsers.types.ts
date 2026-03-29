import type { ExportLimitMode } from '../utils/exportLimit';

export interface UserListItem {
  id: string;
  email: string;
  role: string;
  isActive: boolean;
  exportLimitOverrideMode: ExportLimitMode | null;
  exportLimitRowsOverride: number | null;
  effectiveExportLimitMode: ExportLimitMode;
  effectiveExportLimitRows: number | null;
  isExportLimitOverridden: boolean;
  isExportLimitEditable: boolean;
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

export interface UpdateExportLimitRequest {
  overrideMode: ExportLimitMode | null;
  overrideRows: number | null;
}

export const ROLES = ['SuperAdmin', 'Admin', 'Internal', 'Client'] as const;
export type Role = (typeof ROLES)[number];
