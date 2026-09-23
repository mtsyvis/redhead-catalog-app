import React from 'react';
import { Box, Chip, Typography } from '@mui/material';

interface ApplicationSettingValueRowProps {
  label: string;
  description?: string;
  value: string;
  source: 'Environment' | 'Built in';
  changeBehavior: string;
}

export const ApplicationSettingValueRow: React.FC<ApplicationSettingValueRowProps> = ({
  label,
  description,
  value,
  source,
  changeBehavior,
}) => (
  <Box
    sx={{
      display: 'grid',
      gridTemplateColumns: { xs: '1fr', sm: 'minmax(0, 1fr) auto' },
      alignItems: 'center',
      gap: 1.5,
      py: 1.5,
    }}
  >
    <Box sx={{ minWidth: 0 }}>
      <Typography variant="body2" sx={{ fontWeight: 600 }}>
        {label}
      </Typography>
      {description && (
        <Typography variant="caption" color="text.secondary">
          {description}
        </Typography>
      )}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, flexWrap: 'wrap', mt: 0.5 }}>
        <Chip
          label={source}
          variant="outlined"
          size="small"
          sx={{ height: 22, fontSize: '0.6875rem' }}
        />
        <Typography variant="caption" color="text.secondary">
          {changeBehavior}
        </Typography>
      </Box>
    </Box>
    <Typography
      variant="subtitle2"
      sx={{ textAlign: { xs: 'left', sm: 'right' }, fontVariantNumeric: 'tabular-nums' }}
    >
      {value}
    </Typography>
  </Box>
);
