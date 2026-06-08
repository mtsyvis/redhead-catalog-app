import React from 'react';
import { Chip, Paper, Typography } from '@mui/material';
import type { ExportActivityStatus } from '../../types/analytics.types';
import { formatInteger } from '../../utils/numberFormat';

interface KpiCardProps {
  label: string;
  value: number;
  helperText: string;
}

export function KpiCard({ label, value, helperText }: KpiCardProps) {
  return (
    <Paper variant="outlined" sx={{ p: 2, height: '100%' }}>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 0.75 }}>
        {label}
      </Typography>
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 1 }}>
        {formatInteger(value)}
      </Typography>
      <Typography variant="body2" color="text.secondary">
        {helperText}
      </Typography>
    </Paper>
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
      <Typography variant="h6" sx={{ fontWeight: 700 }}>
        {title}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 2 }}>
        {helperText}
      </Typography>
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
  const color = status === 'Blocked'
    ? 'error'
    : status === 'Partial'
      ? 'warning'
      : 'success';

  return <Chip size="small" label={status} color={color} variant="outlined" />;
}
