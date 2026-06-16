import { useState, useEffect, useMemo, useRef, type KeyboardEvent } from 'react';
import {
  Box,
  TextField,
  MenuItem,
  Menu,
  Typography,
  Stack,
  Autocomplete,
  Chip,
  Checkbox,
  Button,
  IconButton,
  ToggleButton,
  ToggleButtonGroup,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControlLabel,
  Collapse,
  ListItemIcon,
  ListItemText,
  ListSubheader,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown';
import SaveOutlinedIcon from '@mui/icons-material/SaveOutlined';
import TuneIcon from '@mui/icons-material/Tune';
import CheckIcon from '@mui/icons-material/Check';
import type {
  FilterOption,
  LocationFilterOptions,
  ServiceAvailabilityFilter,
  SitesFilters,
  TermFilterOptionDto,
  TopicFitMode,
} from '../../../types/sites.types';
import type { SavedFilterSet, SavedFilterSettings } from '../../../types/savedFilters.types';
import { sitesService } from '../../../services/sites.service';
import { BrandButton } from '../../common/BrandButton';
import {
  SERVICE_AVAILABILITY_FILTER_OPTIONS,
  normalizeServiceAvailabilityFilter,
} from '../../../utils/serviceAvailability';
import { LANGUAGE_OPTIONS, getLanguageOption } from '../../../utils/language';
import {
  ANY_TERM_KEY,
  createTermFilterOptions,
  formatTermFilterLabel,
} from '../../../utils/pricing';
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
import { buildSavedFilterSettings } from '../saved-filters/savedFilters.helpers';

interface SitesFiltersProps {
  filters: SitesFilters;
  onFiltersChange: (filters: SitesFilters) => void;
  onApply: (filters?: SitesFilters) => void;
  multiSearchMode?: boolean;
  onMultiSearchModeChange?: (enabled: boolean) => void;
  canFilterQuarantine?: boolean;
  filterOptionsRefreshKey?: number;
  savedFilterSets?: SavedFilterSet[];
  activeSavedFilterSetId?: string | null;
  savedFiltersLoading?: boolean;
  savedFilterSetChanged?: boolean;
  onClearSavedFilterSetSelection?: () => void;
  onApplySavedFilterSet?: (filterSet: SavedFilterSet) => void;
  onCreateSavedFilterSet?: (name: string, settings: SavedFilterSettings) => Promise<void>;
  onUpdateSavedFilterSet?: (id: string, settings: SavedFilterSettings) => Promise<void>;
  onRenameSavedFilterSet?: (id: string, name: string) => Promise<void>;
  onDeleteSavedFilterSet?: (id: string) => Promise<void>;
}

const INITIAL_FILTERS: SitesFilters = {
  search: '',
  drMin: '',
  drMax: '',
  trafficMin: '',
  trafficMax: '',
  priceMin: '',
  priceMax: '',
  termKey: null,
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

type SavedFilterDialogMode = 'create' | 'update' | 'rename';

interface SavedFilterDialogState {
  mode: SavedFilterDialogMode;
  name: string;
  includeStopListDomains: boolean;
  error: string | null;
}

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
        <Typography
          variant="body2"
          noWrap
          sx={{
            minWidth: 0,
            flex: '1 1 auto',
            overflow: 'hidden',
            textOverflow: 'ellipsis',
          }}
        >
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
      sx={{
        width: 185,
        '& .MuiAutocomplete-inputRoot': {
          flexWrap: 'nowrap',
          overflow: 'hidden',
        },
        '& .MuiAutocomplete-input': {
          minWidth: '0 !important',
        },
      }}
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
  savedFilterSets = [],
  activeSavedFilterSetId = null,
  savedFiltersLoading = false,
  savedFilterSetChanged = false,
  onClearSavedFilterSetSelection,
  onApplySavedFilterSet,
  onCreateSavedFilterSet,
  onUpdateSavedFilterSet,
  onRenameSavedFilterSet,
  onDeleteSavedFilterSet,
}: SitesFiltersProps) {
  const [locationOptions, setLocationOptions] = useState<LocationFilterOptions | null>(null);
  const [locationOptionsLoading, setLocationOptionsLoading] = useState(false);
  const [locationOptionsError, setLocationOptionsError] = useState<string | null>(null);
  const [nicheOptions, setNicheOptions] = useState<FilterOption[]>([]);
  const [termOptions, setTermOptions] = useState<TermFilterOptionDto[]>([
    { termKey: ANY_TERM_KEY, label: 'Any term' },
  ]);
  const [expanded, setExpanded] = useState(false);
  const [stopListDialogOpen, setStopListDialogOpen] = useState(false);
  const [savedFiltersAnchor, setSavedFiltersAnchor] = useState<HTMLElement | null>(null);
  const [savedFilterDialog, setSavedFilterDialog] = useState<SavedFilterDialogState | null>(null);
  const [savedFilterDeleteOpen, setSavedFilterDeleteOpen] = useState(false);
  const [savedFilterDeleteError, setSavedFilterDeleteError] = useState<string | null>(null);
  const [savedFilterActionLoading, setSavedFilterActionLoading] = useState(false);
  const [searchDraft, setSearchDraft] = useState(filters.search);
  const categoriesSearchFilterRef = useRef<CategoriesSearchFilterHandle>(null);
  const excludedCategoriesSearchFilterRef = useRef<CategoriesSearchFilterHandle>(null);

  useEffect(() => {
    setSearchDraft(filters.search);
  }, [filters.search, multiSearchMode]);

  useEffect(() => {
    const loadFilterOptions = async () => {
      setLocationOptionsLoading(true);
      setLocationOptionsError(null);
      try {
        const data = await sitesService.getFilterOptions();
        setNicheOptions(data.niches);
        setLocationOptions(data.locations ?? null);
        setTermOptions(createTermFilterOptions(data.terms));
      } catch (error) {
        console.error('Failed to load filter options:', error);
        setLocationOptions(null);
        setTermOptions(createTermFilterOptions(null));
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

  const handleClearAdvancedFilters = () => {
    onFiltersChange({
      ...filters,
      drMin: INITIAL_FILTERS.drMin,
      drMax: INITIAL_FILTERS.drMax,
      trafficMin: INITIAL_FILTERS.trafficMin,
      trafficMax: INITIAL_FILTERS.trafficMax,
      priceMin: INITIAL_FILTERS.priceMin,
      priceMax: INITIAL_FILTERS.priceMax,
      termKey: INITIAL_FILTERS.termKey,
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
    onClearSavedFilterSetSelection?.();
  };

  const handleApply = () => {
    const appliedSearch = multiSearchMode ? searchDraft : filters.search;
    const filtersWithSearch =
      appliedSearch === filters.search ? filters : { ...filters, search: appliedSearch };
    const categorySearchTerms =
      categoriesSearchFilterRef.current?.commitPendingInput() ??
      filtersWithSearch.categorySearchTerms;
    const excludedCategorySearchTerms =
      excludedCategoriesSearchFilterRef.current?.commitPendingInput() ??
      filtersWithSearch.excludedCategorySearchTerms;
    const nextFilters =
      areStringArraysEqual(categorySearchTerms, filtersWithSearch.categorySearchTerms) &&
      areStringArraysEqual(
        excludedCategorySearchTerms,
        filtersWithSearch.excludedCategorySearchTerms
      )
        ? filtersWithSearch
        : { ...filtersWithSearch, categorySearchTerms, excludedCategorySearchTerms };

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
    if (filters.termKey !== INITIAL_FILTERS.termKey) count += 1;
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
  const selectedTermKey = filters.termKey ?? ANY_TERM_KEY;
  const selectedTermLabel =
    termOptions.find((option) => option.termKey === selectedTermKey)?.label ??
    formatTermFilterLabel(selectedTermKey);
  const optionalServiceAvailabilityFilterActive =
    hasAvailabilityFilter(filters.casinoAvailability) ||
    hasAvailabilityFilter(filters.cryptoAvailability) ||
    hasAvailabilityFilter(filters.linkInsertAvailability) ||
    hasAvailabilityFilter(filters.linkInsertCasinoAvailability) ||
    hasAvailabilityFilter(filters.datingAvailability);
  const showOptionalServiceTermHelper =
    selectedTermKey !== ANY_TERM_KEY && optionalServiceAvailabilityFilterActive;

  const stopListCount = filters.stopListDomains.length;
  const stopListPaused = multiSearchMode && stopListCount > 0;
  const advancedActiveFilterCount = getAdvancedActiveFilterCount();
  const advancedFiltersActive = advancedActiveFilterCount > 0;
  const activeFilterSummaryItems = useMemo(
    () => buildAdvancedActiveFilterSummaries(filters, multiSearchMode),
    [filters, multiSearchMode]
  );
  const activeSavedFilterSet =
    savedFilterSets.find((filterSet) => filterSet.id === activeSavedFilterSetId) ?? null;
  const canIncludeStopListDomains = !multiSearchMode && filters.stopListDomains.length > 0;
  const hasSavableCurrentFilterState = advancedFiltersActive || canIncludeStopListDomains;
  const savedFilterControlsEnabled = Boolean(
    onCreateSavedFilterSet ||
      onApplySavedFilterSet ||
      onUpdateSavedFilterSet ||
      onRenameSavedFilterSet ||
      onDeleteSavedFilterSet
  );
  const savedFilterSelectorText = activeSavedFilterSet
    ? activeSavedFilterSet.name
    : hasSavableCurrentFilterState
      ? 'Unsaved filters'
      : 'Saved sets';
  const stopListStatusText =
    stopListCount === 0
      ? 'No domains excluded'
      : stopListPaused
        ? `${stopListCount} ${pluralize(stopListCount, 'domain')} saved`
        : `${stopListCount} ${pluralize(stopListCount, 'domain')} excluded`;
  const searchValue = multiSearchMode ? searchDraft : filters.search;

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
    const currentSearch = multiSearchMode ? searchDraft : filters.search;

    if (currentSearch === '') return;

    if (multiSearchMode) {
      setSearchDraft('');
      return;
    }

    const nextFilters = { ...filters, search: '' };
    onFiltersChange(nextFilters);

    onApply(nextFilters);
  };

  const handleSearchChange = (value: string) => {
    if (multiSearchMode) {
      setSearchDraft(value);
      return;
    }

    handleChange('search', value);
  };

  const toggleFiltersPanel = () => {
    setExpanded((current) => !current);
  };

  const handleFiltersSummaryKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key !== 'Enter' && event.key !== ' ') return;

    event.preventDefault();
    toggleFiltersPanel();
  };

  const getSavedFilterErrorMessage = (error: unknown, fallback: string) =>
    error instanceof Error ? error.message : fallback;

  const openSavedFilterDialog = (mode: SavedFilterDialogMode) => {
    if (mode !== 'create' && !activeSavedFilterSet) return;

    setSavedFiltersAnchor(null);
    setSavedFilterDialog({
      mode,
      name:
        mode === 'create'
          ? activeSavedFilterSet
            ? `${activeSavedFilterSet.name} copy`
            : ''
          : activeSavedFilterSet?.name ?? '',
      includeStopListDomains:
        mode === 'update' && canIncludeStopListDomains
          ? Array.isArray(activeSavedFilterSet?.settings.stopListDomains)
          : false,
      error: null,
    });
  };

  const buildCurrentSavedFilterSettings = (dialog: SavedFilterDialogState) =>
    buildSavedFilterSettings(filters, {
      includeStopListDomains: canIncludeStopListDomains && dialog.includeStopListDomains,
    });

  const handleSubmitSavedFilterDialog = async () => {
    if (!savedFilterDialog) return;
    const trimmedName = savedFilterDialog.name.trim();

    if (savedFilterDialog.mode !== 'update' && !trimmedName) {
      setSavedFilterDialog((current) =>
        current ? { ...current, error: 'Enter a filter set name.' } : current
      );
      return;
    }

    setSavedFilterActionLoading(true);
    setSavedFilterDialog((current) => (current ? { ...current, error: null } : current));

    try {
      if (savedFilterDialog.mode === 'create') {
        if (!onCreateSavedFilterSet) return;
        await onCreateSavedFilterSet(trimmedName, buildCurrentSavedFilterSettings(savedFilterDialog));
      } else if (savedFilterDialog.mode === 'rename') {
        if (!activeSavedFilterSet || !onRenameSavedFilterSet) return;
        await onRenameSavedFilterSet(activeSavedFilterSet.id, trimmedName);
      } else {
        if (!activeSavedFilterSet || !onUpdateSavedFilterSet) return;
        await onUpdateSavedFilterSet(
          activeSavedFilterSet.id,
          buildCurrentSavedFilterSettings(savedFilterDialog)
        );
      }

      setSavedFilterDialog(null);
    } catch (error) {
      setSavedFilterDialog((current) =>
        current
          ? {
              ...current,
              error: getSavedFilterErrorMessage(error, 'Saved filter set action failed.'),
            }
          : current
      );
    } finally {
      setSavedFilterActionLoading(false);
    }
  };

  const handleDeleteSavedFilterSet = async () => {
    if (!activeSavedFilterSet || !onDeleteSavedFilterSet) return;

    setSavedFilterActionLoading(true);
    setSavedFilterDeleteError(null);
    try {
      await onDeleteSavedFilterSet(activeSavedFilterSet.id);
      setSavedFilterDeleteOpen(false);
    } catch (error) {
      setSavedFilterDeleteError(getSavedFilterErrorMessage(error, 'Failed to delete saved filter set.'));
    } finally {
      setSavedFilterActionLoading(false);
    }
  };

  const savedFilterSelector = savedFilterControlsEnabled ? (
    <Button
      size="small"
      variant="outlined"
      endIcon={
        savedFiltersLoading ? (
          <CircularProgress size={14} color="inherit" />
        ) : (
          <KeyboardArrowDownIcon fontSize="small" />
        )
      }
      disabled={savedFiltersLoading || savedFilterActionLoading}
      onClick={(event) => setSavedFiltersAnchor(event.currentTarget)}
      sx={{
        minWidth: 0,
        height: 28,
        borderRadius: 999,
        borderColor: 'divider',
        bgcolor: 'background.paper',
        color: 'text.primary',
        textTransform: 'none',
        fontSize: 12,
        fontWeight: 600,
        px: 1.25,
      }}
    >
      <Box component="span" sx={{ display: 'inline-flex', minWidth: 0, maxWidth: 220 }}>
        {activeSavedFilterSet && (
          <Box component="span" sx={{ flexShrink: 0 }}>
            Filter set:&nbsp;
          </Box>
        )}
        <Box
          component="span"
          sx={{
            minWidth: 0,
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
          }}
        >
          {savedFilterSelectorText}
        </Box>
      </Box>
    </Button>
  ) : null;

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
          ? 'Paste domains or URLs (one per line or space-separated, max 5,000)'
          : 'Search by domain (example.com or https://www.example.com/path)'
      }
      value={searchValue}
      onChange={(e) => handleSearchChange(e.target.value)}
      onKeyDown={(e) => {
        if (e.key === 'Enter' && !multiSearchMode) {
          e.preventDefault();
          handleApply();
        }
      }}
      InputProps={{
        startAdornment: <SearchIcon sx={{ mr: 1, color: 'text.secondary' }} />,
        endAdornment: searchValue ? (
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

      {/* Filters */}
      <Box
        sx={{
          border: '1px solid',
          borderColor: 'divider',
          borderRadius: (theme) => `${theme.custom.radius}px`,
          bgcolor: 'background.paper',
          boxShadow: '0 1px 2px rgba(15, 23, 42, 0.08)',
          overflow: 'hidden',
        }}
      >
        <Box
          sx={{
            minHeight: 52,
            display: 'flex',
            alignItems: 'center',
            gap: 1,
            flexWrap: 'wrap',
            px: 1.5,
            py: 1,
          }}
        >
          <Box
            role="button"
            tabIndex={0}
            aria-expanded={expanded}
            aria-controls="sites-filters-panel"
            onClick={toggleFiltersPanel}
            onKeyDown={handleFiltersSummaryKeyDown}
            sx={{
              flex: '1 1 280px',
              minWidth: 0,
              minHeight: 32,
              display: 'flex',
              alignItems: 'center',
              gap: 1,
              cursor: 'pointer',
              borderRadius: 999,
              pr: 0.5,
              '&:focus-visible': {
                outline: '2px solid',
                outlineColor: 'primary.main',
                outlineOffset: 2,
              },
            }}
          >
            <Typography sx={{ flexShrink: 0, fontWeight: 600 }}>Filters</Typography>
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
                  fontWeight: 500,
                  flexShrink: 0,
                }}
              />
            )}
            {expanded ? (
              <Box sx={{ flex: '1 1 auto', minWidth: 0 }} />
            ) : activeFilterSummaryItems.length > 0 ? (
              <ActiveFiltersSummary items={activeFilterSummaryItems} />
            ) : (
              <Typography
                variant="body2"
                color="text.secondary"
                sx={{ flex: '1 1 auto', minWidth: 140 }}
              >
                No filters applied
              </Typography>
            )}
          </Box>
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 0.75,
              flexShrink: 0,
              flexWrap: 'wrap',
              justifyContent: 'flex-end',
            }}
          >
            {savedFilterSelector}
            {activeSavedFilterSet && savedFilterSetChanged && onUpdateSavedFilterSet && (
              <Button
                size="small"
                variant="outlined"
                startIcon={<SaveOutlinedIcon fontSize="small" />}
                disabled={savedFiltersLoading || savedFilterActionLoading}
                onClick={() => openSavedFilterDialog('update')}
                sx={{
                  height: 30,
                  borderRadius: 999,
                  borderColor: 'divider',
                  bgcolor: 'background.paper',
                  color: 'text.primary',
                  textTransform: 'none',
                  fontSize: 12,
                  fontWeight: 600,
                  px: 1.25,
                  flexShrink: 0,
                }}
              >
                Save changes
              </Button>
            )}
            {advancedFiltersActive && (
              <Button
                size="small"
                variant="outlined"
                color="inherit"
                startIcon={<ClearIcon fontSize="small" />}
                onClick={handleClearAdvancedFilters}
                sx={{
                  flexShrink: 0,
                  minWidth: 'auto',
                  height: 30,
                  px: 1.25,
                  borderRadius: 999,
                  borderColor: 'divider',
                  bgcolor: 'background.paper',
                  color: 'text.secondary',
                  fontSize: 12,
                  fontWeight: 600,
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
            <Button
              size="small"
              variant="outlined"
              startIcon={<TuneIcon fontSize="small" />}
              onClick={toggleFiltersPanel}
              sx={{
                flexShrink: 0,
                height: 30,
                px: 1.25,
                borderRadius: 999,
                borderColor: expanded ? 'rgba(255, 69, 91, 0.35)' : 'divider',
                bgcolor: expanded ? 'rgba(255, 69, 91, 0.06)' : 'background.paper',
                color: 'primary.main',
                textTransform: 'none',
                fontSize: 12,
                fontWeight: 700,
              }}
            >
              {expanded ? 'Hide filters' : 'Show filters'}
            </Button>
          </Box>
        </Box>
        <Collapse in={expanded}>
          <Box
            id="sites-filters-panel"
            sx={{ borderTop: '1px solid', borderColor: 'divider', px: 1.5, py: 1.5 }}
          >
          <Stack spacing={2}>
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

              {/* Term Filter */}
              <Box sx={{ flex: '0 0 auto', minWidth: '185px' }}>
                <Typography variant="subtitle2" gutterBottom>
                  Term
                </Typography>
                <TextField
                  select
                  size="small"
                  value={selectedTermKey}
                  onChange={(event) =>
                    handleChange(
                      'termKey',
                      event.target.value === ANY_TERM_KEY ? null : event.target.value
                    )
                  }
                  sx={{ width: 185 }}
                  slotProps={{
                    select: {
                      displayEmpty: true,
                      inputProps: { 'aria-label': 'Term' },
                      renderValue: () => selectedTermLabel,
                    },
                  }}
                >
                  {termOptions.map((option) => (
                    <MenuItem key={option.termKey || 'any'} value={option.termKey}>
                      {option.label}
                    </MenuItem>
                  ))}
                </TextField>
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
                <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mt: 1 }}>
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
                {showOptionalServiceTermHelper && (
                  <Typography
                    variant="caption"
                    color="text.secondary"
                    sx={{ display: 'block', maxWidth: 720, mt: 1, lineHeight: 1.35 }}
                  >
                    Term is active: service filters use only {selectedTermLabel} prices.
                  </Typography>
                )}
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

          </Stack>
          </Box>
        </Collapse>
      </Box>

      <Menu
        anchorEl={savedFiltersAnchor}
        open={Boolean(savedFiltersAnchor)}
        onClose={() => setSavedFiltersAnchor(null)}
        MenuListProps={{ dense: true }}
      >
        <MenuItem
          selected={!activeSavedFilterSetId}
          onClick={() => {
            setSavedFiltersAnchor(null);
            onClearSavedFilterSetSelection?.();
          }}
          sx={{ minWidth: 240 }}
        >
          <ListItemIcon>
            {!activeSavedFilterSetId ? <CheckIcon fontSize="small" /> : null}
          </ListItemIcon>
          <ListItemText>Current filters</ListItemText>
        </MenuItem>
        <Divider />
        <ListSubheader>Saved filter sets</ListSubheader>
        {savedFilterSets.length === 0 ? (
          <MenuItem disabled>
            <ListItemText secondary="No saved filter sets yet" />
          </MenuItem>
        ) : (
          savedFilterSets.map((filterSet) => (
            <MenuItem
              key={filterSet.id}
              selected={filterSet.id === activeSavedFilterSetId}
              onClick={() => {
                setSavedFiltersAnchor(null);
                onApplySavedFilterSet?.(filterSet);
              }}
              sx={{ minWidth: 220 }}
            >
              <ListItemIcon>
                {filterSet.id === activeSavedFilterSetId ? (
                  <CheckIcon fontSize="small" />
                ) : null}
              </ListItemIcon>
              <Box
                component="span"
                sx={{
                  minWidth: 0,
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                  whiteSpace: 'nowrap',
                  fontWeight: filterSet.id === activeSavedFilterSetId ? 600 : 400,
                }}
              >
                {filterSet.name}
              </Box>
            </MenuItem>
          ))
        )}
        {(hasSavableCurrentFilterState ||
          (activeSavedFilterSet && (onRenameSavedFilterSet || onDeleteSavedFilterSet))) && (
          <Divider />
        )}
        {hasSavableCurrentFilterState && onCreateSavedFilterSet && (
          <MenuItem onClick={() => openSavedFilterDialog('create')}>
            {activeSavedFilterSet && !savedFilterSetChanged
              ? 'Duplicate filter set'
              : 'Save as new filter set'}
          </MenuItem>
        )}
        {activeSavedFilterSet && onRenameSavedFilterSet && (
          <MenuItem onClick={() => openSavedFilterDialog('rename')}>
            Rename filter set
          </MenuItem>
        )}
        {activeSavedFilterSet && onDeleteSavedFilterSet && (
          <MenuItem
            onClick={() => {
              setSavedFiltersAnchor(null);
              setSavedFilterDeleteError(null);
              setSavedFilterDeleteOpen(true);
            }}
            sx={{ color: 'error.main' }}
          >
            Delete filter set
          </MenuItem>
        )}
      </Menu>

      <Dialog
        open={Boolean(savedFilterDialog)}
        onClose={() => {
          if (!savedFilterActionLoading) setSavedFilterDialog(null);
        }}
        fullWidth
        maxWidth="xs"
      >
        <DialogTitle>
          {savedFilterDialog?.mode === 'update'
            ? 'Update filter set'
            : savedFilterDialog?.mode === 'rename'
              ? 'Rename filter set'
              : 'Save filter set'}
        </DialogTitle>
        <DialogContent sx={{ overflow: 'visible' }}>
          {savedFilterDialog && (
            <Stack spacing={1.5} sx={{ pt: 1 }}>
              {savedFilterDialog.mode === 'update' ? (
                <Typography variant="body2" color="text.secondary">
                  {activeSavedFilterSet?.name}
                </Typography>
              ) : (
                <TextField
                  label="Name"
                  value={savedFilterDialog.name}
                  onChange={(event) =>
                    setSavedFilterDialog((current) =>
                      current
                        ? { ...current, name: event.target.value, error: null }
                        : current
                    )
                  }
                  autoFocus
                  fullWidth
                />
              )}

              {savedFilterDialog.mode !== 'rename' && (
                <Stack spacing={0.25}>
                  {canIncludeStopListDomains && (
                    <FormControlLabel
                      control={
                        <Checkbox
                          checked={savedFilterDialog.includeStopListDomains}
                          onChange={(event) =>
                            setSavedFilterDialog((current) =>
                              current
                                ? {
                                    ...current,
                                    includeStopListDomains: event.target.checked,
                                  }
                                : current
                            )
                          }
                        />
                      }
                      label="Include stop list domains"
                    />
                  )}
                </Stack>
              )}

              {savedFilterDialog.error && (
                <Typography variant="body2" color="error.main">
                  {savedFilterDialog.error}
                </Typography>
              )}
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <BrandButton
            onClick={() => setSavedFilterDialog(null)}
            disabled={savedFilterActionLoading}
          >
            Cancel
          </BrandButton>
          <BrandButton
            kind="primary"
            onClick={handleSubmitSavedFilterDialog}
            disabled={savedFilterActionLoading}
          >
            {savedFilterDialog?.mode === 'update'
              ? 'Update'
              : savedFilterDialog?.mode === 'rename'
                ? 'Rename'
                : 'Save'}
          </BrandButton>
        </DialogActions>
      </Dialog>

      <Dialog
        open={savedFilterDeleteOpen}
        onClose={() => {
          if (!savedFilterActionLoading) setSavedFilterDeleteOpen(false);
        }}
        fullWidth
        maxWidth="xs"
      >
        <DialogTitle>Delete filter set?</DialogTitle>
        <DialogContent>
          <Stack spacing={1.5}>
            <Typography variant="body2" color="text.secondary">
              {activeSavedFilterSet?.name}
            </Typography>
            {savedFilterDeleteError && (
              <Typography variant="body2" color="error.main">
                {savedFilterDeleteError}
              </Typography>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <BrandButton
            onClick={() => setSavedFilterDeleteOpen(false)}
            disabled={savedFilterActionLoading}
          >
            Cancel
          </BrandButton>
          <BrandButton
            kind="primary"
            onClick={handleDeleteSavedFilterSet}
            disabled={savedFilterActionLoading}
          >
            Delete
          </BrandButton>
        </DialogActions>
      </Dialog>

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
