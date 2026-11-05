import { useId } from 'react';
import { Box, MenuItem, TextField, Typography } from '@mui/material';
import type { SitesFilters } from '../../types/sites.types';

interface QuarantineFilterProps {
  value: SitesFilters['quarantine'];
  onChange: (value: SitesFilters['quarantine']) => void;
}

export function QuarantineFilter({ value, onChange }: QuarantineFilterProps) {
  const labelId = useId();
  return (
    <Box sx={{ flex: '0 0 auto', minWidth: 185 }}>
      <Typography id={labelId} variant="subtitle2" gutterBottom>
        Quarantine Status
      </Typography>
      <TextField
        select
        size="small"
        value={value}
        onChange={(event) => onChange(event.target.value as SitesFilters['quarantine'])}
        slotProps={{ select: { labelId } }}
        sx={{ width: 185 }}
      >
        <MenuItem value="all">All Sites</MenuItem>
        <MenuItem value="exclude">Available Only</MenuItem>
        <MenuItem value="only">Unavailable Only</MenuItem>
      </TextField>
    </Box>
  );
}
