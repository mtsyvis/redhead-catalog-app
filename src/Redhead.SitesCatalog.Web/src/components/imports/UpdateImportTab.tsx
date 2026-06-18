import type React from 'react';
import type { UpdateImportResult } from '../../services/import.service';
import { useImportTab } from '../../hooks/useImportTab';
import { ImportTabContent } from './ImportTabContent';
import { ImportInstructionsPanel } from './ImportInstructionsPanel';
import { ImportUploadSection } from './ImportUploadSection';
import { UpdateImportResultCard } from './UpdateImportResultCard';
import {
  MAX_IMPORT_FILE_SIZE_BYTES,
  FILE_TOO_LARGE_MESSAGE,
  ACCEPT_FILES,
} from '../../services/import.service';

type UpdateImportTabProps = {
  readonly runImport: (file: File) => Promise<UpdateImportResult>;
  readonly instructions?: {
    readonly title: React.ReactNode;
    readonly description: React.ReactNode;
    readonly requiredColumns: readonly string[];
    readonly requiredColumnsNote?: React.ReactNode;
    readonly rules?: readonly React.ReactNode[];
    readonly examples?: readonly { title: string; csv: string; note?: React.ReactNode }[];
    readonly exampleDownload?: {
      readonly fileName: string;
      readonly csv: string;
    };
  };
  readonly instructionsContent?: React.ReactNode;
  readonly uploadHelper?: React.ReactNode;
  readonly resultTitle: string;
  readonly persistedStateKey: string;
};

export function UpdateImportTab({
  runImport,
  instructions,
  instructionsContent,
  uploadHelper,
  resultTitle,
  persistedStateKey,
}: UpdateImportTabProps) {
  const {
    file,
    fileInputKey,
    loading,
    error,
    result,
    persistedResult,
    setError,
    clearImportState,
    handleFileChange,
    handleSubmit,
  } = useImportTab<UpdateImportResult>(runImport, {
    maxFileSizeBytes: MAX_IMPORT_FILE_SIZE_BYTES,
    fileTooLargeMessage: FILE_TOO_LARGE_MESSAGE,
    persistedStateKey,
  });

  return (
    <ImportTabContent
      instructions={
        instructionsContent ?? (instructions ? (
          <ImportInstructionsPanel
            title={instructions.title}
            description={instructions.description}
            requiredColumns={instructions.requiredColumns}
            requiredColumnsNote={instructions.requiredColumnsNote}
            rules={instructions.rules}
            examples={instructions.examples}
            exampleDownload={instructions.exampleDownload}
          />
        ) : null)
      }
      uploadSection={
        <ImportUploadSection
          file={file}
          fileInputKey={fileInputKey}
          loading={loading}
          accept={ACCEPT_FILES}
          maxFileSizeBytes={MAX_IMPORT_FILE_SIZE_BYTES}
          helperContent={uploadHelper}
          onFileChange={handleFileChange}
          onSubmit={handleSubmit}
        />
      }
      error={error}
      onClearError={() => setError(null)}
      result={
        result ? (
          <UpdateImportResultCard
            title={resultTitle}
            result={result}
            fileName={persistedResult?.fileName}
            fileSize={persistedResult?.fileSize}
            completedAtUtc={persistedResult?.completedAtUtc}
            onStartNewImport={clearImportState}
          />
        ) : null
      }
    />
  );
}
