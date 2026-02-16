export interface SitesImportError {
  rowNumber: number;
  message: string;
}

export interface SitesImportResult {
  inserted: number;
  duplicatesCount: number;
  duplicates: string[];
  errorsCount: number;
  errors: SitesImportError[];
}

/** Maximum file size for sites import (50 MB) */
export const MAX_IMPORT_FILE_SIZE_BYTES = 50 * 1024 * 1024;

export const FILE_TOO_LARGE_MESSAGE = 'File is too large. Maximum size is 50 MB.';

/**
 * Import sites from CSV (add-only)
 */
export async function importSites(file: File): Promise<SitesImportResult> {
  if (file.size > MAX_IMPORT_FILE_SIZE_BYTES) {
    throw new Error(FILE_TOO_LARGE_MESSAGE);
  }

  const formData = new FormData();
  formData.append('file', file);

  const baseUrl = import.meta.env.DEV ? '' : (import.meta.env.VITE_API_URL || '');
  const response = await fetch(`${baseUrl}/api/import/sites`, {
    method: 'POST',
    credentials: 'include',
    body: formData,
  });

  if (!response.ok) {
    const err = await response.json().catch(() => ({ message: 'Import failed' }));
    throw new Error(err.message || err.error || 'Import failed');
  }

  return response.json();
}
