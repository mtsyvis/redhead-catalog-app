import { ApiClient } from './api.client';
import type {
  UserListItem,
  CreateUserRequest,
  CreateUserResponse,
  ResetPasswordResponse,
  UpdateExportLimitRequest,
} from '../types/adminUsers.types';

export const adminUsersService = {
  list(): Promise<UserListItem[]> {
    return ApiClient.get<UserListItem[]>('/api/admin/users');
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
};
