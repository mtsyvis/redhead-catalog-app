import { Box, Paper, Stack, Typography } from '@mui/material';
import { useState } from 'react';
import { downloadImportArtifactCsv, type UpdateImportResult } from '../../services/import.service';
import { DuplicateDomainsPreview } from './DuplicateDomainsPreview';
import { ImportResultDownloadAction } from './ImportResultDownloadAction';

export interface UpdateImportResultCardProps {
  readonly title: string;
  readonly result: UpdateImportResult;
}

export function UpdateImportResultCard({
  title,
  result,
}: UpdateImportResultCardProps) {
  const [downloadError, setDownloadError] = useState<string | null>(null);
  const [downloadingAction, setDownloadingAction] = useState<'invalid' | 'unmatched' | null>(null);

  const updatedCount = result.updatedCount ?? 0;
  const unmatchedRowsCount = result.unmatchedRowsCount ?? 0;
  const duplicateDomainsCount = result.duplicateDomainsCount ?? 0;
  const duplicateDomainsPreview = result.duplicateDomainsPreview ?? [];
  const invalidRowsCount = result.invalidRowsCount ?? 0;
  const invalidRowsDownload = result.downloads?.invalidRows;
  const unmatchedRowsDownload = result.downloads?.unmatchedRows;
  const canDownloadInvalidRows =
    invalidRowsCount > 0 && !!invalidRowsDownload?.available && !!invalidRowsDownload.token;
  const canDownloadUnmatchedRows =
    unmatchedRowsCount > 0 && !!unmatchedRowsDownload?.available && !!unmatchedRowsDownload.token;

  const handleDownload = async (
    action: 'invalid' | 'unmatched',
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

  return (
    <Paper sx={{ p: 3 }}>
      <Stack spacing={3}>
        <Stack spacing={1.5}>
          <Typography variant="h6">{title}</Typography>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', sm: 'repeat(4, minmax(0, 1fr))' },
              gap: 1.5,
            }}
          >
            <Box>
              <Typography variant="caption" color="text.secondary">
                Updated
              </Typography>
              <Typography variant="h6">{updatedCount}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Unmatched domains
              </Typography>
              <Typography variant="h6">{unmatchedRowsCount}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Duplicate domains in file
              </Typography>
              <Typography variant="h6">{duplicateDomainsCount}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Invalid rows
              </Typography>
              <Typography variant="h6">{invalidRowsCount}</Typography>
            </Box>
          </Box>
        </Stack>

        {(canDownloadInvalidRows || canDownloadUnmatchedRows) && (
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', md: 'repeat(2, minmax(0, 360px))' },
              gap: 2,
              alignItems: 'stretch',
              justifyContent: 'start',
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
                helperText="Includes row number from the source file."
                onClick={onDownloadUnmatchedRows}
                disabled={downloadingAction !== null}
              />
            )}
          </Box>
        )}

        <DuplicateDomainsPreview
          duplicateDomainsCount={duplicateDomainsCount}
          duplicateDomainsPreview={duplicateDomainsPreview}
        />

        {downloadError && <Typography color="error">{downloadError}</Typography>}
      </Stack>
    </Paper>
  );
}
