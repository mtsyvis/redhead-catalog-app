import { notifySessionExpired } from './sessionExpired';
import { sitesService } from './sites.service';

export interface ImportDownloadInfo {
  available: boolean;
  token: string;
  fileName?: string;
}

export interface ImportDownloadsInfo {
  invalidRows?: ImportDownloadInfo | null;
  unmatchedRows?: ImportDownloadInfo | null;
  warningRows?: ImportDownloadInfo | null;
}

export interface ImportResultBase {
  invalidRowsCount?: number;
  savedWithWarningsCount?: number;
  downloads?: ImportDownloadsInfo;
}

export interface DuplicateDomainsImportResult {
  duplicateDomainsCount?: number;
  duplicateDomainsPreview?: string[];
}

export interface SitesImportResult extends ImportResultBase, DuplicateDomainsImportResult {
  insertedCount?: number;
  skippedExistingCount?: number;
}

export interface UpdateImportResult extends ImportResultBase, DuplicateDomainsImportResult {
  updatedCount?: number;
  unmatchedRowsCount?: number;
  metricSnapshotsSavedCount?: number | null;
  metricSnapshotDate?: string | null;
  metricHistorySkippedReason?: string | null;
}

export type AvailabilityImportAction = 'markUnavailable' | 'restoreAvailable';

/** Maximum file size for sites import (50 MB) */
export const MAX_IMPORT_FILE_SIZE_BYTES = 50 * 1024 * 1024;

export const FILE_TOO_LARGE_MESSAGE = 'File is too large. Maximum size is 50 MB.';

export const ACCEPT_FILES = '.csv';

async function runImportRequest<T>(
  endpoint: string,
  file: File,
  fields?: Readonly<Record<string, string>>,
): Promise<T> {
  if (file.size > MAX_IMPORT_FILE_SIZE_BYTES) {
    throw new Error(FILE_TOO_LARGE_MESSAGE);
  }

  const formData = new FormData();
  formData.append('file', file);
  Object.entries(fields ?? {}).forEach(([key, value]) => {
    formData.append(key, value);
  });

  const baseUrl = import.meta.env.DEV ? '' : (import.meta.env.VITE_API_URL || '');
  const response = await fetch(`${baseUrl}${endpoint}`, {
    method: 'POST',
    credentials: 'include',
    body: formData,
  });

  if (!response.ok) {
    if (response.status === 401) {
      notifySessionExpired();
    }

    const err = await response.json().catch(() => ({ message: 'Import failed' }));
    throw new Error(err.message || err.error || 'Import failed');
  }

  const result = await response.json();
  sitesService.clearFilterOptionsCache();

  return result;
}

/**
 * Import sites from CSV
 */
export function importSites(file: File) {
  return runImportRequest<SitesImportResult>('/api/import/sites', file);
}

/**
 * Mass-update existing sites from CSV. Domain is lookup key; included update columns are applied.
 */
export function importSitesUpdate(file: File, snapshotDate?: string) {
  return runImportRequest<UpdateImportResult>(
    '/api/import/sites-update',
    file,
    snapshotDate ? { snapshotDate } : undefined,
  );
}

/**
 * Import availability from CSV. Updates existing sites by exact normalized domain match.
 */
export function importAvailability(file: File, action: AvailabilityImportAction) {
  return runImportRequest<UpdateImportResult>('/api/imports/availability', file, { action });
}

/**
 * Import Last Published Date from CSV (Domain, LastPublishedDate). Updates existing sites by exact normalized domain match.
 */
export function importLastPublished(file: File) {
  return runImportRequest<UpdateImportResult>('/api/import/last-published', file);
}

export async function downloadImportArtifactCsv(token: string, fallbackFileName: string): Promise<void> {
  const baseUrl = import.meta.env.DEV ? '' : (import.meta.env.VITE_API_URL || '');
  const response = await fetch(`${baseUrl}/api/imports/downloads/${encodeURIComponent(token)}`, {
    method: 'GET',
    credentials: 'include',
  });

  if (!response.ok) {
    if (response.status === 401) {
      notifySessionExpired();
    }

    const err = await response.json().catch(() => ({ message: 'Download failed' }));
    throw new Error(err.message || err.error || 'Download failed');
  }

  const blob = await response.blob();
  const downloadUrl = window.URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = downloadUrl;
  link.download = fallbackFileName || 'invalid-rows.csv';
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  window.URL.revokeObjectURL(downloadUrl);
}
