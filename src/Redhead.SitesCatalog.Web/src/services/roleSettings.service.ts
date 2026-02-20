import { ApiClient } from './api.client';
import type { RoleSettingItem, RoleSettingUpdateItem } from '../types/roleSettings.types';

export const roleSettingsService = {
  list(): Promise<RoleSettingItem[]> {
    return ApiClient.get<RoleSettingItem[]>('/api/admin/role-settings');
  },

  update(items: RoleSettingUpdateItem[]): Promise<void> {
    return ApiClient.put<void, RoleSettingUpdateItem[]>('/api/admin/role-settings', items);
  },
};
