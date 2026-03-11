import React from 'react';
import { Box, Paper, Typography, CircularProgress } from '@mui/material';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import { BrandButton } from '../common/BrandButton';

export interface ImportUploadSectionProps {
  file: File | null;
  loading: boolean;
  accept?: string;
  maxFileSizeBytes?: number;
  onFileChange: (event: React.ChangeEvent<HTMLInputElement>) => void;
  onSubmit: (event: React.FormEvent<HTMLFormElement>) => void;
  submitLabel?: string;
  loadingLabel?: string;
}

const DEFAULT_SUBMIT_LABEL = 'Import';
const DEFAULT_LOADING_LABEL = 'Importing…';

export function ImportUploadSection({
  file,
  loading,
  accept,
  maxFileSizeBytes,
  onFileChange,
  onSubmit,
  submitLabel = DEFAULT_SUBMIT_LABEL,
  loadingLabel = DEFAULT_LOADING_LABEL,
}: ImportUploadSectionProps) {
  const isTooLarge =
    file !== null && maxFileSizeBytes !== undefined ? file.size > maxFileSizeBytes : false;

  const isSubmitDisabled = !file || loading || isTooLarge;

  return (
    <Paper sx={{ p: 3, mb: 3 }}>
      <form onSubmit={onSubmit}>
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <BrandButton
            component="label"
            startIcon={<UploadFileIcon />}
            disabled={loading}
          >
            Choose file (CSV)
            <input
              type="file"
              hidden
              accept={accept}
              onChange={onFileChange}
            />
          </BrandButton>

          {file && (
            <Typography variant="body2" color="text.secondary">
              Selected: {file.name} ({(file.size / 1024).toFixed(1)} KB)
            </Typography>
          )}

          <BrandButton
            type="submit"
            kind="primary"
            disabled={isSubmitDisabled}
            startIcon={loading ? <CircularProgress size={20} color="inherit" /> : null}
          >
            {loading ? loadingLabel : submitLabel}
          </BrandButton>
        </Box>
      </form>
    </Paper>
  );
}

