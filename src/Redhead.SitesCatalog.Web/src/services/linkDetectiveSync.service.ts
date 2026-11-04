import { ApiClient } from './api.client';
import type {
  LinkDetectiveSyncDetails,
  LinkDetectiveSyncRun,
  LinkDetectiveSyncStatus,
  SyncPage,
} from '../types/linkDetectiveSync.types';

const base = '/api/admin/linkdetective-sync';
export const linkDetectiveSyncService = {
  getStatus: () => ApiClient.get<LinkDetectiveSyncStatus>(`${base}/status`),
  getRuns: (page: number, pageSize: number) =>
    ApiClient.get<SyncPage<LinkDetectiveSyncRun>>(
      `${base}/runs?${new URLSearchParams({ page: String(page), pageSize: String(pageSize) })}`
    ),
  getRun: (id: string, page: number, pageSize: number, domain: string, status: string) => {
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (domain.trim()) query.set('domain', domain.trim());
    if (status !== '') query.set('status', status);
    return ApiClient.get<LinkDetectiveSyncDetails>(
      `${base}/runs/${encodeURIComponent(id)}?${query}`
    );
  },
};
