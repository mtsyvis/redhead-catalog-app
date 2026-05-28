import { useCallback, useEffect, useState } from 'react';

export const IMPORT_RESULT_TTL_MS = 30 * 60 * 1000;

export interface PersistedImportResult<TImportResult> {
  result: TImportResult;
  fileName: string;
  fileSize: number;
  completedAtUtc: string;
  expiresAtUtc: string;
}

export interface UseImportTabOptions {
  /**
   * Optional max file size in bytes. If provided, selecting a file larger than this
   * value will set an error message but still keep the file selected.
   */
  maxFileSizeBytes?: number;
  /**
   * Error message to use when the selected file exceeds the max size.
   */
  fileTooLargeMessage?: string;
  persistedStateKey?: string;
  persistedStateTtlMs?: number;
}

export interface UseImportTabState<TImportResult> {
  file: File | null;
  setFile: (file: File | null) => void;
  fileInputKey: number;
  loading: boolean;
  error: string | null;
  setError: (error: string | null) => void;
  result: TImportResult | null;
  persistedResult: PersistedImportResult<TImportResult> | null;
  setResult: (result: TImportResult | null) => void;
  clearImportState: () => void;
  handleFileChange: (event: React.ChangeEvent<HTMLInputElement>) => void;
  handleSubmit: (event: React.FormEvent<HTMLFormElement>) => Promise<void>;
}

function loadPersistedImportResult<TImportResult>(
  storageKey: string | undefined,
): PersistedImportResult<TImportResult> | null {
  if (!storageKey || typeof window === 'undefined') {
    return null;
  }

  try {
    const raw = window.localStorage.getItem(storageKey);
    if (!raw) {
      return null;
    }

    const parsed = JSON.parse(raw) as PersistedImportResult<TImportResult>;
    if (!parsed.result || !parsed.expiresAtUtc || Date.parse(parsed.expiresAtUtc) <= Date.now()) {
      removePersistedImportResult(storageKey);
      return null;
    }

    return parsed;
  } catch {
    removePersistedImportResult(storageKey);
    return null;
  }
}

function removePersistedImportResult(storageKey: string | undefined) {
  if (!storageKey || typeof window === 'undefined') {
    return;
  }

  try {
    window.localStorage.removeItem(storageKey);
  } catch {
    // Ignore storage cleanup failures; import state can still reset in memory.
  }
}

function savePersistedImportResult<TImportResult>(
  storageKey: string | undefined,
  persistedResult: PersistedImportResult<TImportResult>,
) {
  if (!storageKey || typeof window === 'undefined') {
    return;
  }

  try {
    window.localStorage.setItem(storageKey, JSON.stringify(persistedResult));
  } catch {
    // Import success should not become an error because browser storage is unavailable.
  }
}

export function useImportTab<TImportResult>(
  runImport: (file: File) => Promise<TImportResult>,
  options?: UseImportTabOptions,
): UseImportTabState<TImportResult> {
  const persistedStateKey = options?.persistedStateKey;
  const persistedStateTtlMs = options?.persistedStateTtlMs ?? IMPORT_RESULT_TTL_MS;
  const initialPersistedResult = () =>
    loadPersistedImportResult<TImportResult>(persistedStateKey);

  const [file, setFile] = useState<File | null>(null);
  const [fileInputKey, setFileInputKey] = useState(0);
  const [loading, setLoading] = useState(false);
  const [persistedResult, setPersistedResult] =
    useState<PersistedImportResult<TImportResult> | null>(initialPersistedResult);
  const [result, setResult] = useState<TImportResult | null>(
    () => initialPersistedResult()?.result ?? null,
  );
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const stored = loadPersistedImportResult<TImportResult>(persistedStateKey);
    setPersistedResult(stored);
    setResult(stored?.result ?? null);
    setFile(null);
    setError(null);
    setFileInputKey((current) => current + 1);
  }, [persistedStateKey]);

  useEffect(() => {
    if (!persistedResult) {
      return undefined;
    }

    const expiresAt = Date.parse(persistedResult.expiresAtUtc);
    const timeoutMs = expiresAt - Date.now();
    if (timeoutMs <= 0) {
      removePersistedImportResult(persistedStateKey);
      setPersistedResult(null);
      setResult(null);
      return undefined;
    }

    const timeoutId = window.setTimeout(() => {
      removePersistedImportResult(persistedStateKey);
      setPersistedResult(null);
      setResult(null);
    }, timeoutMs);

    return () => window.clearTimeout(timeoutId);
  }, [persistedResult, persistedStateKey]);

  const clearImportState = useCallback(() => {
    setFile(null);
    setLoading(false);
    setError(null);
    setResult(null);
    setPersistedResult(null);
    setFileInputKey((current) => current + 1);
    removePersistedImportResult(persistedStateKey);
  }, [persistedStateKey]);

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const chosen = event.target.files?.[0] ?? null;
    setFile(chosen);
    setResult(null);
    setPersistedResult(null);
    setError(null);
    removePersistedImportResult(persistedStateKey);
    event.target.value = '';

    if (
      chosen &&
      options?.maxFileSizeBytes !== undefined &&
      chosen.size > options.maxFileSizeBytes &&
      options.fileTooLargeMessage
    ) {
      setError(options.fileTooLargeMessage);
    }
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!file) {
      return;
    }

    setLoading(true);
    setError(null);
    setResult(null);

    try {
      const data = await runImport(file);
      const completedAtUtc = new Date().toISOString();
      const expiresAtUtc = new Date(Date.now() + persistedStateTtlMs).toISOString();
      const nextPersistedResult = {
        result: data,
        fileName: file.name,
        fileSize: file.size,
        completedAtUtc,
        expiresAtUtc,
      };

      setResult(data);
      setPersistedResult(nextPersistedResult);
      savePersistedImportResult(persistedStateKey, nextPersistedResult);
      setFile(null);
      setFileInputKey((current) => current + 1);
    } catch (err) {
      // Preserve existing error extraction behavior
      setError(err instanceof Error ? err.message : 'Import failed');
    } finally {
      setLoading(false);
    }
  };

  return {
    file,
    setFile,
    fileInputKey,
    loading,
    error,
    setError,
    result,
    persistedResult,
    setResult,
    clearImportState,
    handleFileChange,
    handleSubmit,
  };
}
