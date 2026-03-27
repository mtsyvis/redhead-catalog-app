import { Box, Stack, Typography } from '@mui/material';
import { BrandButton } from '../common/BrandButton';

export interface ImportResultDownloadActionProps {
  readonly label: string;
  readonly helperText: string;
  readonly disabled: boolean;
  readonly onClick: () => Promise<void>;
}

export function ImportResultDownloadAction({
  label,
  helperText,
  disabled,
  onClick,
}: ImportResultDownloadActionProps) {
  return (
    <Box
      sx={{
        p: 2,
        borderRadius: (theme) => `${theme.custom.cardRadius}px`,
        border: '1px solid rgba(38,38,38,0.10)',
        backgroundColor: 'background.paper',
        boxShadow: 'none',
        minHeight: 116,
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
      }}
    >
      <Stack spacing={1}>
        <BrandButton
        onClick={onClick}
        disabled={disabled}
          sx={{
            alignSelf: 'flex-start',
            backgroundColor: 'background.paper',
          }}
        >
          {label}
        </BrandButton>
        <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 320 }}>
          {helperText}
        </Typography>
      </Stack>
    </Box>
  );
}
