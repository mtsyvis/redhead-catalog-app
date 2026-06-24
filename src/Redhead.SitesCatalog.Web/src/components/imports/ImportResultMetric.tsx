import { Box, Typography } from '@mui/material';

export interface ImportResultMetricProps {
  readonly label: string;
  readonly value: number;
  readonly tone?: 'default' | 'warning' | 'error';
}

export function ImportResultMetric({ label, value, tone = 'default' }: ImportResultMetricProps) {
  const activeIssue = value > 0 && tone !== 'default';
  const color =
    activeIssue && tone === 'error'
      ? 'error.main'
      : activeIssue && tone === 'warning'
        ? 'warning.main'
        : 'text.primary';

  return (
    <Box sx={{ minWidth: 0 }}>
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="h6" sx={{ color }}>
        {value}
      </Typography>
    </Box>
  );
}
