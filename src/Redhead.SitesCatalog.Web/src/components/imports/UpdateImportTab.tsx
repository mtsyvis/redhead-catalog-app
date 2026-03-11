import type { UpdateImportResult } from "../../services/import.service";
import { useImportTab } from "../../hooks/useImportTab";
import { ImportTabContent } from "./ImportTabContent";
import { ImportInstructionsCard } from "./ImportInstructionsCard";
import { ImportUploadSection } from "./ImportUploadSection";
import { UpdateImportResultCard } from "./UpdateImportResultCard";
import { MAX_IMPORT_FILE_SIZE_BYTES, FILE_TOO_LARGE_MESSAGE, ACCEPT_FILES } from "../../services/import.service";

type UpdateImportTabProps = {
  runImport: (file: File) => Promise<UpdateImportResult>;
  instructions: {
    description: React.ReactNode;
    requiredColumns: readonly string[];
    optionalNote?: React.ReactNode;
  };
  resultTitle: string;
};

export function UpdateImportTab({ runImport, instructions, resultTitle }: UpdateImportTabProps) {
  const {
    file,
    loading,
    error,
    result,
    setError,
    handleFileChange,
    handleSubmit,
  } = useImportTab<UpdateImportResult>(runImport, {
    maxFileSizeBytes: MAX_IMPORT_FILE_SIZE_BYTES,
    fileTooLargeMessage: FILE_TOO_LARGE_MESSAGE,
  });

  return (
    <ImportTabContent
      instructions={
        <ImportInstructionsCard
          description={instructions.description}
          requiredColumns={instructions.requiredColumns}
          optionalNote={instructions.optionalNote}
        />
      }
      uploadSection={
        <ImportUploadSection
          file={file}
          loading={loading}
          accept={ACCEPT_FILES}
          maxFileSizeBytes={MAX_IMPORT_FILE_SIZE_BYTES}
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
            unmatchedTitle="Unmatched domains"
            result={result}
          />
        ) : null
      }
    />
  );
}
