import { ApiClient } from './api.client';
import type {
  UserListQueryParams,
  UserListResponse,
  AdminUserDetails,
  CreateUserRequest,
  CreateUserResponse,
  ResetPasswordResponse,
  UpdateExportLimitRequest,
  UpdateSuperAdminNoteRequest,
} from '../types/adminUsers.types';

export const adminUsersService = {
  list(params: UserListQueryParams): Promise<UserListResponse> {
    const query = new URLSearchParams({
      userType: params.userType,
      page: String(params.page),
      pageSize: String(params.pageSize),
    });

    return ApiClient.get<UserListResponse>(`/api/admin/users?${query.toString()}`);
  },

  getDetails(id: string): Promise<AdminUserDetails> {
    return ApiClient.get<AdminUserDetails>(`/api/admin/users/${encodeURIComponent(id)}`);
  },

  create(data: CreateUserRequest): Promise<CreateUserResponse> {
    return ApiClient.post<CreateUserResponse, CreateUserRequest>('/api/admin/users', data);
  },

  resetPassword(id: string): Promise<ResetPasswordResponse> {
    return ApiClient.post<ResetPasswordResponse>(`/api/admin/users/${id}/reset-password`);
  },

  disable(id: string): Promise<{ message: string }> {
    return ApiClient.post<{ message: string }>(`/api/admin/users/${id}/disable`);
  },

  updateExportLimit(id: string, data: UpdateExportLimitRequest): Promise<void> {
    return ApiClient.put<void, UpdateExportLimitRequest>(`/api/admin/users/${id}/export-limit`, data);
  },

  updateSuperAdminNote(id: string, data: UpdateSuperAdminNoteRequest): Promise<void> {
    return ApiClient.put<void, UpdateSuperAdminNoteRequest>(`/api/admin/users/${id}/super-admin-note`, data);
  },
};
