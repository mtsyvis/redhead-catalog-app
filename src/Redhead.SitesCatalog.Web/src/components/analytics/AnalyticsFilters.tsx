import {
  Autocomplete,
  Box,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  TextField,
} from '@mui/material';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import type { Dayjs } from 'dayjs';
import type { AnalyticsClientOption } from '../../types/analytics.types';
import {
  type DateRangePreset,
  type DestinationFilterValue,
  getClientOptionLabel,
  type StatusFilterValue,
} from './analyticsFilterUtils';

interface AnalyticsFiltersProps {
  datePreset: DateRangePreset;
  onDatePresetChange: (value: DateRangePreset) => void;
  customFrom: Dayjs | null;
  onCustomFromChange: (value: Dayjs | null) => void;
  customTo: Dayjs | null;
  onCustomToChange: (value: Dayjs | null) => void;
  dateRangeError: string | null;
  clientSelectOptions: AnalyticsClientOption[];
  selectedClient: AnalyticsClientOption;
  onClientIdChange: (clientId: string | null) => void;
  clientsLoading: boolean;
  destination: DestinationFilterValue;
  onDestinationChange: (value: DestinationFilterValue) => void;
  status: StatusFilterValue;
  onStatusChange: (value: StatusFilterValue) => void;
}

export function AnalyticsFilters({
  datePreset,
  onDatePresetChange,
  customFrom,
  onCustomFromChange,
  customTo,
  onCustomToChange,
  dateRangeError,
  clientSelectOptions,
  selectedClient,
  onClientIdChange,
  clientsLoading,
  destination,
  onDestinationChange,
  status,
  onStatusChange,
}: AnalyticsFiltersProps) {
  return (
    <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: '1fr',
            sm: 'repeat(2, minmax(0, 1fr))',
            lg: '1fr 1.4fr 1fr 1fr',
          },
          gap: 2,
          alignItems: 'flex-start',
        }}
      >
        <FormControl size="small" fullWidth>
          <InputLabel>Date range</InputLabel>
          <Select
            value={datePreset}
            label="Date range"
            onChange={(event) => onDatePresetChange(event.target.value as DateRangePreset)}
          >
            <MenuItem value="last7">Last 7 days</MenuItem>
            <MenuItem value="last30">Last 30 days</MenuItem>
            <MenuItem value="last90">Last 90 days</MenuItem>
            <MenuItem value="custom">Custom</MenuItem>
          </Select>
        </FormControl>

        <Autocomplete
          size="small"
          options={clientSelectOptions}
          value={selectedClient}
          getOptionLabel={getClientOptionLabel}
          isOptionEqualToValue={(option, value) => option.id === value.id}
          onChange={(_event, option) =>
            onClientIdChange(option?.id === 'all' ? null : option?.id ?? null)
          }
          loading={clientsLoading}
          renderInput={(params) => (
            <TextField {...params} label="Client" placeholder="All clients" />
          )}
        />

        <FormControl size="small" fullWidth>
          <InputLabel>Destination</InputLabel>
          <Select
            value={destination}
            label="Destination"
            onChange={(event) => onDestinationChange(event.target.value as DestinationFilterValue)}
          >
            <MenuItem value="all">All destinations</MenuItem>
            <MenuItem value="Download">Download</MenuItem>
            <MenuItem value="GoogleDrive">Google Drive</MenuItem>
          </Select>
        </FormControl>

        <FormControl size="small" fullWidth>
          <InputLabel>Status</InputLabel>
          <Select
            value={status}
            label="Status"
            onChange={(event) => onStatusChange(event.target.value as StatusFilterValue)}
          >
            <MenuItem value="all">All statuses</MenuItem>
            <MenuItem value="successful">Successful</MenuItem>
            <MenuItem value="partial">Partial</MenuItem>
            <MenuItem value="blocked">Blocked</MenuItem>
          </Select>
        </FormControl>
      </Box>

      {datePreset === 'custom' && (
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, minmax(0, 240px))' },
            gap: 2,
            mt: 2,
          }}
        >
          <DatePicker
            label="From"
            value={customFrom}
            onChange={onCustomFromChange}
            slotProps={{
              textField: {
                size: 'small',
                error: !!dateRangeError,
                helperText: dateRangeError ?? undefined,
              },
            }}
          />
          <DatePicker
            label="To"
            value={customTo}
            onChange={onCustomToChange}
            slotProps={{
              textField: {
                size: 'small',
                error: !!dateRangeError,
              },
            }}
          />
        </Box>
      )}
    </Paper>
  );
}
