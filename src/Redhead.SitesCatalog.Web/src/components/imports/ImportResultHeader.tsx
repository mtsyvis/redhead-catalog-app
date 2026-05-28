import { Box, Stack, Typography } from '@mui/material';
import RestartAltIcon from '@mui/icons-material/RestartAlt';
import { BrandButton } from '../common/BrandButton';

export interface ImportResultHeaderProps {
  title: string;
  fileName?: string;
  fileSize?: number;
  completedAtUtc?: string;
  onStartNewImport?: () => void;
}

function formatFileSize(fileSize: number | undefined) {
  if (fileSize === undefined) {
    return null;
  }

  if (fileSize < 1024 * 1024) {
    return `${(fileSize / 1024).toFixed(1)} KB`;
  }

  return `${(fileSize / 1024 / 1024).toFixed(1)} MB`;
}

function formatCompletedAt(completedAtUtc: string | undefined) {
  if (!completedAtUtc) {
    return null;
  }

  const timestamp = Date.parse(completedAtUtc);
  if (Number.isNaN(timestamp)) {
    return null;
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(timestamp));
}

export function ImportResultHeader({
  title,
  fileName,
  fileSize,
  completedAtUtc,
  onStartNewImport,
}: ImportResultHeaderProps) {
  const completedAt = formatCompletedAt(completedAtUtc);
  const formattedFileSize = formatFileSize(fileSize);
  const metadata = [
    completedAt ? `Completed ${completedAt}` : null,
    fileName ? `File: ${fileName}${formattedFileSize ? ` (${formattedFileSize})` : ''}` : null,
  ].filter(Boolean);

  return (
    <Box
      sx={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        gap: 2,
        flexWrap: 'wrap',
      }}
    >
      <Stack spacing={0.5}>
        <Typography variant="h6">{title}</Typography>
        {metadata.length > 0 && (
          <Typography variant="body2" color="text.secondary">
            {metadata.join(' | ')}
          </Typography>
        )}
      </Stack>

      {onStartNewImport && (
        <BrandButton startIcon={<RestartAltIcon />} onClick={onStartNewImport}>
          Start new import
        </BrandButton>
      )}
    </Box>
  );
}
