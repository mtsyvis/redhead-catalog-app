import ClearIcon from '@mui/icons-material/Clear';
import { Box, Typography, FormHelperText, IconButton } from '@mui/material';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import dayjs from 'dayjs';
import { useRef, useState } from 'react';
import type { MouseEvent } from 'react';
import { toApiMonth, fromApiMonth } from '../../../utils/lastPublishedMonth';

const MIN_DATE = dayjs('2024-01-01');
const MAX_DATE = dayjs().endOf('year');
const FIELD_WIDTH = 185;

const clearButtonSx = {
  position: 'absolute',
  top: '50%',
  right: 34,
  transform: 'translateY(-50%)',
  zIndex: 1,
  width: 26,
  height: 26,
  color: 'text.secondary',
  '&:hover': {
    bgcolor: 'action.hover',
    color: 'text.primary',
  },
};

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
  const [openPicker, setOpenPicker] = useState<'from' | 'to' | null>(null);
  const fromFieldRef = useRef<HTMLDivElement | null>(null);
  const toFieldRef = useRef<HTMLDivElement | null>(null);

  const clearFieldFocus = (field: 'from' | 'to') => {
    const clearFocus = () => {
      window.getSelection()?.removeAllRanges();

      const fieldRoot = field === 'from' ? fromFieldRef.current : toFieldRef.current;
      const activeElement = document.activeElement;
      if (activeElement instanceof HTMLElement && fieldRoot?.contains(activeElement)) {
        activeElement.blur();
      }
    };

    window.setTimeout(clearFocus, 0);
    window.setTimeout(clearFocus, 50);
  };

  const handleClosePicker = (field: 'from' | 'to') => {
    setOpenPicker(null);
    clearFieldFocus(field);
  };

  const handleClearFrom = (event: MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();
    event.stopPropagation();
    setOpenPicker(null);
    onFromChange(null);
    clearFieldFocus('from');
  };

  const handleClearTo = (event: MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();
    event.stopPropagation();
    setOpenPicker(null);
    onToChange(null);
    clearFieldFocus('to');
  };

  return (
    <Box sx={{ flex: '0 0 auto' }}>
      <Typography variant="subtitle2" gutterBottom>
        Last Publication
      </Typography>
      <Box sx={{ display: 'flex', gap: 0.75, alignItems: 'center' }}>
        <Box
          ref={fromFieldRef}
          onMouseDown={(event) => event.preventDefault()}
          onClick={() => setOpenPicker('from')}
          sx={{ position: 'relative', width: FIELD_WIDTH, cursor: 'pointer' }}
        >
          <DatePicker
            label="From"
            views={['year', 'month']}
            value={fromApiMonth(fromValue)}
            onChange={(v) => {
              onFromChange(toApiMonth(v));
              clearFieldFocus('from');
            }}
            open={openPicker === 'from'}
            onOpen={() => setOpenPicker('from')}
            onClose={() => handleClosePicker('from')}
            selectedSections={null}
            format="MMM YYYY"
            minDate={MIN_DATE}
            maxDate={MAX_DATE}
            slotProps={{
              textField: {
                size: 'small',
                focused: openPicker === 'from',
                sx: {
                  width: FIELD_WIDTH,
                  cursor: 'pointer',
                  userSelect: 'none',
                  '& *': {
                    cursor: 'pointer',
                    userSelect: 'none',
                  },
                },
                error: !!error,
              },
              field: { readOnly: true },
            }}
          />
          {fromValue !== null && (
            <IconButton
              size="small"
              aria-label="Clear Last Publication From"
              onMouseDown={(event) => {
                event.preventDefault();
                event.stopPropagation();
              }}
              onClick={handleClearFrom}
              sx={clearButtonSx}
            >
              <ClearIcon fontSize="small" />
            </IconButton>
          )}
        </Box>
        <Typography sx={{ flexShrink: 0, color: 'text.secondary', mx: 0.25 }}>—</Typography>
        <Box
          ref={toFieldRef}
          onMouseDown={(event) => event.preventDefault()}
          onClick={() => setOpenPicker('to')}
          sx={{ position: 'relative', width: FIELD_WIDTH, cursor: 'pointer' }}
        >
          <DatePicker
            label="To"
            views={['year', 'month']}
            value={fromApiMonth(toValue)}
            onChange={(v) => {
              onToChange(toApiMonth(v));
              clearFieldFocus('to');
            }}
            open={openPicker === 'to'}
            onOpen={() => setOpenPicker('to')}
            onClose={() => handleClosePicker('to')}
            selectedSections={null}
            format="MMM YYYY"
            minDate={MIN_DATE}
            maxDate={MAX_DATE}
            slotProps={{
              textField: {
                size: 'small',
                focused: openPicker === 'to',
                sx: {
                  width: FIELD_WIDTH,
                  cursor: 'pointer',
                  userSelect: 'none',
                  '& *': {
                    cursor: 'pointer',
                    userSelect: 'none',
                  },
                },
                error: !!error,
              },
              field: { readOnly: true },
            }}
          />
          {toValue !== null && (
            <IconButton
              size="small"
              aria-label="Clear Last Publication To"
              onMouseDown={(event) => {
                event.preventDefault();
                event.stopPropagation();
              }}
              onClick={handleClearTo}
              sx={clearButtonSx}
            >
              <ClearIcon fontSize="small" />
            </IconButton>
          )}
        </Box>
      </Box>
      {error && (
        <FormHelperText error sx={{ mt: 0.5 }}>
          {error}
        </FormHelperText>
      )}
    </Box>
  );
}
