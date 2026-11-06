import { useId } from 'react';
import { Box, MenuItem, TextField, Typography } from '@mui/material';
import type { SitesFilters } from '../../types/sites.types';

interface QuarantineFilterProps {
  value: SitesFilters['quarantine'];
  onChange: (value: SitesFilters['quarantine']) => void;
  labelPlacement?: 'above' | 'outlined';
}

export function QuarantineFilter({
  value,
  onChange,
  labelPlacement = 'above',
}: QuarantineFilterProps) {
  const labelId = useId();
  const outlinedLabel = labelPlacement === 'outlined';
  return (
    <Box
      sx={
        outlinedLabel
          ? { flex: '0 0 auto', minWidth: 0, width: { xs: '100%', sm: 185 } }
          : { flex: '0 0 auto', minWidth: 185 }
      }
    >
      {!outlinedLabel && (
        <Typography id={labelId} variant="subtitle2" gutterBottom>
          Quarantine Status
        </Typography>
      )}
      <TextField
        select
        fullWidth
        label={outlinedLabel ? 'Quarantine Status' : undefined}
        size="small"
        value={value}
        onChange={(event) => onChange(event.target.value as SitesFilters['quarantine'])}
        slotProps={outlinedLabel ? { inputLabel: { shrink: true } } : { select: { labelId } }}
        sx={outlinedLabel ? undefined : { width: 185 }}
      >
        <MenuItem value="all">All Sites</MenuItem>
        <MenuItem value="exclude">Available Only</MenuItem>
        <MenuItem value="only">Unavailable Only</MenuItem>
      </TextField>
    </Box>
  );
}
