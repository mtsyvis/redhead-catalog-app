import React from 'react';
import { Box } from '@mui/material';
import { ImportErrorAlert } from './ImportErrorAlert';

export interface ImportTabContentProps {
  instructions: React.ReactNode;
  uploadSection: React.ReactNode;
  error: string | null;
  onClearError: () => void;
  result: React.ReactNode | null;
}

export function ImportTabContent({
  instructions,
  uploadSection,
  error,
  onClearError,
  result,
}: ImportTabContentProps) {
  return (
    <Box>
      {instructions}
      {uploadSection}
      <ImportErrorAlert error={error} onClose={onClearError} />
      {result}
    </Box>
  );
}

