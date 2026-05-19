import { apiClient } from './api.client';
import type {
  GoogleDriveConnectStartResponse,
  GoogleDriveStatus,
} from '../types/googleDrive.types';

class GoogleDriveService {
  private readonly baseUrl = '/api/integrations/google-drive';

  getStatus(): Promise<GoogleDriveStatus> {
    return apiClient.get<GoogleDriveStatus>(`${this.baseUrl}/status`);
  }

  startConnect(): Promise<GoogleDriveConnectStartResponse> {
    return apiClient.post<GoogleDriveConnectStartResponse>(`${this.baseUrl}/connect/start`);
  }

  disconnect(): Promise<void> {
    return apiClient.post<void>(`${this.baseUrl}/disconnect`);
  }
}

export const googleDriveService = new GoogleDriveService();
