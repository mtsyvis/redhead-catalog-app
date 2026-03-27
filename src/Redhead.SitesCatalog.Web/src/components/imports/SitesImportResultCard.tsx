import { Box, Paper, Stack, Typography } from '@mui/material';
import { useState } from 'react';
import { downloadImportArtifactCsv, type SitesImportResult } from '../../services/import.service';
import { DuplicateDomainsPreview } from './DuplicateDomainsPreview';
import { ImportResultDownloadAction } from './ImportResultDownloadAction';

export interface SitesImportResultCardProps {
  readonly result: SitesImportResult;
}

export function SitesImportResultCard({ result }: SitesImportResultCardProps) {
  const [downloadError, setDownloadError] = useState<string | null>(null);
  const [downloading, setDownloading] = useState(false);

  const insertedCount = result.insertedCount ?? 0;
  const skippedExistingCount = result.skippedExistingCount ?? 0;
  const duplicateDomainsCount = result.duplicateDomainsCount ?? 0;
  const duplicateDomainsPreview = result.duplicateDomainsPreview ?? [];
  const invalidRowsCount = result.invalidRowsCount ?? 0;
  const invalidRowsDownload = result.downloads?.invalidRows;
  const canDownloadInvalidRows =
    invalidRowsCount > 0 && !!invalidRowsDownload?.available && !!invalidRowsDownload.token;

  const handleDownloadInvalidRows = async () => {
    if (!invalidRowsDownload?.token) {
      return;
    }

    setDownloadError(null);
    setDownloading(true);
    try {
      await downloadImportArtifactCsv(
        invalidRowsDownload.token,
        invalidRowsDownload.fileName ?? 'sites-import-invalid-rows.csv',
      );
    } catch (error) {
      setDownloadError(error instanceof Error ? error.message : 'Download failed');
    } finally {
      setDownloading(false);
    }
  };

  return (
    <Paper sx={{ p: 3 }}>
      <Stack spacing={3}>
        <Stack spacing={1.5}>
          <Typography variant="h6">Import result</Typography>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', sm: 'repeat(4, minmax(0, 1fr))' },
              gap: 1.5,
            }}
          >
            <Box>
              <Typography variant="caption" color="text.secondary">
                Inserted
              </Typography>
              <Typography variant="h6">{insertedCount}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Skipped existing domains
              </Typography>
              <Typography variant="h6">{skippedExistingCount}</Typography>
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

        {canDownloadInvalidRows && (
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', md: 'repeat(2, minmax(0, 360px))' },
              gap: 2,
              alignItems: 'stretch',
              justifyContent: 'start',
            }}
          >
            <ImportResultDownloadAction
              label="Download invalid rows"
              helperText="Includes row number and validation details."
              onClick={handleDownloadInvalidRows}
              disabled={downloading}
            />
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

