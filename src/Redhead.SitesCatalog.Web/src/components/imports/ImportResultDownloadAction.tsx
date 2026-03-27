import { Button, Stack, Typography } from '@mui/material';

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
    <Stack spacing={0.5}>
      <Button
        variant="outlined"
        onClick={onClick}
        disabled={disabled}
        sx={{ alignSelf: 'flex-start', minHeight: 40, fontWeight: 600 }}
      >
        {label}
      </Button>
      <Typography variant="caption" color="text.secondary">
        {helperText}
      </Typography>
    </Stack>
  );
}
