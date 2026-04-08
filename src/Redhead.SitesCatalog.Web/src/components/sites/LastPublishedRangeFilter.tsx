import { Box, Typography, FormHelperText } from '@mui/material';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import dayjs from 'dayjs';
import { toApiMonth, fromApiMonth } from '../../utils/lastPublishedMonth';

const MIN_DATE = dayjs('2024-01-01');
const MAX_DATE = dayjs().endOf('year');

interface LastPublishedRangeFilterProps {
  fromValue: string | null;
  toValue: string | null;
  onFromChange: (value: string | null) => void;
  onToChange: (value: string | null) => void;
  error?: string;
}

export function LastPublishedRangeFilter({
  fromValue,
  toValue,
  onFromChange,
  onToChange,
  error,
}: LastPublishedRangeFilterProps) {
  return (
    <Box sx={{ flex: '0 0 auto' }}>
      <Typography variant="subtitle2" gutterBottom>
        Last Publication
      </Typography>
      <Box sx={{ display: 'flex', gap: 0.75, alignItems: 'center' }}>
        <DatePicker
          label="From"
          views={['year', 'month']}
          value={fromApiMonth(fromValue)}
          onChange={(v) => onFromChange(toApiMonth(v))}
          format="MMM YYYY"
          minDate={MIN_DATE}
          maxDate={MAX_DATE}
          slotProps={{
            textField: {
              size: 'small',
              sx: { width: 185 },
              error: !!error,
            },
            field: { readOnly: true },
          }}
        />
        <Typography sx={{ flexShrink: 0, color: 'text.secondary', mx: 0.25 }}>—</Typography>
        <DatePicker
          label="To"
          views={['year', 'month']}
          value={fromApiMonth(toValue)}
          onChange={(v) => onToChange(toApiMonth(v))}
          format="MMM YYYY"
          minDate={MIN_DATE}
          maxDate={MAX_DATE}
          slotProps={{
            textField: {
              size: 'small',
              sx: { width: 185 },
              error: !!error,
            },
            field: { readOnly: true },
          }}
        />
      </Box>
      {error && (
        <FormHelperText error sx={{ mt: 0.5 }}>
          {error}
        </FormHelperText>
      )}
    </Box>
  );
}
