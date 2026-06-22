import { ApiClient } from './api.client';
import type {
  AhrefsSyncRunDetails,
  AhrefsSyncRunsPage,
  AhrefsSyncStatus,
} from '../types/ahrefsSync.types';

export const ahrefsSyncService = {
  getStatus(refresh = false): Promise<AhrefsSyncStatus> {
    return ApiClient.get<AhrefsSyncStatus>(
      `/api/admin/ahrefs-sync/status?refresh=${refresh}`
    );
  },

  listRuns(page: number, pageSize: number): Promise<AhrefsSyncRunsPage> {
    const query = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
    });
    return ApiClient.get<AhrefsSyncRunsPage>(
      `/api/admin/ahrefs-sync/runs?${query.toString()}`
    );
  },

  getRun(id: string, page: number, pageSize: number): Promise<AhrefsSyncRunDetails> {
    const query = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
    });
    return ApiClient.get<AhrefsSyncRunDetails>(
      `/api/admin/ahrefs-sync/runs/${encodeURIComponent(id)}?${query.toString()}`
    );
  },
};
