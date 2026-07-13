import { Alert, Box, Paper, Stack, Typography } from '@mui/material';
import { useState } from 'react';
import {
  downloadImportArtifactCsv,
  type WebmasterOffersImportResult,
} from '../../services/import.service';
import { ImportResultDownloadAction } from './ImportResultDownloadAction';
import { ImportResultHeader } from './ImportResultHeader';
import { ImportResultMetric } from './ImportResultMetric';

export interface WebmasterOffersImportResultCardProps {
  readonly result: WebmasterOffersImportResult;
  readonly fileName?: string;
  readonly fileSize?: number;
  readonly completedAtUtc?: string;
  readonly onStartNewImport?: () => void;
}

export function WebmasterOffersImportResultCard({
  result,
  fileName,
  fileSize,
  completedAtUtc,
  onStartNewImport,
}: WebmasterOffersImportResultCardProps) {
  const [downloadError, setDownloadError] = useState<string | null>(null);
  const [downloadingAction, setDownloadingAction] = useState<
    'invalid' | 'unmatched' | 'warning' | null
  >(null);

  const importedCount = result.importedCount ?? 0;
  const unmatchedRowsCount = result.unmatchedRowsCount ?? 0;
  const invalidRowsCount = result.invalidRowsCount ?? 0;
  const savedWithWarningsCount = result.savedWithWarningsCount ?? 0;
  const invalidRowsDownload = result.downloads?.invalidRows;
  const unmatchedRowsDownload = result.downloads?.unmatchedRows;
  const warningRowsDownload = result.downloads?.warningRows;
  const canDownloadInvalidRows =
    invalidRowsCount > 0 && !!invalidRowsDownload?.available && !!invalidRowsDownload.token;
  const canDownloadUnmatchedRows =
    unmatchedRowsCount > 0 && !!unmatchedRowsDownload?.available && !!unmatchedRowsDownload.token;
  const canDownloadWarningRows =
    savedWithWarningsCount > 0 && !!warningRowsDownload?.available && !!warningRowsDownload.token;

  const handleDownload = async (
    action: 'invalid' | 'unmatched' | 'warning',
    token: string | undefined,
    fallbackFileName: string,
  ) => {
    if (!token) return;

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

  return (
    <Paper sx={{ p: { xs: 2.5, sm: 3 } }}>
      <Stack spacing={2.5}>
        <Stack spacing={1.5}>
          <ImportResultHeader
            title="Webmaster offers import result"
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
                sm: 'repeat(4, minmax(0, 1fr))',
              },
              gap: 1.5,
            }}
          >
            <ImportResultMetric label="Imported" value={importedCount} />
            <ImportResultMetric label="Unmatched" value={unmatchedRowsCount} tone="warning" />
            <ImportResultMetric label="Invalid rows" value={invalidRowsCount} tone="error" />
            <ImportResultMetric label="Warnings" value={savedWithWarningsCount} tone="warning" />
          </Box>
        </Stack>

        {savedWithWarningsCount > 0 && (
          <Alert severity="warning">
            Some offers were saved with mailbox alias warnings. Download the warning file to review unmapped aliases.
          </Alert>
        )}

        {(canDownloadInvalidRows || canDownloadUnmatchedRows || canDownloadWarningRows) && (
          <Box sx={{ display: 'grid', gridTemplateColumns: '1fr', gap: 1 }}>
            {canDownloadInvalidRows && (
              <ImportResultDownloadAction
                label="Download invalid rows"
                helperText="Includes row number and validation details."
                onClick={() =>
                  handleDownload(
                    'invalid',
                    invalidRowsDownload?.token,
                    invalidRowsDownload?.fileName ?? 'webmaster-offers-invalid-rows.csv'
                  )
                }
                disabled={downloadingAction !== null}
              />
            )}
            {canDownloadUnmatchedRows && (
              <ImportResultDownloadAction
                label="Download unmatched rows"
                helperText="Includes domains that were not found in the catalog."
                onClick={() =>
                  handleDownload(
                    'unmatched',
                    unmatchedRowsDownload?.token,
                    unmatchedRowsDownload?.fileName ?? 'webmaster-offers-unmatched-rows.csv'
                  )
                }
                disabled={downloadingAction !== null}
              />
            )}
            {canDownloadWarningRows && (
              <ImportResultDownloadAction
                label="Download warning rows"
                helperText="Includes unmapped linkbuilder mailbox aliases."
                onClick={() =>
                  handleDownload(
                    'warning',
                    warningRowsDownload?.token,
                    warningRowsDownload?.fileName ?? 'webmaster-offers-warning-rows.csv'
                  )
                }
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
