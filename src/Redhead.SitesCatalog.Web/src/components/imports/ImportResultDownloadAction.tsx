import { Box, Stack, Typography } from '@mui/material';
import FileDownloadOutlinedIcon from '@mui/icons-material/FileDownloadOutlined';
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
        p: 1.5,
        borderRadius: (theme) => `${theme.custom.cardRadius}px`,
        border: '1px solid rgba(38,38,38,0.10)',
        backgroundColor: 'background.paper',
        boxShadow: 'none',
      }}
    >
      <Stack spacing={1} direction={{ xs: 'column', sm: 'row' }} alignItems={{ sm: 'center' }}>
        <BrandButton
          startIcon={<FileDownloadOutlinedIcon />}
          onClick={onClick}
          disabled={disabled}
          sx={{
            alignSelf: 'flex-start',
            backgroundColor: 'background.paper',
            whiteSpace: 'nowrap',
          }}
        >
          {label}
        </BrandButton>
        <Typography variant="body2" color="text.secondary">
          {helperText}
        </Typography>
      </Stack>
    </Box>
  );
}
