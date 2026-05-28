import {
  Alert,
  Autocomplete,
  Box,
  Checkbox,
  Chip,
  CircularProgress,
  TextField,
  Typography,
} from '@mui/material';
import type {
  LocationFilterOptions,
  LocationFilterSelection,
} from '../../../types/sites.types';
import { pluralize } from '../../../utils/pluralize';

interface LocationFilterProps {
  value: LocationFilterSelection[];
  options: LocationFilterOptions | null;
  loading: boolean;
  error: string | null;
  onChange: (value: LocationFilterSelection[]) => void;
}

const OTHER_LOCATION_SPECIAL_KEY = 'other';
const UNKNOWN_LOCATION_SPECIAL_KEY = 'unknown';

type LocationSelectOption = LocationFilterSelection & {
  section: 'Regions' | 'Business groups' | 'Locations' | 'Special';
};

function getGroupSection(groupType: string): 'Regions' | 'Business groups' {
  return groupType.toLowerCase() === 'region' ? 'Regions' : 'Business groups';
}

function getLocationOptionId(option: LocationFilterSelection): string {
  return `${option.kind}:${option.key}`;
}

function formatLocationSelectionLabel(option: LocationFilterSelection): string {
  if (option.kind === 'group' && option.locationCount != null) {
    return `${option.displayName} - ${option.locationCount} ${pluralize(option.locationCount, 'location')}`;
  }

  return option.displayName;
}

function buildLocationSelectOptions(options: LocationFilterOptions | null): LocationSelectOption[] {
  if (!options) return [];

  const unknownKey = options.special.unknown.key;
  const groups = options.groups.map<LocationSelectOption>((group) => ({
    kind: 'group',
    key: group.key,
    displayName: group.displayName,
    groupType: group.groupType,
    locationCount: group.locationCount,
    section: getGroupSection(group.groupType),
  }));
  const locations = options.locations
    .filter((location) => location.key !== unknownKey)
    .map<LocationSelectOption>((location) => ({
      kind: 'location',
      key: location.key,
      displayName: location.displayName,
      section: 'Locations',
    }));
  const special: LocationSelectOption[] = [
    {
      kind: 'special',
      key: UNKNOWN_LOCATION_SPECIAL_KEY,
      displayName: options.special.unknown.displayName,
      locationKey: options.special.unknown.key,
      section: 'Special',
    },
    {
      kind: 'special',
      key: OTHER_LOCATION_SPECIAL_KEY,
      displayName: options.special.other?.displayName ?? 'Other',
      locationKey: options.special.other?.key,
      section: 'Special',
    },
  ];

  return [...groups, ...locations, ...special];
}

function toLocationFilterSelection(option: LocationSelectOption): LocationFilterSelection {
  if (option.kind === 'group') {
    return {
      kind: 'group',
      key: option.key,
      displayName: option.displayName,
      groupType: option.groupType,
      locationCount: option.locationCount,
    };
  }

  if (option.kind === 'special') {
    return {
      kind: 'special',
      key: option.key,
      displayName: option.displayName,
      locationKey: option.locationKey,
    };
  }

  return {
    kind: 'location',
    key: option.key,
    displayName: option.displayName,
  };
}

function restoreSelectedOptionSection(selection: LocationFilterSelection): LocationSelectOption {
  return {
    ...selection,
    section:
      selection.kind === 'group'
        ? getGroupSection(selection.groupType)
        : selection.kind === 'special'
          ? 'Special'
          : 'Locations',
  } as LocationSelectOption;
}

export function LocationFilter({
  value,
  options,
  loading,
  error,
  onChange,
}: Readonly<LocationFilterProps>) {
  const selectOptions = buildLocationSelectOptions(options);
  const selectedOptions = value.map(
    (selection) =>
      selectOptions.find(
        (option) => getLocationOptionId(option) === getLocationOptionId(selection)
      ) ?? restoreSelectedOptionSection(selection)
  );

  return (
    <Box sx={{ flex: 1, minWidth: '200px', maxWidth: '350px' }}>
      <Typography variant="subtitle2" gutterBottom>
        Location
      </Typography>
      <Autocomplete<LocationSelectOption, true, false, false>
        multiple
        size="small"
        options={selectOptions}
        value={selectedOptions}
        loading={loading}
        disabled={Boolean(error)}
        groupBy={(option) => option.section}
        getOptionLabel={formatLocationSelectionLabel}
        isOptionEqualToValue={(option, selectedValue) =>
          getLocationOptionId(option) === getLocationOptionId(selectedValue)
        }
        onChange={(_, newValue) => onChange(newValue.map(toLocationFilterSelection))}
        renderOption={(props, option, { selected }) => {
          const { key, ...optionProps } = props;
          return (
            <Box component="li" key={key} {...optionProps}>
              <Checkbox checked={selected} sx={{ mr: 1 }} />
              <Box sx={{ minWidth: 0 }}>
                <Typography variant="body2">{option.displayName}</Typography>
                {option.kind === 'group' && (
                  <Typography variant="caption" color="text.secondary">
                    {option.locationCount} {pluralize(option.locationCount ?? 0, 'location')}
                  </Typography>
                )}
              </Box>
            </Box>
          );
        }}
        renderTags={(tagValue, getTagProps) =>
          tagValue.map((option, index) => {
            const { key, ...tagProps } = getTagProps({ index });
            return (
              <Chip
                key={key}
                label={formatLocationSelectionLabel(option)}
                size="small"
                {...tagProps}
              />
            );
          })
        }
        renderInput={(params) => (
          <TextField
            {...params}
            placeholder={value.length === 0 ? 'Select locations' : ''}
            helperText={error ?? undefined}
            error={Boolean(error)}
            InputProps={{
              ...params.InputProps,
              endAdornment: (
                <>
                  {loading ? <CircularProgress color="inherit" size={18} /> : null}
                  {params.InputProps.endAdornment}
                </>
              ),
            }}
          />
        )}
        disableCloseOnSelect
        limitTags={2}
      />
      {error && (
        <Alert severity="warning" sx={{ mt: 1 }}>
          Location filter is unavailable.
        </Alert>
      )}
    </Box>
  );
}
