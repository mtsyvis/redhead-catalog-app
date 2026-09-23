import { ApiClient } from './api.client';
import type {
  ApplicationSettingsLimits,
  ClientCatalogProtectionSettings,
  ClientCatalogProtectionSettingsUpdate,
} from '../types/applicationSettings.types';

export const applicationSettingsService = {
  getLimits(): Promise<ApplicationSettingsLimits> {
    return ApiClient.get<ApplicationSettingsLimits>('/api/admin/application-settings/limits');
  },

  getClientProtection(): Promise<ClientCatalogProtectionSettings> {
    return ApiClient.get<ClientCatalogProtectionSettings>('/api/admin/client-catalog-protection');
  },

  updateClientProtection(
    settings: ClientCatalogProtectionSettingsUpdate
  ): Promise<ClientCatalogProtectionSettings> {
    return ApiClient.put<ClientCatalogProtectionSettings, ClientCatalogProtectionSettingsUpdate>(
      '/api/admin/client-catalog-protection',
      settings
    );
  },
};
