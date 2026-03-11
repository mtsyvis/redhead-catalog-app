import { useState } from 'react';

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
}

export interface UseImportTabState<TImportResult> {
  file: File | null;
  setFile: (file: File | null) => void;
  loading: boolean;
  error: string | null;
  setError: (error: string | null) => void;
  result: TImportResult | null;
  setResult: (result: TImportResult | null) => void;
  handleFileChange: (event: React.ChangeEvent<HTMLInputElement>) => void;
  handleSubmit: (event: React.FormEvent<HTMLFormElement>) => Promise<void>;
}

export function useImportTab<TImportResult>(
  runImport: (file: File) => Promise<TImportResult>,
  options?: UseImportTabOptions,
): UseImportTabState<TImportResult> {
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<TImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const chosen = event.target.files?.[0] ?? null;
    setFile(chosen);
    setResult(null);
    setError(null);

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
      setResult(data);
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
    loading,
    error,
    setError,
    result,
    setResult,
    handleFileChange,
    handleSubmit,
  };
}

