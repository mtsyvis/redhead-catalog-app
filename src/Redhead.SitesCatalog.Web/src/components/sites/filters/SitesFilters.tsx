import { useState, useEffect, useMemo, useRef } from 'react';
import type { MouseEvent } from 'react';
import {
  Box,
  TextField,
  MenuItem,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Typography,
  Stack,
  Autocomplete,
  Chip,
  Checkbox,
  Button,
  IconButton,
  ToggleButton,
  ToggleButtonGroup,
} from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import type {
  FilterOption,
  LocationFilterOptions,
  ServiceAvailabilityFilter,
  SitesFilters,
  TopicFitMode,
} from '../../../types/sites.types';
import { sitesService } from '../../../services/sites.service';
import { BrandButton } from '../../common/BrandButton';
import {
  SERVICE_AVAILABILITY_FILTER_OPTIONS,
  normalizeServiceAvailabilityFilter,
} from '../../../utils/serviceAvailability';
import { LANGUAGE_OPTIONS, getLanguageOption } from '../../../utils/language';
import { LastPublishedRangeFilter } from './LastPublishedRangeFilter';
import { StopListDialog } from '../dialogs/StopListDialog';
import { pluralize } from '../../../utils/pluralize';
import {
  CategoriesSearchFilter,
  type CategoriesSearchFilterHandle,
} from './CategoriesSearchFilter';
import { LocationFilter } from './LocationFilter';
import {
  ActiveFiltersSummary,
} from './ActiveFiltersSummary';
import { buildAdvancedActiveFilterSummaries } from './ActiveFiltersSummary.helpers';

interface SitesFiltersProps {
  filters: SitesFilters;
  onFiltersChange: (filters: SitesFilters) => void;
  onApply: (filters?: SitesFilters) => void;
  multiSearchMode?: boolean;
  onMultiSearchModeChange?: (enabled: boolean) => void;
  canFilterQuarantine?: boolean;
  filterOptionsRefreshKey?: number;
}

const INITIAL_FILTERS: SitesFilters = {
  search: '',
  drMin: '',
  drMax: '',
  trafficMin: '',
  trafficMax: '',
  priceMin: '',
  priceMax: '',
  stopListDomains: [],
  locationSelections: [],
  excludedLocationKeys: [],
  niches: [],
  categorySearchTerms: [],
  topicFitMode: 'expand',
  excludedNiches: [],
  excludedCategorySearchTerms: [],
  languages: [],
  casinoAvailability: [],
  cryptoAvailability: [],
  linkInsertAvailability: [],
  linkInsertCasinoAvailability: [],
  datingAvailability: [],
  quarantine: 'exclude',
  lastPublishedFromMonth: null,
  lastPublishedToMonth: null,
};

const FILTER_GROUP_GAP = 2.5;

function areStringArraysEqual(left: string[], right: string[]): boolean {
  return left.length === right.length && left.every((value, index) => value === right[index]);
}

function hasAvailabilityFilter(filter: ServiceAvailabilityFilter): boolean {
  return normalizeServiceAvailabilityFilter(filter).length > 0;
}

function renderAvailabilityValue(
  selectedOptions: typeof SERVICE_AVAILABILITY_FILTER_OPTIONS
): string {
  if (selectedOptions.length === 0) return '';
  if (selectedOptions.length <= 2) {
    return selectedOptions.map((option) => option.label).join(', ');
  }
  return `${selectedOptions.length} selected`;
}

interface OptionalServiceAvailabilitySelectProps {
  label: string;
  value: ServiceAvailabilityFilter;
  onChange: (value: ServiceAvailabilityFilter) => void;
}

function OptionalServiceAvailabilitySelect({
  label,
  value,
  onChange,
}: Readonly<OptionalServiceAvailabilitySelectProps>) {
  const normalizedValue = normalizeServiceAvailabilityFilter(value);
  const selectedOptions = SERVICE_AVAILABILITY_FILTER_OPTIONS.filter((option) =>
    normalizedValue.includes(option.value)
  );

  return (
    <Autocomplete
      multiple
      size="small"
      options={SERVICE_AVAILABILITY_FILTER_OPTIONS}
      value={selectedOptions}
      onChange={(_, nextOptions) => onChange(nextOptions.map((option) => option.value))}
      getOptionLabel={(option) => option.label}
      isOptionEqualToValue={(option, selected) => option.value === selected.value}
      disableCloseOnSelect
      clearOnBlur={false}
      renderTags={(selected) => (
        <Typography variant="body2" noWrap sx={{ minWidth: 0 }}>
          {renderAvailabilityValue(selected)}
        </Typography>
      )}
      renderOption={(props, option, { selected }) => {
        const { key, ...optionProps } = props;
        return (
          <li key={key} {...optionProps}>
            <Checkbox checked={selected} size="small" sx={{ mr: 1, p: 0.25 }} />
            {option.label}
          </li>
        );
      }}
      renderInput={(params) => (
        <TextField
          {...params}
          label={label}
          placeholder={normalizedValue.length === 0 ? 'All' : ''}
        />
      )}
      sx={{ width: 185 }}
    />
  );
}

export function SitesFilters({
  filters,
  onFiltersChange,
  onApply,
  multiSearchMode = false,
  onMultiSearchModeChange,
  filterOptionsRefreshKey = 0,
}: SitesFiltersProps) {
  const [locationOptions, setLocationOptions] = useState<LocationFilterOptions | null>(null);
  const [locationOptionsLoading, setLocationOptionsLoading] = useState(false);
  const [locationOptionsError, setLocationOptionsError] = useState<string | null>(null);
  const [nicheOptions, setNicheOptions] = useState<FilterOption[]>([]);
  const [expanded, setExpanded] = useState(false);
  const [stopListDialogOpen, setStopListDialogOpen] = useState(false);
  const categoriesSearchFilterRef = useRef<CategoriesSearchFilterHandle>(null);
  const excludedCategoriesSearchFilterRef = useRef<CategoriesSearchFilterHandle>(null);

  useEffect(() => {
    const loadFilterOptions = async () => {
      setLocationOptionsLoading(true);
      setLocationOptionsError(null);
      try {
        const data = await sitesService.getFilterOptions();
        setNicheOptions(data.niches);
        setLocationOptions(data.locations ?? null);
      } catch (error) {
        console.error('Failed to load filter options:', error);
        setLocationOptions(null);
        setLocationOptionsError('Location options could not be loaded.');
      } finally {
        setLocationOptionsLoading(false);
      }
    };

    loadFilterOptions();
  }, [filterOptionsRefreshKey]);

  const handleChange = <K extends keyof SitesFilters>(
    field: K,
    value: SitesFilters[K]
  ) => {
    onFiltersChange({ ...filters, [field]: value });
  };

  const handleClear = () => {
    onFiltersChange(INITIAL_FILTERS);
    onApply(INITIAL_FILTERS);
  };

  const handleClearAdvancedFilters = () => {
    onFiltersChange({
      ...filters,
      drMin: INITIAL_FILTERS.drMin,
      drMax: INITIAL_FILTERS.drMax,
      trafficMin: INITIAL_FILTERS.trafficMin,
      trafficMax: INITIAL_FILTERS.trafficMax,
      priceMin: INITIAL_FILTERS.priceMin,
      priceMax: INITIAL_FILTERS.priceMax,
      stopListDomains: multiSearchMode ? filters.stopListDomains : INITIAL_FILTERS.stopListDomains,
      locationSelections: INITIAL_FILTERS.locationSelections,
      excludedLocationKeys: INITIAL_FILTERS.excludedLocationKeys,
      niches: INITIAL_FILTERS.niches,
      categorySearchTerms: INITIAL_FILTERS.categorySearchTerms,
      topicFitMode: INITIAL_FILTERS.topicFitMode,
      excludedNiches: INITIAL_FILTERS.excludedNiches,
      excludedCategorySearchTerms: INITIAL_FILTERS.excludedCategorySearchTerms,
      languages: INITIAL_FILTERS.languages,
      casinoAvailability: INITIAL_FILTERS.casinoAvailability,
      cryptoAvailability: INITIAL_FILTERS.cryptoAvailability,
      linkInsertAvailability: INITIAL_FILTERS.linkInsertAvailability,
      linkInsertCasinoAvailability: INITIAL_FILTERS.linkInsertCasinoAvailability,
      datingAvailability: INITIAL_FILTERS.datingAvailability,
      quarantine: INITIAL_FILTERS.quarantine,
      lastPublishedFromMonth: INITIAL_FILTERS.lastPublishedFromMonth,
      lastPublishedToMonth: INITIAL_FILTERS.lastPublishedToMonth,
    });
  };

  const handleClearAdvancedFiltersClick = (event: MouseEvent<HTMLButtonElement>) => {
    event.stopPropagation();
    handleClearAdvancedFilters();
  };

  const handleApply = () => {
    const categorySearchTerms =
      categoriesSearchFilterRef.current?.commitPendingInput() ?? filters.categorySearchTerms;
    const excludedCategorySearchTerms =
      excludedCategoriesSearchFilterRef.current?.commitPendingInput() ??
      filters.excludedCategorySearchTerms;
    const nextFilters =
      areStringArraysEqual(categorySearchTerms, filters.categorySearchTerms) &&
      areStringArraysEqual(excludedCategorySearchTerms, filters.excludedCategorySearchTerms)
        ? filters
        : { ...filters, categorySearchTerms, excludedCategorySearchTerms };

    if (nextFilters !== filters) {
      onFiltersChange(nextFilters);
    }
    onApply(nextFilters);
  };

  const getAdvancedActiveFilterCount = () => {
    let count = 0;

    if (filters.drMin !== '' || filters.drMax !== '') count += 1;
    if (filters.trafficMin !== '' || filters.trafficMax !== '') count += 1;
    if (filters.priceMin !== '' || filters.priceMax !== '') count += 1;
    if (!multiSearchMode && filters.stopListDomains.length > 0) count += 1;
    if (filters.locationSelections.length > 0 || filters.excludedLocationKeys.length > 0) count += 1;
    if (filters.niches.length > 0) count += 1;
    if (filters.categorySearchTerms.length > 0) count += 1;
    if (filters.niches.length > 0 && filters.categorySearchTerms.length > 0) count += 1;
    if (filters.excludedNiches.length > 0) count += 1;
    if (filters.excludedCategorySearchTerms.length > 0) count += 1;
    if (filters.languages.length > 0) count += 1;
    if (hasAvailabilityFilter(filters.casinoAvailability)) count += 1;
    if (hasAvailabilityFilter(filters.cryptoAvailability)) count += 1;
    if (hasAvailabilityFilter(filters.linkInsertAvailability)) count += 1;
    if (hasAvailabilityFilter(filters.linkInsertCasinoAvailability)) count += 1;
    if (hasAvailabilityFilter(filters.datingAvailability)) count += 1;
    if (filters.quarantine !== INITIAL_FILTERS.quarantine) count += 1;
    if (filters.lastPublishedFromMonth !== null || filters.lastPublishedToMonth !== null) {
      count += 1;
    }

    return count;
  };

  const hasSearchOrAdvancedFilters = () => {
    return (
      filters.search !== '' ||
      filters.drMin !== '' ||
      filters.drMax !== '' ||
      filters.trafficMin !== '' ||
      filters.trafficMax !== '' ||
      filters.priceMin !== '' ||
      filters.priceMax !== '' ||
      (!multiSearchMode && filters.stopListDomains.length > 0) ||
      filters.locationSelections.length > 0 ||
      filters.excludedLocationKeys.length > 0 ||
      filters.niches.length > 0 ||
      filters.categorySearchTerms.length > 0 ||
      filters.excludedNiches.length > 0 ||
      filters.excludedCategorySearchTerms.length > 0 ||
      filters.languages.length > 0 ||
      hasAvailabilityFilter(filters.casinoAvailability) ||
      hasAvailabilityFilter(filters.cryptoAvailability) ||
      hasAvailabilityFilter(filters.linkInsertAvailability) ||
      hasAvailabilityFilter(filters.linkInsertCasinoAvailability) ||
      hasAvailabilityFilter(filters.datingAvailability) ||
      filters.quarantine !== INITIAL_FILTERS.quarantine ||
      filters.lastPublishedFromMonth !== null ||
      filters.lastPublishedToMonth !== null
    );
  };

  const lastPublishedRangeError =
    filters.lastPublishedFromMonth &&
    filters.lastPublishedToMonth &&
    filters.lastPublishedFromMonth > filters.lastPublishedToMonth
      ? '"From" must be before or equal to "To"'
      : undefined;

  const selectedNicheOptions = filters.niches.map(
    (value) => nicheOptions.find((option) => option.value === value) ?? { value, label: value }
  );
  const selectedExcludedNicheOptions = filters.excludedNiches.map(
    (value) => nicheOptions.find((option) => option.value === value) ?? { value, label: value }
  );
  const selectedLanguageOptions = filters.languages.map(
    (value) => getLanguageOption(value) ?? { value, label: value }
  );

  const stopListCount = filters.stopListDomains.length;
  const stopListPaused = multiSearchMode && stopListCount > 0;
  const stopListApplied = !multiSearchMode && stopListCount > 0;
  const advancedActiveFilterCount = getAdvancedActiveFilterCount();
  const advancedFiltersActive = advancedActiveFilterCount > 0;
  const activeFilterSummaryItems = useMemo(
    () => buildAdvancedActiveFilterSummaries(filters, multiSearchMode),
    [filters, multiSearchMode]
  );
  const stopListStatusText =
    stopListCount === 0
      ? 'No domains excluded'
      : stopListPaused
        ? `${stopListCount} ${pluralize(stopListCount, 'domain')} saved`
        : `${stopListCount} ${pluralize(stopListCount, 'domain')} excluded`;

  const handleOpenStopListDialog = () => {
    setStopListDialogOpen(true);
  };

  const handleCancelStopListDialog = () => {
    setStopListDialogOpen(false);
  };

  const handleApplyStopList = (domains: string[]) => {
    handleChange('stopListDomains', domains);
    setStopListDialogOpen(false);
  };

  const handleClearStopList = () => {
    handleChange('stopListDomains', []);
    setStopListDialogOpen(false);
  };

  const handleClearSearch = () => {
    if (filters.search === '') return;

    const nextFilters = { ...filters, search: '' };
    onFiltersChange(nextFilters);

    if (!multiSearchMode) {
      onApply(nextFilters);
    }
  };

  const searchModeControl = onMultiSearchModeChange ? (
    <ToggleButtonGroup
      exclusive
      size="small"
      value={multiSearchMode ? 'multi' : 'single'}
      onChange={(_, value: 'single' | 'multi' | null) => {
        if (value === null) return;
        onMultiSearchModeChange(value === 'multi');
      }}
      aria-label="Search mode"
      sx={{
        flex: multiSearchMode ? '0 0 auto' : { xs: '1 1 100%', md: '0 0 auto' },
        maxWidth: { xs: '100%', md: 'none' },
        borderRadius: 999,
        bgcolor: 'background.paper',
        '& .MuiToggleButton-root': {
          px: 1.75,
          py: 0.75,
          minHeight: 38,
          textTransform: 'none',
          borderColor: 'divider',
          color: 'text.primary',
          bgcolor: 'background.paper',
          lineHeight: 1.2,
          '&:first-of-type': {
            borderTopLeftRadius: 999,
            borderBottomLeftRadius: 999,
          },
          '&:last-of-type': {
            borderTopRightRadius: 999,
            borderBottomRightRadius: 999,
          },
          '&.Mui-selected': {
            color: 'primary.main',
            bgcolor: 'rgba(255, 69, 91, 0.08)',
            borderColor: 'rgba(255, 69, 91, 0.35)',
            fontWeight: 600,
            '&:hover': {
              bgcolor: 'rgba(255, 69, 91, 0.12)',
            },
          },
          '&:not(.Mui-selected)': {
            '&:hover': {
              bgcolor: 'action.hover',
            },
          },
        },
      }}
    >
      <ToggleButton value="single">Single search</ToggleButton>
      <ToggleButton value="multi">Multi-search</ToggleButton>
    </ToggleButtonGroup>
  ) : null;

  const searchInput = (
    <TextField
      fullWidth
      multiline={multiSearchMode}
      minRows={multiSearchMode ? 3 : 1}
      maxRows={multiSearchMode ? 8 : 1}
      placeholder={
        multiSearchMode
          ? 'Paste domains or URLs (one per line or space-separated, max 500)'
          : 'Search by domain (example.com or https://www.example.com/path)'
      }
      value={filters.search}
      onChange={(e) => handleChange('search', e.target.value)}
      onKeyDown={(e) => {
        if (e.key === 'Enter' && !multiSearchMode) {
          e.preventDefault();
          handleApply();
        }
      }}
      InputProps={{
        startAdornment: <SearchIcon sx={{ mr: 1, color: 'text.secondary' }} />,
        endAdornment: filters.search ? (
          <IconButton
            aria-label="Clear search"
            edge="end"
            size="small"
            onMouseDown={(e) => e.preventDefault()}
            onClick={handleClearSearch}
            sx={{ color: 'text.secondary' }}
          >
            <ClearIcon fontSize="small" />
          </IconButton>
        ) : undefined,
      }}
      sx={multiSearchMode ? undefined : { flex: '1 1 360px', minWidth: { xs: '100%', md: 320 } }}
    />
  );

  return (
    <Box sx={{ mb: 1.5 }}>
      {/* Search Bar */}
      <Box sx={{ mb: 1 }}>
        {multiSearchMode ? (
          <Stack spacing={0.75}>
            {searchModeControl}
            {searchInput}
            <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
              <BrandButton kind="primary" onClick={handleApply} sx={{ minWidth: 120 }}>
                Search
              </BrandButton>
            </Box>
          </Stack>
        ) : (
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1.25,
              flexWrap: 'wrap',
            }}
          >
            {searchModeControl}
            {searchInput}
          </Box>
        )}
      </Box>

      {/* Advanced Filters */}
      <Accordion expanded={expanded} onChange={() => setExpanded(!expanded)}>
        <AccordionSummary
          expandIcon={<ExpandMoreIcon />}
          sx={{
            minHeight: 44,
            '&.Mui-expanded': { minHeight: 44 },
            '& .MuiAccordionSummary-content': {
              minWidth: 0,
              my: 0.75,
              alignItems: 'center',
            },
            '& .MuiAccordionSummary-content.Mui-expanded': {
              my: 0.75,
            },
          }}
        >
          <Stack
            direction="row"
            spacing={1}
            alignItems="center"
            sx={{ minWidth: 0, width: '100%' }}
          >
            <Typography sx={{ flexShrink: 0, fontWeight: 500 }}>Advanced Filters</Typography>
            {advancedFiltersActive && (
              <Chip
                label={`${advancedActiveFilterCount} active`}
                size="small"
                variant="outlined"
                sx={{
                  height: 24,
                  borderColor: 'divider',
                  bgcolor: 'background.paper',
                  color: 'text.secondary',
                  fontWeight: 600,
                  flexShrink: 0,
                }}
              />
            )}
            {!expanded && activeFilterSummaryItems.length > 0 && (
              <ActiveFiltersSummary items={activeFilterSummaryItems} />
            )}
            {!expanded && advancedFiltersActive && (
              <Button
                size="small"
                variant="outlined"
                color="inherit"
                onMouseDown={(event) => event.stopPropagation()}
                onClick={handleClearAdvancedFiltersClick}
                sx={{
                  flexShrink: 0,
                  minWidth: 'auto',
                  height: 24,
                  px: 1.25,
                  py: 0,
                  borderRadius: 999,
                  borderColor: 'divider',
                  bgcolor: 'background.paper',
                  color: 'text.secondary',
                  fontSize: 12,
                  fontWeight: 600,
                  lineHeight: 1,
                  textTransform: 'none',
                  whiteSpace: 'nowrap',
                  '&:hover': {
                    borderColor: 'rgba(255, 69, 91, 0.32)',
                    bgcolor: 'rgba(255, 69, 91, 0.06)',
                  },
                }}
              >
                Clear filters
              </Button>
            )}
          </Stack>
        </AccordionSummary>
        <AccordionDetails sx={{ pt: 1.5 }}>
          <Stack spacing={2}>
            {stopListApplied && !expanded && (
              <Box sx={{ display: 'inline-flex' }}>
                <Chip
                  label={`Stop list: ${stopListCount} ${pluralize(stopListCount, 'domain')}`}
                  onDelete={handleClearStopList}
                  variant="outlined"
                  sx={{
                    borderColor: 'divider',
                    color: 'text.primary',
                    bgcolor: 'background.paper',
                    '& .MuiChip-deleteIcon': {
                      color: 'text.secondary',
                      '&:hover': { color: 'text.primary' },
                    },
                  }}
                />
              </Box>
            )}

            {/* Range Filters Row */}
            <Box sx={{ display: 'flex', columnGap: FILTER_GROUP_GAP, rowGap: 2, flexWrap: 'wrap' }}>
              {/* DR Range */}
              <Box sx={{ flex: 1, minWidth: '200px' }}>
                <Typography variant="subtitle2" gutterBottom>
                  Domain Rating (DR)
                </Typography>
                <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
                  <TextField
                    size="small"
                    label="Min"
                    type="number"
                    value={filters.drMin}
                    onChange={(e) => handleChange('drMin', e.target.value)}
                    slotProps={{ htmlInput: { min: 0, max: 100 } }}
                    sx={{ flex: 1 }}
                  />
                  <Typography>—</Typography>
                  <TextField
                    size="small"
                    label="Max"
                    type="number"
                    value={filters.drMax}
                    onChange={(e) => handleChange('drMax', e.target.value)}
                    slotProps={{ htmlInput: { min: 0, max: 100 } }}
                    sx={{ flex: 1 }}
                  />
                </Box>
              </Box>

              {/* Traffic Range */}
              <Box sx={{ flex: 1, minWidth: '200px' }}>
                <Typography variant="subtitle2" gutterBottom>
                  Traffic
                </Typography>
                <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
                  <TextField
                    size="small"
                    label="Min"
                    type="number"
                    value={filters.trafficMin}
                    onChange={(e) => handleChange('trafficMin', e.target.value)}
                    slotProps={{ htmlInput: { min: 0 } }}
                    sx={{ flex: 1 }}
                  />
                  <Typography>—</Typography>
                  <TextField
                    size="small"
                    label="Max"
                    type="number"
                    value={filters.trafficMax}
                    onChange={(e) => handleChange('trafficMax', e.target.value)}
                    slotProps={{ htmlInput: { min: 0 } }}
                    sx={{ flex: 1 }}
                  />
                </Box>
              </Box>

              {/* Price Range (USD) */}
              <Box sx={{ flex: 1, minWidth: '200px' }}>
                <Typography variant="subtitle2" gutterBottom>
                  Price (USD)
                </Typography>
                <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
                  <TextField
                    size="small"
                    label="Min"
                    type="number"
                    value={filters.priceMin}
                    onChange={(e) => handleChange('priceMin', e.target.value)}
                    slotProps={{ htmlInput: { min: 0 } }}
                    sx={{ flex: 1 }}
                  />
                  <Typography>—</Typography>
                  <TextField
                    size="small"
                    label="Max"
                    type="number"
                    value={filters.priceMax}
                    onChange={(e) => handleChange('priceMax', e.target.value)}
                    slotProps={{ htmlInput: { min: 0 } }}
                    sx={{ flex: 1 }}
                  />
                </Box>
              </Box>

              <LocationFilter
                value={filters.locationSelections}
                excludedLocationKeys={filters.excludedLocationKeys}
                options={locationOptions}
                loading={locationOptionsLoading}
                error={locationOptionsError}
                onChange={(value) => handleChange('locationSelections', value)}
                onExcludedLocationKeysChange={(value) => handleChange('excludedLocationKeys', value)}
              />
            </Box>

            {/* Row 2: Last Publication + Quarantine */}
            <Box sx={{ display: 'flex', columnGap: FILTER_GROUP_GAP, rowGap: 2, flexWrap: 'wrap', alignItems: 'flex-start' }}>
              <LastPublishedRangeFilter
                fromValue={filters.lastPublishedFromMonth}
                toValue={filters.lastPublishedToMonth}
                onFromChange={(v) => handleChange('lastPublishedFromMonth', v)}
                onToChange={(v) => handleChange('lastPublishedToMonth', v)}
                error={lastPublishedRangeError}
              />

              {/* Quarantine Filter */}
              <Box sx={{ flex: '0 0 auto', minWidth: '185px' }}>
                <Typography variant="subtitle2" gutterBottom>
                  Quarantine Status
                </Typography>
                <TextField
                  select
                  size="small"
                  value={filters.quarantine}
                  onChange={(e) =>
                    handleChange('quarantine', e.target.value as 'all' | 'only' | 'exclude')
                  }
                  sx={{ width: 185 }}
                >
                  <MenuItem value="all">All Sites</MenuItem>
                  <MenuItem value="exclude">Available Only</MenuItem>
                  <MenuItem value="only">Unavailable Only</MenuItem>
                </TextField>
              </Box>
              {/* Language Multi-Select */}
              <Box sx={{ flex: 1, minWidth: '200px', maxWidth: '350px' }}>
                <Typography variant="subtitle2" gutterBottom>
                  Language
                </Typography>
                <Autocomplete
                  multiple
                  size="small"
                  options={LANGUAGE_OPTIONS}
                  value={selectedLanguageOptions}
                  getOptionLabel={(option) => option.label}
                  isOptionEqualToValue={(option, value) => option.value === value.value}
                  onChange={(_, newValue) =>
                    handleChange('languages', newValue.map((option) => option.value))
                  }
                  renderInput={(params) => (
                    <TextField
                      {...params}
                      placeholder={filters.languages.length === 0 ? 'Select languages' : ''}
                    />
                  )}
                  disableCloseOnSelect
                  limitTags={2}
                />
              </Box>

            </Box>

            {/* Row 3: Topic fit */}
            <Box
              sx={{
                display: 'flex',
                flexDirection: 'column',
                gap: 1.5,
                borderTop: '1px solid',
                borderBottom: '1px solid',
                borderColor: 'divider',
                pt: 2,
                pb: 2,
              }}
            >
              <Box
                sx={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  columnGap: 2,
                  rowGap: 1,
                  flexWrap: 'wrap',
                  alignItems: 'center',
                }}
              >
                <Typography variant="subtitle2">Topic fit</Typography>
                <ToggleButtonGroup
                  exclusive
                  size="small"
                  value={filters.topicFitMode}
                  sx={{
                    '& .MuiToggleButton-root': {
                      minHeight: 32,
                      px: 1.25,
                      py: 0.5,
                      textTransform: 'none',
                    },
                  }}
                  onChange={(_, value) => {
                    if (value) {
                      handleChange('topicFitMode', value as TopicFitMode);
                    }
                  }}
                >
                  <ToggleButton value="expand">Expand: OR</ToggleButton>
                  <ToggleButton value="narrow">Narrow: AND</ToggleButton>
                </ToggleButtonGroup>
              </Box>

              <Box
                sx={{
                  display: 'grid',
                  gridTemplateColumns: { xs: '1fr', md: 'repeat(2, minmax(300px, 1fr))' },
                  columnGap: FILTER_GROUP_GAP,
                  rowGap: 1,
                  alignItems: 'flex-start',
                }}
              >
                <Typography variant="subtitle2" sx={{ order: { xs: 1, md: 1 } }}>
                  Niche
                </Typography>
                <Typography variant="subtitle2" sx={{ order: { xs: 4, md: 2 } }}>
                  Categories
                </Typography>
                <Box sx={{ minWidth: 0, order: { xs: 2, md: 3 } }}>
                  <Autocomplete
                    multiple
                    size="small"
                    options={nicheOptions}
                    value={selectedNicheOptions}
                    getOptionLabel={(option) => option.label}
                    isOptionEqualToValue={(option, value) => option.value === value.value}
                    onChange={(_, newValue) =>
                      handleChange('niches', newValue.map((option) => option.value))
                    }
                    renderInput={(params) => (
                      <TextField
                        {...params}
                        label="Include niches"
                        placeholder={filters.niches.length === 0 ? 'Select niches' : ''}
                      />
                    )}
                    disableCloseOnSelect
                  />
                </Box>
                <Box sx={{ minWidth: 0, order: { xs: 5, md: 4 } }}>
                  <CategoriesSearchFilter
                    ref={categoriesSearchFilterRef}
                    title={null}
                    inputLabel="Include categories"
                    placeholder="Search categories: travel blog, sports betting, crypto"
                    helperText={null}
                    value={filters.categorySearchTerms}
                    onChange={(terms) => handleChange('categorySearchTerms', terms)}
                  />
                </Box>
                <Box sx={{ minWidth: 0, mt: 0.75, order: { xs: 3, md: 5 } }}>
                  <Autocomplete
                    multiple
                    size="small"
                    options={nicheOptions}
                    value={selectedExcludedNicheOptions}
                    getOptionLabel={(option) => option.label}
                    isOptionEqualToValue={(option, value) => option.value === value.value}
                    onChange={(_, newValue) =>
                      handleChange('excludedNiches', newValue.map((option) => option.value))
                    }
                    renderInput={(params) => (
                      <TextField
                        {...params}
                        label="Exclude niches"
                        placeholder={
                          filters.excludedNiches.length === 0 ? 'Select niches to exclude' : ''
                        }
                      />
                    )}
                    disableCloseOnSelect
                  />
                </Box>
                <Box sx={{ minWidth: 0, mt: 0.75, order: { xs: 6, md: 6 } }}>
                  <CategoriesSearchFilter
                    ref={excludedCategoriesSearchFilterRef}
                    title={null}
                    inputLabel="Exclude categories"
                    placeholder="Exclude categories: gambling, adult, betting"
                    helperText={null}
                    value={filters.excludedCategorySearchTerms}
                    onChange={(terms) => handleChange('excludedCategorySearchTerms', terms)}
                  />
                </Box>
              </Box>
            </Box>

            {/* Row 4: Service Availability */}
            <Box sx={{ display: 'flex', columnGap: FILTER_GROUP_GAP, rowGap: 2, flexWrap: 'wrap', alignItems: 'flex-start' }}>
              {/* Optional Service Availability */}
              <Box sx={{ flex: '0 0 auto' }}>
                <Typography variant="subtitle2" gutterBottom>
                  Optional Service Availability
                </Typography>
                <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                  <OptionalServiceAvailabilitySelect
                    label="Casino"
                    value={filters.casinoAvailability}
                    onChange={(value) => handleChange('casinoAvailability', value)}
                  />
                  <OptionalServiceAvailabilitySelect
                    label="Crypto"
                    value={filters.cryptoAvailability}
                    onChange={(value) => handleChange('cryptoAvailability', value)}
                  />
                  <OptionalServiceAvailabilitySelect
                    label="Link Insert"
                    value={filters.linkInsertAvailability}
                    onChange={(value) => handleChange('linkInsertAvailability', value)}
                  />
                  <OptionalServiceAvailabilitySelect
                    label="Link Insert Casino"
                    value={filters.linkInsertCasinoAvailability}
                    onChange={(value) => handleChange('linkInsertCasinoAvailability', value)}
                  />
                  <OptionalServiceAvailabilitySelect
                    label="Dating"
                    value={filters.datingAvailability}
                    onChange={(value) => handleChange('datingAvailability', value)}
                  />
                </Box>
              </Box>
            </Box>

            {/* Stop list */}
            <Box sx={{ width: { xs: '100%', sm: 520 }, maxWidth: '100%' }}>
              <Typography variant="subtitle2" gutterBottom>
                Stop list
              </Typography>
              <Box
                data-testid="stop-list-control"
                sx={{
                  minHeight: 40,
                  display: 'flex',
                  alignItems: 'center',
                  gap: 1,
                  flexWrap: 'wrap',
                  px: 1.5,
                  py: 0.75,
                  border: '1px solid',
                  borderColor: 'divider',
                  borderRadius: (theme) => `${theme.custom.radius}px`,
                  bgcolor: 'background.paper',
                }}
              >
                <Typography variant="body2" color="text.secondary" sx={{ flex: '1 1 auto', minWidth: 150 }}>
                  {stopListStatusText}
                </Typography>
                {stopListPaused && (
                  <Chip
                    label="Paused"
                    size="small"
                    variant="outlined"
                    sx={{
                      height: 22,
                      borderColor: 'divider',
                      color: 'text.secondary',
                      bgcolor: 'background.paper',
                    }}
                  />
                )}
                {stopListCount > 0 && (
                  <Button
                    size="small"
                    variant="text"
                    color="inherit"
                    onClick={handleClearStopList}
                    sx={{ textTransform: 'none', color: 'text.secondary', px: 1 }}
                  >
                    Clear
                  </Button>
                )}
                <Button
                  size="small"
                  variant="outlined"
                  onClick={handleOpenStopListDialog}
                  sx={{ textTransform: 'none' }}
                >
                  {stopListCount === 0 ? 'Add domains' : 'Edit'}
                </Button>
              </Box>
              {stopListPaused && (
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.75 }}>
                  Not applied in Multi-search mode.
                </Typography>
              )}
            </Box>

            {/* Action Buttons */}
            <Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end' }}>
              <BrandButton
                startIcon={<ClearIcon />}
                onClick={handleClear}
                disabled={!hasSearchOrAdvancedFilters()}
              >
                Clear All
              </BrandButton>
            </Box>
          </Stack>
        </AccordionDetails>
      </Accordion>

      {stopListDialogOpen && (
        <StopListDialog
          open={stopListDialogOpen}
          domains={filters.stopListDomains}
          onClose={handleCancelStopListDialog}
          onApply={handleApplyStopList}
          onClear={handleClearStopList}
        />
      )}
    </Box>
  );
}
