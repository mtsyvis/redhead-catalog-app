import { useDeferredValue, useMemo, useState } from 'react';
import type { MouseEvent, ReactNode } from 'react';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  Collapse,
  IconButton,
  Popover,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import ArrowDropDownIcon from '@mui/icons-material/ArrowDropDown';
import ClearIcon from '@mui/icons-material/Clear';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import SearchIcon from '@mui/icons-material/Search';
import type {
  LocationFilterOption,
  LocationFilterOptions,
  LocationFilterSelection,
  LocationGroupFilterOption,
} from '../../../types/sites.types';
import { pluralize } from '../../../utils/pluralize';

interface LocationFilterProps {
  value: LocationFilterSelection[];
  excludedLocationKeys: string[];
  options: LocationFilterOptions | null;
  loading: boolean;
  error: string | null;
  onChange: (value: LocationFilterSelection[]) => void;
  onExcludedLocationKeysChange: (value: string[]) => void;
}

const OTHER_LOCATION_SPECIAL_KEY = 'other' as const;
const UNKNOWN_LOCATION_SPECIAL_KEY = 'unknown' as const;

type LocationGroupSelection = Extract<LocationFilterSelection, { kind: 'group' }>;
type LocationLocationSelection = Extract<LocationFilterSelection, { kind: 'location' }>;
type LocationSpecialSelection = Extract<LocationFilterSelection, { kind: 'special' }>;
type LocationGroupLike = LocationGroupSelection | LocationGroupFilterOption;

interface LocationSearchResult {
  location: LocationFilterOption;
  groups: LocationGroupFilterOption[];
}

const EMPTY_GROUP_OPTIONS: LocationGroupFilterOption[] = [];
const EMPTY_LOCATION_OPTIONS: LocationFilterOption[] = [];

function getGroupSection(groupType: string): 'Regions' | 'Business groups' {
  return groupType.toLowerCase() === 'region' ? 'Regions' : 'Business groups';
}

function normalizeSearchTerm(value: string): string {
  return value.trim().toLowerCase();
}

function getLocationSearchText(location: LocationFilterOption): string {
  const aliases = [
    location.key === 'GB' ? 'uk' : '',
    location.key === 'US' ? 'usa' : '',
  ];

  return [location.key, location.displayName, ...aliases].join(' ').toLowerCase();
}

function getGroupSearchText(group: LocationGroupFilterOption): string {
  return [
    group.key,
    group.displayName,
    group.groupType,
    ...group.locations.map(getLocationSearchText),
  ]
    .join(' ')
    .toLowerCase();
}

function uniqueKeys(keys: string[]): string[] {
  return [...new Set(keys.filter((key) => key.trim() !== ''))];
}

function areStringArraysEqual(left: string[], right: string[]): boolean {
  return left.length === right.length && left.every((value, index) => value === right[index]);
}

function getGroupExcludedCount(group: LocationGroupLike, excludedKeys: Set<string>): number {
  return group.locations?.filter((location) => excludedKeys.has(location.key)).length ?? 0;
}

function formatLocationSelectionLabel(
  option: LocationFilterSelection,
  excludedKeys: Set<string>
): string {
  if (option.kind === 'group') {
    const excludedCount = getGroupExcludedCount(option, excludedKeys);
    if (excludedCount > 0) {
      return `${option.displayName} - ${excludedCount} excluded`;
    }

    if (option.locationCount != null) {
      return `${option.displayName} - ${option.locationCount} ${pluralize(option.locationCount, 'location')}`;
    }
  }

  return option.displayName;
}

function toGroupSelection(group: LocationGroupLike): LocationGroupSelection {
  return {
    kind: 'group',
    key: group.key,
    displayName: group.displayName,
    groupType: group.groupType,
    locationCount: group.locationCount,
    locations: group.locations ?? [],
  };
}

function toLocationSelection(location: LocationFilterOption): LocationLocationSelection {
  return {
    kind: 'location',
    key: location.key,
    displayName: location.displayName,
  };
}

function buildSpecialOptions(options: LocationFilterOptions | null): LocationSpecialSelection[] {
  if (!options) return [];

  return [
    {
      kind: 'special',
      key: UNKNOWN_LOCATION_SPECIAL_KEY,
      displayName: options.special.unknown.displayName,
      locationKey: options.special.unknown.key,
    },
    ...(options.special.other
      ? [
          {
            kind: 'special' as const,
            key: OTHER_LOCATION_SPECIAL_KEY,
            displayName: options.special.other.displayName,
            locationKey: options.special.other.key,
          },
        ]
      : []),
  ];
}

function pruneExcludedLocationKeys(
  excludedKeys: string[],
  selections: LocationFilterSelection[]
): string[] {
  const selectedGroupLocationKeys = new Set(
    selections
      .filter((selection): selection is LocationGroupSelection => selection.kind === 'group')
      .flatMap((selection) => selection.locations?.map((location) => location.key) ?? [])
  );

  return excludedKeys.filter((key) => selectedGroupLocationKeys.has(key));
}

export function LocationFilter({
  value,
  excludedLocationKeys,
  options,
  loading,
  error,
  onChange,
  onExcludedLocationKeysChange,
}: Readonly<LocationFilterProps>) {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(() => new Set());
  const [searchValue, setSearchValue] = useState('');
  const open = Boolean(anchorEl);
  const deferredSearchValue = useDeferredValue(searchValue);
  const searchTerm = normalizeSearchTerm(deferredSearchValue);
  const searchActive = searchTerm.length > 0;
  const groups = options?.groups ?? EMPTY_GROUP_OPTIONS;
  const locations = options?.locations ?? EMPTY_LOCATION_OPTIONS;
  const unknownLocationKey = options?.special.unknown.key;
  const excludedKeys = useMemo(() => new Set(excludedLocationKeys), [excludedLocationKeys]);
  const groupOptionsByKey = useMemo(
    () => new Map(groups.map((group) => [group.key, group])),
    [groups]
  );
  const normalizedValue = useMemo<LocationFilterSelection[]>(
    () =>
      value.map((selection) => {
        if (selection.kind !== 'group') return selection;

        const option = groupOptionsByKey.get(selection.key);
        return {
          ...selection,
          locationCount: option?.locationCount ?? selection.locationCount,
          locations: option?.locations ?? selection.locations ?? [],
        };
      }),
    [groupOptionsByKey, value]
  );
  const selectedGroups = useMemo(
    () =>
      normalizedValue.filter(
        (selection): selection is LocationGroupSelection => selection.kind === 'group'
      ),
    [normalizedValue]
  );
  const selectedLocationKeys = useMemo(
    () =>
      new Set(
        normalizedValue
          .filter(
            (selection): selection is LocationLocationSelection => selection.kind === 'location'
          )
          .map((selection) => selection.key)
      ),
    [normalizedValue]
  );
  const selectedSpecialKeys = useMemo(
    () =>
      new Set(
        normalizedValue
          .filter(
            (selection): selection is LocationSpecialSelection => selection.kind === 'special'
          )
          .map((selection) => selection.key)
      ),
    [normalizedValue]
  );
  const sourceGroupsByLocationKey = useMemo(() => {
    const sources = new Map<string, LocationGroupSelection[]>();

    for (const group of selectedGroups) {
      for (const location of group.locations ?? []) {
        const currentSources = sources.get(location.key);
        if (currentSources) {
          currentSources.push(group);
        } else {
          sources.set(location.key, [group]);
        }
      }
    }

    return sources;
  }, [selectedGroups]);
  const allGroupsByLocationKey = useMemo(() => {
    const groupsByLocationKey = new Map<string, LocationGroupFilterOption[]>();

    for (const group of groups) {
      for (const location of group.locations) {
        const currentGroups = groupsByLocationKey.get(location.key);
        if (currentGroups) {
          currentGroups.push(group);
        } else {
          groupsByLocationKey.set(location.key, [group]);
        }
      }
    }

    return groupsByLocationKey;
  }, [groups]);
  const normalLocations = useMemo(
    () => locations.filter((location) => location.key !== unknownLocationKey),
    [locations, unknownLocationKey]
  );
  const specialOptions = useMemo(() => buildSpecialOptions(options), [options]);
  const regions = useMemo(
    () => groups.filter((group) => getGroupSection(group.groupType) === 'Regions'),
    [groups]
  );
  const businessGroups = useMemo(
    () => groups.filter((group) => getGroupSection(group.groupType) === 'Business groups'),
    [groups]
  );
  const searchableGroups = useMemo(
    () => groups.map((group) => ({ group, searchText: getGroupSearchText(group) })),
    [groups]
  );
  const searchableLocations = useMemo(
    () =>
      normalLocations.map((location) => ({
        location,
        searchText: getLocationSearchText(location),
      })),
    [normalLocations]
  );
  const searchableSpecialOptions = useMemo(
    () =>
      specialOptions.map((special) => ({
        special,
        searchText: [special.key, special.displayName, special.locationKey ?? '']
          .join(' ')
          .toLowerCase(),
      })),
    [specialOptions]
  );
  const searchResults = useMemo(() => {
    if (!searchActive) {
      return {
        groups: [] as LocationGroupFilterOption[],
        locations: [] as LocationSearchResult[],
        special: [] as LocationSpecialSelection[],
      };
    }

    const matchingGroups = searchableGroups
      .filter((item) => item.searchText.includes(searchTerm))
      .map((item) => item.group);
    const matchingLocations = searchableLocations
      .filter((item) => item.searchText.includes(searchTerm))
      .map((item) => ({
        location: item.location,
        groups: allGroupsByLocationKey.get(item.location.key) ?? [],
      }));
    const matchingSpecial = searchableSpecialOptions
      .filter((item) => item.searchText.includes(searchTerm))
      .map((item) => item.special);

    return {
      groups: matchingGroups,
      locations: matchingLocations,
      special: matchingSpecial,
    };
  }, [
    allGroupsByLocationKey,
    searchActive,
    searchableGroups,
    searchableLocations,
    searchableSpecialOptions,
    searchTerm,
  ]);

  const emitChange = (
    nextSelections: LocationFilterSelection[],
    nextExcludedKeys = excludedLocationKeys
  ) => {
    onChange(nextSelections);

    if (!areStringArraysEqual(nextExcludedKeys, excludedLocationKeys)) {
      onExcludedLocationKeysChange(nextExcludedKeys);
    }
  };

  const openPopover = (event: MouseEvent<HTMLElement>) => {
    if (error) return;
    setAnchorEl(event.currentTarget);
  };

  const closePopover = () => {
    setAnchorEl(null);
  };

  const toggleGroupExpanded = (groupKey: string) => {
    setExpandedGroups((current) => {
      const next = new Set(current);
      if (next.has(groupKey)) {
        next.delete(groupKey);
      } else {
        next.add(groupKey);
      }
      return next;
    });
  };

  const toggleGroupSelection = (group: LocationGroupSelection) => {
    const isSelected = selectedGroups.some((selection) => selection.key === group.key);
    const nextSelections = isSelected
      ? normalizedValue.filter((selection) => !(selection.kind === 'group' && selection.key === group.key))
      : [...normalizedValue, toGroupSelection(group)];
    const nextExcludedKeys = isSelected
      ? pruneExcludedLocationKeys(excludedLocationKeys, nextSelections)
      : excludedLocationKeys;

    emitChange(nextSelections, nextExcludedKeys);
  };

  const toggleSpecialSelection = (special: LocationSpecialSelection) => {
    const isSelected = selectedSpecialKeys.has(special.key);
    const nextSelections = isSelected
      ? normalizedValue.filter(
          (selection) => !(selection.kind === 'special' && selection.key === special.key)
        )
      : [...normalizedValue, special];

    emitChange(nextSelections);
  };

  const toggleLocationSelection = (location: LocationFilterOption) => {
    const sourceGroups = sourceGroupsByLocationKey.get(location.key) ?? [];
    const isExplicitlySelected = selectedLocationKeys.has(location.key);
    const isSelected =
      isExplicitlySelected || (sourceGroups.length > 0 && !excludedKeys.has(location.key));

    if (isSelected) {
      const nextSelections = normalizedValue.filter(
        (selection) => !(selection.kind === 'location' && selection.key === location.key)
      );
      const nextExcludedKeys =
        sourceGroups.length > 0
          ? uniqueKeys([...excludedLocationKeys, location.key])
          : excludedLocationKeys;

      emitChange(nextSelections, nextExcludedKeys);
      return;
    }

    const nextExcludedKeys = excludedLocationKeys.filter((key) => key !== location.key);
    const nextSelections =
      sourceGroups.length > 0 || isExplicitlySelected
        ? normalizedValue
        : [...normalizedValue, toLocationSelection(location)];

    emitChange(nextSelections, nextExcludedKeys);
  };

  const removeSelection = (selection: LocationFilterSelection) => {
    const nextSelections = normalizedValue.filter(
      (item) => !(item.kind === selection.kind && item.key === selection.key)
    );
    const nextExcludedKeys =
      selection.kind === 'group'
        ? pruneExcludedLocationKeys(excludedLocationKeys, nextSelections)
        : excludedLocationKeys;

    emitChange(nextSelections, nextExcludedKeys);
  };

  const resetGroupExclusions = (group: LocationGroupSelection) => {
    const groupLocationKeys = new Set(group.locations?.map((location) => location.key) ?? []);
    const nextExcludedKeys = excludedLocationKeys.filter((key) => !groupLocationKeys.has(key));
    emitChange(normalizedValue, nextExcludedKeys);
  };

  const isLocationSelected = (locationKey: string): boolean => {
    const sourceGroups = sourceGroupsByLocationKey.get(locationKey) ?? [];
    return selectedLocationKeys.has(locationKey) || (sourceGroups.length > 0 && !excludedKeys.has(locationKey));
  };

  const renderLocationRow = (
    location: LocationFilterOption,
    nested = false,
    groupMemberships: LocationGroupFilterOption[] = []
  ) => {
    const sourceGroups = sourceGroupsByLocationKey.get(location.key) ?? [];
    const selected = isLocationSelected(location.key);
    const sourceText = excludedKeys.has(location.key)
      ? 'Excluded from selected groups'
      : sourceGroups.length > 0
        ? `via ${sourceGroups.map((group) => group.displayName).join(', ')}`
        : selectedLocationKeys.has(location.key)
          ? 'Selected directly'
          : groupMemberships.length > 0
            ? `in ${groupMemberships.map((group) => group.displayName).join(', ')}`
          : null;

    return (
      <Box
        key={location.key}
        role="option"
        aria-selected={selected}
        onClick={() => toggleLocationSelection(location)}
        sx={{
          display: 'flex',
          alignItems: 'center',
          minHeight: 40,
          pl: nested ? 5 : 1,
          pr: 1,
          py: 0.5,
          cursor: 'pointer',
          '&:hover': { bgcolor: 'action.hover' },
        }}
      >
        <Checkbox checked={selected} size="small" sx={{ mr: 1, p: 0.25 }} />
        <Box sx={{ minWidth: 0 }}>
          <Typography variant="body2">{location.displayName}</Typography>
          {sourceText && (
            <Typography variant="caption" color="text.secondary">
              {sourceText}
            </Typography>
          )}
        </Box>
      </Box>
    );
  };

  const renderGroupRow = (groupOption: LocationGroupFilterOption) => {
    const group = toGroupSelection(groupOption);
    const isSelected = selectedGroups.some((selection) => selection.key === group.key);
    const isExpanded = expandedGroups.has(group.key);
    const excludedCount = getGroupExcludedCount(group, excludedKeys);
    const checked = isSelected && excludedCount === 0;
    const indeterminate = isSelected && excludedCount > 0;

    return (
      <Box key={group.key}>
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            minHeight: 44,
            px: 1,
            py: 0.5,
            '&:hover': { bgcolor: 'action.hover' },
          }}
        >
          <IconButton
            size="small"
            aria-label={isExpanded ? `Collapse ${group.displayName}` : `Expand ${group.displayName}`}
            onClick={() => toggleGroupExpanded(group.key)}
            sx={{ mr: 0.5 }}
          >
            {isExpanded ? <ExpandMoreIcon fontSize="small" /> : <ChevronRightIcon fontSize="small" />}
          </IconButton>
          <Checkbox
            checked={checked}
            indeterminate={indeterminate}
            size="small"
            onChange={() => toggleGroupSelection(group)}
            sx={{ mr: 1, p: 0.25 }}
          />
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography variant="body2">{group.displayName}</Typography>
            <Typography variant="caption" color="text.secondary">
              {group.locationCount ?? group.locations?.length ?? 0}{' '}
              {pluralize(group.locationCount ?? group.locations?.length ?? 0, 'location')}
            </Typography>
          </Box>
          {excludedCount > 0 && (
            <Button
              size="small"
              color="inherit"
              onClick={() => resetGroupExclusions(group)}
              sx={{ textTransform: 'none', color: 'text.secondary', minWidth: 'auto' }}
            >
              Reset
            </Button>
          )}
        </Box>
        <Collapse in={isExpanded} timeout="auto" unmountOnExit>
          {(group.locations ?? []).map((location) => renderLocationRow(location, true))}
        </Collapse>
      </Box>
    );
  };

  const renderSpecialRow = (special: LocationSpecialSelection) => {
    const selected = selectedSpecialKeys.has(special.key);

    return (
      <Box
        key={special.key}
        role="option"
        aria-selected={selected}
        onClick={() => toggleSpecialSelection(special)}
        sx={{
          display: 'flex',
          alignItems: 'center',
          minHeight: 40,
          px: 1,
          py: 0.5,
          cursor: 'pointer',
          '&:hover': { bgcolor: 'action.hover' },
        }}
      >
        <Checkbox checked={selected} size="small" sx={{ mr: 1, p: 0.25 }} />
        <Typography variant="body2">{special.displayName}</Typography>
      </Box>
    );
  };

  const renderSection = (title: string, content: ReactNode) => (
    <Box>
      <Typography
        variant="caption"
        color="text.secondary"
        sx={{ display: 'block', px: 1.5, pt: 1, pb: 0.5, fontWeight: 700 }}
      >
        {title}
      </Typography>
      {content}
    </Box>
  );

  const renderSearchResults = () => {
    const resultCount =
      searchResults.groups.length + searchResults.locations.length + searchResults.special.length;

    if (resultCount === 0) {
      return (
        <Typography variant="body2" color="text.secondary" sx={{ px: 1.5, py: 2 }}>
          No locations or groups found
        </Typography>
      );
    }

    return (
      <>
        {searchResults.locations.length > 0 &&
          renderSection(
            'Locations',
            searchResults.locations.map((result) =>
              renderLocationRow(result.location, false, result.groups)
            )
          )}
        {searchResults.groups.length > 0 &&
          renderSection('Groups', searchResults.groups.map((group) => renderGroupRow(group)))}
        {searchResults.special.length > 0 &&
          renderSection('Special', searchResults.special.map((special) => renderSpecialRow(special)))}
      </>
    );
  };

  return (
    <Box sx={{ flex: 1, minWidth: '200px', maxWidth: '350px' }}>
      <Typography variant="subtitle2" gutterBottom>
        Location
      </Typography>
      <Box
        role="button"
        tabIndex={error ? -1 : 0}
        aria-expanded={open}
        onClick={openPopover}
        onKeyDown={(event) => {
          if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            if (!error) setAnchorEl(event.currentTarget);
          }
        }}
        sx={{
          minHeight: 40,
          display: 'flex',
          alignItems: 'center',
          gap: 0.5,
          px: 1,
          py: 0.5,
          border: '1px solid',
          borderColor: error ? 'error.main' : open ? 'primary.main' : 'divider',
          borderRadius: (theme) => `${theme.custom.radius}px`,
          bgcolor: error ? 'action.disabledBackground' : 'background.paper',
          cursor: error ? 'default' : 'pointer',
        }}
      >
        <Stack direction="row" spacing={0.5} useFlexGap flexWrap="wrap" sx={{ flex: 1, minWidth: 0 }}>
          {normalizedValue.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              Select locations
            </Typography>
          ) : (
            normalizedValue.map((selection) => (
              <Chip
                key={`${selection.kind}:${selection.key}`}
                label={formatLocationSelectionLabel(selection, excludedKeys)}
                size="small"
                onDelete={(event) => {
                  event.stopPropagation();
                  removeSelection(selection);
                }}
                onMouseDown={(event) => event.stopPropagation()}
              />
            ))
          )}
        </Stack>
        {loading ? <CircularProgress color="inherit" size={18} /> : <ArrowDropDownIcon fontSize="small" />}
      </Box>
      {error && (
        <Alert severity="warning" sx={{ mt: 1 }}>
          Location filter is unavailable.
        </Alert>
      )}
      {open && (
        <Popover
          open={open}
          anchorEl={anchorEl}
          onClose={closePopover}
          anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
          transformOrigin={{ vertical: 'top', horizontal: 'left' }}
          slotProps={{
            paper: {
              sx: {
                mt: 0.5,
                width: anchorEl?.clientWidth ?? 350,
                maxWidth: 420,
                maxHeight: 420,
                overflow: 'auto',
              },
            },
          }}
        >
          <Box
            sx={{
              position: 'sticky',
              top: 0,
              zIndex: 1,
              bgcolor: 'background.paper',
              borderBottom: '1px solid',
              borderColor: 'divider',
              p: 1,
            }}
          >
            <TextField
              size="small"
              fullWidth
              placeholder="Search locations or groups"
              value={searchValue}
              onChange={(event) => setSearchValue(event.target.value)}
              onClick={(event) => event.stopPropagation()}
              InputProps={{
                startAdornment: (
                  <SearchIcon fontSize="small" sx={{ mr: 1, color: 'text.secondary' }} />
                ),
                endAdornment: searchValue ? (
                  <IconButton
                    aria-label="Clear location search"
                    edge="end"
                    size="small"
                    onClick={() => setSearchValue('')}
                    sx={{ color: 'text.secondary' }}
                  >
                    <ClearIcon fontSize="small" />
                  </IconButton>
                ) : undefined,
              }}
            />
          </Box>
          {searchActive ? (
            renderSearchResults()
          ) : (
            <>
              {renderSection('Regions', regions.map((group) => renderGroupRow(group)))}
              {renderSection(
                'Business groups',
                businessGroups.map((group) => renderGroupRow(group))
              )}
              {renderSection(
                'Locations',
                normalLocations.map((location) => renderLocationRow(location))
              )}
              {renderSection('Special', specialOptions.map((special) => renderSpecialRow(special)))}
            </>
          )}
        </Popover>
      )}
    </Box>
  );
}
