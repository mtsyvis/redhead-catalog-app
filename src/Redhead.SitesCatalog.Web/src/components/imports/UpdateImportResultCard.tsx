import { Box, Button, Paper, Stack, Typography } from '@mui/material';
import { useState } from 'react';
import { downloadImportArtifactCsv, type UpdateImportResult } from '../../services/import.service';

export interface UpdateImportResultCardProps {
  readonly title: string;
  readonly result: UpdateImportResult;
}

export function UpdateImportResultCard({
  title,
  result,
}: UpdateImportResultCardProps) {
  const [downloadError, setDownloadError] = useState<string | null>(null);
  const [downloading, setDownloading] = useState(false);

  const updatedCount = result.updatedCount ?? result.matched ?? 0;
  const duplicateInputRowsCount =
    result.duplicateInputRowsCount ?? result.duplicatesCount ?? result.duplicates?.length ?? 0;
  const invalidRowsCount = result.invalidRowsCount ?? result.errorsCount ?? result.errors?.length ?? 0;
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
        invalidRowsDownload.fileName ?? 'update-import-invalid-rows.csv',
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
          <Typography variant="h6">{title}</Typography>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, minmax(0, 1fr))' },
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
                Duplicate rows in file
              </Typography>
              <Typography variant="h6">{duplicateInputRowsCount}</Typography>
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
              maxWidth: 420,
              px: 1.5,
              py: 1.25,
              borderRadius: 1.5,
              bgcolor: 'action.hover',
            }}
          >
            <Stack spacing={0.5}>
              <Button
                variant="outlined"
                onClick={handleDownloadInvalidRows}
                disabled={downloading}
                sx={{ alignSelf: 'flex-start', minHeight: 40, fontWeight: 600 }}
              >
                Download invalid rows
              </Button>
              <Typography variant="caption" color="text.secondary">
                Includes original invalid rows, Source Row Number, and Error Details.
              </Typography>
            </Stack>
          </Box>
        )}

        {downloadError && <Typography color="error">{downloadError}</Typography>}
      </Stack>
    </Paper>
  );
}
