import { useState, type ReactNode } from 'react';
import CheckIcon from '@mui/icons-material/Check';
import {
  Alert,
  Box,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from '@mui/material';

import { BrandButton } from '../common/BrandButton';

interface OneTimeValueDialogProps {
  title: string;
  email: string;
  notice: ReactNode;
  valueLabel: string;
  value: string;
  helperText: string;
  copyLabel: string;
  copyErrorMessage: string;
  onClose: () => void;
}

export const OneTimeValueDialog: React.FC<OneTimeValueDialogProps> = ({
  title,
  email,
  notice,
  valueLabel,
  value,
  helperText,
  copyLabel,
  copyErrorMessage,
  onClose,
}) => {
  const [copyState, setCopyState] = useState<'idle' | 'copied' | 'failed'>('idle');

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopyState('copied');
    } catch {
      setCopyState('failed');
    }
  };

  return (
    <Dialog
      open
      disableEscapeKeyDown
      onClose={(_event, reason) => {
        if (reason !== 'backdropClick') onClose();
      }}
      maxWidth="sm"
      fullWidth
    >
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <Stack spacing={2}>
          <Typography variant="body2" color="text.secondary">
            For {email}
          </Typography>

          {notice}

          <Box>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              {valueLabel}
            </Typography>
            <Box
              sx={{
                p: 2,
                bgcolor: 'grey.100',
                borderRadius: 1,
                fontFamily: 'monospace',
                overflowWrap: 'anywhere',
                userSelect: 'text',
              }}
            >
              {value}
            </Box>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
              {helperText}
            </Typography>
          </Box>

          {copyState === 'failed' ? <Alert severity="error">{copyErrorMessage}</Alert> : null}
        </Stack>
      </DialogContent>
      <DialogActions>
        <BrandButton kind="outline" onClick={onClose}>
          Done
        </BrandButton>
        <BrandButton
          kind="primary"
          onClick={() => void handleCopy()}
          startIcon={copyState === 'copied' ? <CheckIcon /> : undefined}
        >
          {copyState === 'copied' ? 'Copied' : copyLabel}
        </BrandButton>
      </DialogActions>
    </Dialog>
  );
};
