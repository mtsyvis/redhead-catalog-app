import { Alert, Box, Paper, Stack, Typography } from '@mui/material';
import { useState } from 'react';
import { downloadImportArtifactCsv, type UpdateImportResult } from '../../services/import.service';
import { DuplicateDomainsPreview } from './DuplicateDomainsPreview';
import { ImportResultDownloadAction } from './ImportResultDownloadAction';
import { ImportResultHeader } from './ImportResultHeader';

export interface UpdateImportResultCardProps {
  readonly title: string;
  readonly result: UpdateImportResult;
  readonly fileName?: string;
  readonly fileSize?: number;
  readonly completedAtUtc?: string;
  readonly onStartNewImport?: () => void;
}

interface ResultMetricProps {
  readonly label: string;
  readonly value: number;
  readonly tone?: 'default' | 'warning' | 'error';
}

function ResultMetric({ label, value, tone = 'default' }: ResultMetricProps) {
  const activeIssue = value > 0 && tone !== 'default';
  const color =
    activeIssue && tone === 'error'
      ? 'error.main'
      : activeIssue && tone === 'warning'
        ? 'warning.main'
        : 'text.primary';

  return (
    <Box sx={{ minWidth: 0 }}>
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="h6" sx={{ color }}>
        {value}
      </Typography>
    </Box>
  );
}

export function UpdateImportResultCard({
  title,
  result,
  fileName,
  fileSize,
  completedAtUtc,
  onStartNewImport,
}: UpdateImportResultCardProps) {
  const [downloadError, setDownloadError] = useState<string | null>(null);
  const [downloadingAction, setDownloadingAction] = useState<
    'invalid' | 'unmatched' | 'warning' | null
  >(null);

  const updatedCount = result.updatedCount ?? 0;
  const unmatchedRowsCount = result.unmatchedRowsCount ?? 0;
  const duplicateDomainsCount = result.duplicateDomainsCount ?? 0;
  const duplicateDomainsPreview = result.duplicateDomainsPreview ?? [];
  const invalidRowsCount = result.invalidRowsCount ?? 0;
  const savedWithWarningsCount = result.savedWithWarningsCount ?? 0;
  const metricSnapshotsSavedCount = result.metricSnapshotsSavedCount;
  const metricSnapshotDate = result.metricSnapshotDate;
  const metricHistorySkippedReason = result.metricHistorySkippedReason;
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

  const onDownloadInvalidRows = async () => {
    await handleDownload(
      'invalid',
      invalidRowsDownload?.token,
      invalidRowsDownload?.fileName ?? 'update-import-invalid-rows.csv',
    );
  };

  const onDownloadUnmatchedRows = async () => {
    await handleDownload(
      'unmatched',
      unmatchedRowsDownload?.token,
      unmatchedRowsDownload?.fileName ?? 'update-import-unmatched-rows.csv',
    );
  };

  const onDownloadWarningRows = async () => {
    await handleDownload(
      'warning',
      warningRowsDownload?.token,
      warningRowsDownload?.fileName ?? 'update-import-warning-rows.csv',
    );
  };

  return (
    <Paper sx={{ p: { xs: 2.5, sm: 3 } }}>
      <Stack spacing={2.5}>
        <Stack spacing={1.5}>
          <ImportResultHeader
            title={title}
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
            <ResultMetric label="Updated" value={updatedCount} />
            <ResultMetric label="Unmatched" value={unmatchedRowsCount} tone="warning" />
            <ResultMetric label="Duplicates" value={duplicateDomainsCount} tone="warning" />
            <ResultMetric label="Invalid rows" value={invalidRowsCount} tone="error" />
            <ResultMetric label="Warnings" value={savedWithWarningsCount} tone="warning" />
          </Box>

          <DuplicateDomainsPreview
            duplicateDomainsCount={duplicateDomainsCount}
            duplicateDomainsPreview={duplicateDomainsPreview}
          />
        </Stack>

        {metricSnapshotsSavedCount !== undefined && metricSnapshotsSavedCount !== null && (
          <Alert severity="success">
            Traffic/DR history saved: {metricSnapshotsSavedCount} snapshots
            {metricSnapshotDate ? ` for ${metricSnapshotDate}` : ''}.
          </Alert>
        )}

        {metricHistorySkippedReason && (
          <Alert severity="info">
            Traffic/DR history not saved: {metricHistorySkippedReason}
          </Alert>
        )}

        {savedWithWarningsCount > 0 && (
          <Alert severity="warning">
            Some rows were saved with warnings. Download the warning file to review affected fields and how they were handled.
          </Alert>
        )}

        {(canDownloadInvalidRows || canDownloadUnmatchedRows || canDownloadWarningRows) && (
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
                onClick={onDownloadInvalidRows}
                disabled={downloadingAction !== null}
              />
            )}
            {canDownloadUnmatchedRows && (
              <ImportResultDownloadAction
                label="Download unmatched rows"
                helperText="Includes domains that were not found in the catalog."
                onClick={onDownloadUnmatchedRows}
                disabled={downloadingAction !== null}
              />
            )}
            {canDownloadWarningRows && (
              <ImportResultDownloadAction
                label="Download warning rows"
                helperText="Rows saved as Other because Location could not be mapped."
                onClick={onDownloadWarningRows}
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
