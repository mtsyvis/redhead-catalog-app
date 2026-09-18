import React, { useState } from 'react';
import { Box, Chip, IconButton, Paper, Tooltip, Typography } from '@mui/material';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import type { ExportActivityStatus } from '../../types/analytics.types';
import { formatInteger } from '../../utils/numberFormat';

interface AnalyticsMetric {
  label: string;
  value: number;
  helperText?: string;
}

export function AnalyticsInfo({ label, text }: { label: string; text: string }) {
  const [open, setOpen] = useState(false);
  return (
    <Tooltip
      title={text}
      describeChild
      open={open}
      onOpen={() => setOpen(true)}
      onClose={() => setOpen(false)}
    >
      <IconButton
        size="small"
        aria-label={`About ${label}`}
        onClick={() => setOpen(true)}
        sx={{ p: 0.25, color: 'text.secondary' }}
      >
        <InfoOutlinedIcon sx={{ fontSize: 16 }} />
      </IconButton>
    </Tooltip>
  );
}

export function AnalyticsMetrics({
  metrics,
  inline = false,
}: {
  metrics: AnalyticsMetric[];
  inline?: boolean;
}) {
  return (
    <Box
      component={inline ? 'div' : Paper}
      variant={inline ? undefined : 'outlined'}
      sx={{
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'center',
        gap: inline ? 2 : 0,
        overflow: 'hidden',
      }}
    >
      {metrics.map(({ label, value, helperText }) => (
        <Box
          key={label}
          sx={
            inline
              ? { display: 'flex', alignItems: 'center', gap: 0.5 }
              : { flex: '1 1 150px', minWidth: 0, px: 2, py: 1.25 }
          }
        >
          {inline && (
            <Typography component="span" sx={{ fontSize: 18, fontWeight: 700 }}>
              {formatInteger(value)}
            </Typography>
          )}
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
            <Typography component="span" color="text.secondary" sx={{ fontSize: inline ? 14 : 12 }}>
              {label}
            </Typography>
            {helperText && <AnalyticsInfo label={label} text={helperText} />}
          </Box>
          {!inline && (
            <Typography sx={{ fontSize: 26, fontWeight: 700, lineHeight: 1.2 }}>
              {formatInteger(value)}
            </Typography>
          )}
        </Box>
      ))}
    </Box>
  );
}

interface AnalyticsSectionProps {
  title: string;
  helperText: string;
  children: React.ReactNode;
}

export function AnalyticsSection({ title, helperText, children }: AnalyticsSectionProps) {
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mb: 1 }}>
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          {title}
        </Typography>
        <AnalyticsInfo label={title} text={helperText} />
      </Box>
      {children}
    </Paper>
  );
}

export function EmptyState({ text }: { text: string }) {
  return (
    <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
      {text}
    </Typography>
  );
}

export function ExportStatusChip({ status }: { status: ExportActivityStatus }) {
  const color = status === 'Blocked' ? 'error' : status === 'Partial' ? 'warning' : 'success';

  return <Chip size="small" label={status} color={color} variant="outlined" />;
}
