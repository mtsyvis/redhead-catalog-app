import { Box, Stack, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material';
import type { MouseEvent } from 'react';
import { useCallback, useState } from 'react';
import {
  ACCEPT_FILES,
  FILE_TOO_LARGE_MESSAGE,
  importAvailability,
  MAX_IMPORT_FILE_SIZE_BYTES,
  type AvailabilityImportAction,
  type UpdateImportResult,
} from '../../services/import.service';
import { AVAILABILITY_IMPORT_INSTRUCTIONS } from '../../constants/imports.constants';
import { useImportTab } from '../../hooks/useImportTab';
import { ImportInstructionsCard } from './ImportInstructionsCard';
import { ImportTabContent } from './ImportTabContent';
import { ImportUploadSection } from './ImportUploadSection';
import { UpdateImportResultCard } from './UpdateImportResultCard';

const DEFAULT_ACTION: AvailabilityImportAction = 'markUnavailable';
const DEFAULT_ACTION_LABEL = 'Mark as unavailable';

const ACTION_OPTIONS: ReadonlyArray<{
  value: AvailabilityImportAction;
  label: string;
}> = [
  { value: 'markUnavailable', label: 'Mark as unavailable' },
  { value: 'restoreAvailable', label: 'Restore as available' },
];

type AvailabilityImportTabProps = {
  readonly persistedStateKey: string;
};

export function AvailabilityImportTab({ persistedStateKey }: AvailabilityImportTabProps) {
  const [action, setAction] = useState<AvailabilityImportAction>(DEFAULT_ACTION);

  const runImport = useCallback(
    (file: File) => importAvailability(file, action),
    [action],
  );

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

  const selectedInstructions = AVAILABILITY_IMPORT_INSTRUCTIONS[action];
  const selectedActionLabel =
    ACTION_OPTIONS.find((option) => option.value === action)?.label ?? DEFAULT_ACTION_LABEL;

  const handleActionChange = (_event: MouseEvent<HTMLElement>, nextAction: AvailabilityImportAction | null) => {
    if (nextAction === null || nextAction === action) {
      return;
    }

    setAction(nextAction);
    clearImportState();
  };

  const handleStartNewImport = () => {
    setAction(DEFAULT_ACTION);
    clearImportState();
  };

  return (
    <ImportTabContent
      instructions={
        <Stack spacing={2}>
          <Box>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              Operation
            </Typography>
            <ToggleButtonGroup
              exclusive
              size="small"
              value={action}
              onChange={handleActionChange}
              aria-label="Availability import operation"
              sx={{
                flexWrap: 'wrap',
                borderRadius: 999,
                bgcolor: 'background.paper',
                '& .MuiToggleButton-root': {
                  px: 1.75,
                  py: 0.75,
                  minHeight: 38,
                  textTransform: 'none',
                  borderColor: 'divider',
                  color: 'text.primary',
                  bgcolor: 'background.paper',
                  lineHeight: 1.2,
                  '&:first-of-type': {
                    borderTopLeftRadius: 999,
                    borderBottomLeftRadius: 999,
                  },
                  '&:last-of-type': {
                    borderTopRightRadius: 999,
                    borderBottomRightRadius: 999,
                  },
                  '&.Mui-selected': {
                    color: 'primary.main',
                    bgcolor: 'rgba(255, 69, 91, 0.08)',
                    borderColor: 'rgba(255, 69, 91, 0.35)',
                    fontWeight: 600,
                    '&:hover': {
                      bgcolor: 'rgba(255, 69, 91, 0.12)',
                    },
                  },
                  '&:not(.Mui-selected)': {
                    '&:hover': {
                      bgcolor: 'action.hover',
                    },
                  },
                },
              }}
            >
              {ACTION_OPTIONS.map((option) => (
                <ToggleButton key={option.value} value={option.value}>
                  {option.label}
                </ToggleButton>
              ))}
            </ToggleButtonGroup>
          </Box>

          <ImportInstructionsCard
            description={selectedInstructions.description}
            requiredColumns={selectedInstructions.requiredColumns}
            optionalNote={selectedInstructions.optionalNote}
          />
        </Stack>
      }
      uploadSection={
        <ImportUploadSection
          file={file}
          fileInputKey={fileInputKey}
          loading={loading}
          accept={ACCEPT_FILES}
          maxFileSizeBytes={MAX_IMPORT_FILE_SIZE_BYTES}
          onFileChange={handleFileChange}
          onSubmit={handleSubmit}
          submitLabel={selectedActionLabel}
        />
      }
      error={error}
      onClearError={() => setError(null)}
      result={
        result ? (
          <UpdateImportResultCard
            title="Availability import result"
            result={result}
            fileName={persistedResult?.fileName}
            fileSize={persistedResult?.fileSize}
            completedAtUtc={persistedResult?.completedAtUtc}
            onStartNewImport={handleStartNewImport}
          />
        ) : null
      }
    />
  );
}
