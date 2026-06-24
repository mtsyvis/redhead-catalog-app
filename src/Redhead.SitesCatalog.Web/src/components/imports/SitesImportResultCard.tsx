import { Alert, Box, Paper, Stack, Typography } from '@mui/material';
import { useState } from 'react';
import { downloadImportArtifactCsv, type SitesImportResult } from '../../services/import.service';
import { DuplicateDomainsPreview } from './DuplicateDomainsPreview';
import { ImportResultDownloadAction } from './ImportResultDownloadAction';
import { ImportResultHeader } from './ImportResultHeader';
import { ImportResultMetric } from './ImportResultMetric';

export interface SitesImportResultCardProps {
  readonly result: SitesImportResult;
  readonly fileName?: string;
  readonly fileSize?: number;
  readonly completedAtUtc?: string;
  readonly onStartNewImport?: () => void;
}

export function SitesImportResultCard({
  result,
  fileName,
  fileSize,
  completedAtUtc,
  onStartNewImport,
}: SitesImportResultCardProps) {
  const [downloadError, setDownloadError] = useState<string | null>(null);
  const [downloadingAction, setDownloadingAction] = useState<'invalid' | 'warning' | null>(null);

  const insertedCount = result.insertedCount ?? 0;
  const skippedExistingCount = result.skippedExistingCount ?? 0;
  const duplicateDomainsCount = result.duplicateDomainsCount ?? 0;
  const duplicateDomainsPreview = result.duplicateDomainsPreview ?? [];
  const invalidRowsCount = result.invalidRowsCount ?? 0;
  const savedWithWarningsCount = result.savedWithWarningsCount ?? 0;
  const invalidRowsDownload = result.downloads?.invalidRows;
  const warningRowsDownload = result.downloads?.warningRows;
  const canDownloadInvalidRows =
    invalidRowsCount > 0 && !!invalidRowsDownload?.available && !!invalidRowsDownload.token;
  const canDownloadWarningRows =
    savedWithWarningsCount > 0 && !!warningRowsDownload?.available && !!warningRowsDownload.token;

  const handleDownload = async (
    action: 'invalid' | 'warning',
    token: string | undefined,
    fallbackFileName: string,
  ) => {
    if (!token) {
      return;
    }

    setDownloadError(null);
    setDownloadingAction(action);
    try {
      await downloadImportArtifactCsv(token, fallbackFileName);
    } catch (error) {
      setDownloadError(error instanceof Error ? error.message : 'Download failed');
    } finally {
      setDownloadingAction(null);
    }
  };

  const handleDownloadInvalidRows = async () => {
    await handleDownload(
      'invalid',
      invalidRowsDownload?.token,
      invalidRowsDownload?.fileName ?? 'sites-import-invalid-rows.csv',
    );
  };

  const handleDownloadWarningRows = async () => {
    await handleDownload(
      'warning',
      warningRowsDownload?.token,
      warningRowsDownload?.fileName ?? 'sites-import-warning-rows.csv',
    );
  };

  return (
    <Paper sx={{ p: { xs: 2.5, sm: 3 } }}>
      <Stack spacing={2.5}>
        <Stack spacing={1.5}>
          <ImportResultHeader
            title="Import result"
            fileName={fileName}
            fileSize={fileSize}
            completedAtUtc={completedAtUtc}
            onStartNewImport={onStartNewImport}
          />
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: {
                xs: 'repeat(2, minmax(0, 1fr))',
                sm: 'repeat(3, minmax(0, 1fr))',
                md: 'repeat(5, minmax(0, 1fr))',
              },
              gap: 1.5,
            }}
          >
            <ImportResultMetric label="Inserted" value={insertedCount} />
            <ImportResultMetric label="Skipped existing" value={skippedExistingCount} tone="warning" />
            <ImportResultMetric label="Duplicates" value={duplicateDomainsCount} tone="warning" />
            <ImportResultMetric label="Invalid rows" value={invalidRowsCount} tone="error" />
            <ImportResultMetric label="Warnings" value={savedWithWarningsCount} tone="warning" />
          </Box>

          <DuplicateDomainsPreview
            duplicateDomainsCount={duplicateDomainsCount}
            duplicateDomainsPreview={duplicateDomainsPreview}
          />
        </Stack>

        {savedWithWarningsCount > 0 && (
          <Alert severity="warning">
            Some rows were saved with warnings. Download the warning file to review affected fields and how they were handled.
          </Alert>
        )}

        {(canDownloadInvalidRows || canDownloadWarningRows) && (
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: '1fr',
              gap: 1,
            }}
          >
            {canDownloadInvalidRows && (
              <ImportResultDownloadAction
                label="Download invalid rows"
                helperText="Includes row number and validation details."
                onClick={handleDownloadInvalidRows}
                disabled={downloadingAction !== null}
              />
            )}
            {canDownloadWarningRows && (
              <ImportResultDownloadAction
                label="Download warning rows"
                helperText="Rows saved as Other because Location could not be mapped."
                onClick={handleDownloadWarningRows}
                disabled={downloadingAction !== null}
              />
            )}
          </Box>
        )}

        {downloadError && <Typography color="error">{downloadError}</Typography>}
      </Stack>
    </Paper>
  );
}

