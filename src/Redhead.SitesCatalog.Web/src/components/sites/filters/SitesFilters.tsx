import { useState, useEffect, useRef } from 'react';
import {
  Box,
  TextField,
  MenuItem,
  FormControlLabel,
  Checkbox,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Typography,
  Stack,
  Autocomplete,
  Chip,
  Button,
} from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import type { FilterOption, SitesFilters } from '../../../types/sites.types';
import { sitesService } from '../../../services/sites.service';
import { BrandButton } from '../../common/BrandButton';
import { SERVICE_AVAILABILITY_FILTER_OPTIONS } from '../../../utils/serviceAvailability';
import { LANGUAGE_OPTIONS, getLanguageOption } from '../../../utils/language';
import { LastPublishedRangeFilter } from './LastPublishedRangeFilter';
import { StopListDialog } from '../dialogs/StopListDialog';
import { pluralize } from '../../../utils/pluralize';
import {
  CategoriesSearchFilter,
  type CategoriesSearchFilterHandle,
} from './CategoriesSearchFilter';

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
  location: [],
  niches: [],
  categorySearchTerms: [],
  languages: [],
  casinoAvailability: 'all',
  cryptoAvailability: 'all',
  linkInsertAvailability: 'all',
  linkInsertCasinoAvailability: 'all',
  datingAvailability: 'all',
  quarantine: 'exclude',
  lastPublishedFromMonth: null,
  lastPublishedToMonth: null,
};

const FILTER_GROUP_GAP = 5;

function areStringArraysEqual(left: string[], right: string[]): boolean {
  return left.length === right.length && left.every((value, index) => value === right[index]);
}

export function SitesFilters({
  filters,
  onFiltersChange,
  onApply,
  multiSearchMode = false,
  onMultiSearchModeChange,
  filterOptionsRefreshKey = 0,
}: SitesFiltersProps) {
  const [locations, setLocations] = useState<string[]>([]);
  const [nicheOptions, setNicheOptions] = useState<FilterOption[]>([]);
  const [expanded, setExpanded] = useState(false);
  const [stopListDialogOpen, setStopListDialogOpen] = useState(false);
  const categoriesSearchFilterRef = useRef<CategoriesSearchFilterHandle>(null);

  useEffect(() => {
    const loadLocations = async () => {
      try {
        const data = await sitesService.getLocations();
        setLocations(data);
      } catch (error) {
        console.error('Failed to load locations:', error);
      }
    };

    loadLocations();
  }, []);

  useEffect(() => {
    const loadFilterOptions = async () => {
      try {
        const data = await sitesService.getFilterOptions();
        setNicheOptions(data.niches);
      } catch (error) {
        console.error('Failed to load filter options:', error);
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

  const handleApply = () => {
    const categorySearchTerms =
      categoriesSearchFilterRef.current?.commitPendingInput() ?? filters.categorySearchTerms;
    const nextFilters = areStringArraysEqual(categorySearchTerms, filters.categorySearchTerms)
      ? filters
      : { ...filters, categorySearchTerms };

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
    if (filters.location.length > 0) count += 1;
    if (filters.niches.length > 0) count += 1;
    if (filters.categorySearchTerms.length > 0) count += 1;
    if (filters.languages.length > 0) count += 1;
    if (filters.casinoAvailability !== 'all') count += 1;
    if (filters.cryptoAvailability !== 'all') count += 1;
    if (filters.linkInsertAvailability !== 'all') count += 1;
    if (filters.linkInsertCasinoAvailability !== 'all') count += 1;
    if (filters.datingAvailability !== 'all') count += 1;
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
      filters.location.length > 0 ||
      filters.niches.length > 0 ||
      filters.categorySearchTerms.length > 0 ||
      filters.languages.length > 0 ||
      filters.casinoAvailability !== 'all' ||
      filters.cryptoAvailability !== 'all' ||
      filters.linkInsertAvailability !== 'all' ||
      filters.linkInsertCasinoAvailability !== 'all' ||
      filters.datingAvailability !== 'all' ||
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
  const selectedLanguageOptions = filters.languages.map(
    (value) => getLanguageOption(value) ?? { value, label: value }
  );

  const stopListCount = filters.stopListDomains.length;
  const stopListPaused = multiSearchMode && stopListCount > 0;
  const stopListApplied = !multiSearchMode && stopListCount > 0;
  const advancedActiveFilterCount = getAdvancedActiveFilterCount();
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

  return (
    <Box sx={{ mb: 3 }}>
      {/* Search Bar */}
      <Box sx={{ display: 'flex', gap: 2, mb: 2, alignItems: 'flex-start' }}>
        <Box sx={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 1 }}>
          <TextField
            fullWidth
            multiline={multiSearchMode}
            minRows={multiSearchMode ? 3 : 1}
            maxRows={multiSearchMode ? 12 : 1}
            placeholder={
              multiSearchMode
                ? 'Paste domains or URLs (one per line or space-separated, max 500)'
                : 'Search by domain (example.com or https://www.example.com/path)'
            }
            value={filters.search}
            onChange={(e) => handleChange('search', e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !multiSearchMode) {
                handleApply();
              }
            }}
            InputProps={{
              startAdornment: <SearchIcon sx={{ mr: 1, color: 'text.secondary' }} />,
            }}
          />
          {onMultiSearchModeChange && (
            <FormControlLabel
              control={
                <Checkbox
                  checked={multiSearchMode}
                  onChange={(e) => onMultiSearchModeChange(e.target.checked)}
                />
              }
              label="Multi-search"
            />
          )}
        </Box>
        <BrandButton kind="primary" onClick={handleApply} sx={{ minWidth: 120 }}>
          Search
        </BrandButton>
      </Box>

      {/* Advanced Filters */}
      <Accordion expanded={expanded} onChange={() => setExpanded(!expanded)}>
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Stack direction="row" spacing={1} alignItems="center">
            <Typography>Advanced Filters</Typography>
            {advancedActiveFilterCount > 0 && (
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
                }}
              />
            )}
          </Stack>
        </AccordionSummary>
        <AccordionDetails>
          <Stack spacing={3}>
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
            <Box sx={{ display: 'flex', columnGap: FILTER_GROUP_GAP, rowGap: 3, flexWrap: 'wrap' }}>
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

              {/* Location Multi-Select */}
              <Box sx={{ flex: 1, minWidth: '200px', maxWidth: '350px' }}>
                <Typography variant="subtitle2" gutterBottom>
                  Location
                </Typography>
                <Autocomplete
                  multiple
                  size="small"
                  options={locations}
                  value={filters.location}
                  onChange={(_, newValue) => handleChange('location', newValue)}
                  renderInput={(params) => (
                    <TextField
                      {...params}
                      placeholder={filters.location.length === 0 ? "Select locations" : ""}
                    />
                  )}
                  disableCloseOnSelect
                  limitTags={2}
                />
              </Box>
            </Box>

            {/* Row 2: Last Publication + Quarantine */}
            <Box sx={{ display: 'flex', columnGap: FILTER_GROUP_GAP, rowGap: 3, flexWrap: 'wrap', alignItems: 'flex-start' }}>
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
              {/* Niche Multi-Select */}
              <Box sx={{ flex: 1, minWidth: '200px', maxWidth: '350px' }}>
                <Typography variant="subtitle2" gutterBottom>
                  Niche
                </Typography>
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
                      placeholder={filters.niches.length === 0 ? 'Select niches' : ''}
                    />
                  )}
                  disableCloseOnSelect
                  limitTags={2}
                />
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

            {/* Row 3: Categories */}
            <Box sx={{ display: 'flex', columnGap: FILTER_GROUP_GAP, rowGap: 3, flexWrap: 'wrap', alignItems: 'flex-start' }}>
              <Box sx={{ flex: 1, minWidth: '280px' }}>
                <CategoriesSearchFilter
                  ref={categoriesSearchFilterRef}
                  value={filters.categorySearchTerms}
                  onChange={(terms) => handleChange('categorySearchTerms', terms)}
                />
              </Box>
            </Box>

            {/* Row 4: Service Availability */}
            <Box sx={{ display: 'flex', columnGap: FILTER_GROUP_GAP, rowGap: 3, flexWrap: 'wrap', alignItems: 'flex-start' }}>
              {/* Optional Service Availability */}
              <Box sx={{ flex: '0 0 auto' }}>
                <Typography variant="subtitle2" gutterBottom>
                  Optional Service Availability
                </Typography>
                <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                  <TextField
                    select
                    size="small"
                    label="Casino"
                    value={filters.casinoAvailability}
                    onChange={(e) => handleChange('casinoAvailability', e.target.value as SitesFilters['casinoAvailability'])}
                    sx={{ width: 185 }}
                  >
                    {SERVICE_AVAILABILITY_FILTER_OPTIONS.map((option) => (
                      <MenuItem key={option.value} value={option.value}>
                        {option.label}
                      </MenuItem>
                    ))}
                  </TextField>
                  <TextField
                    select
                    size="small"
                    label="Crypto"
                    value={filters.cryptoAvailability}
                    onChange={(e) => handleChange('cryptoAvailability', e.target.value as SitesFilters['cryptoAvailability'])}
                    sx={{ width: 185 }}
                  >
                    {SERVICE_AVAILABILITY_FILTER_OPTIONS.map((option) => (
                      <MenuItem key={option.value} value={option.value}>
                        {option.label}
                      </MenuItem>
                    ))}
                  </TextField>
                  <TextField
                    select
                    size="small"
                    label="Link Insert"
                    value={filters.linkInsertAvailability}
                    onChange={(e) =>
                      handleChange('linkInsertAvailability', e.target.value as SitesFilters['linkInsertAvailability'])
                    }
                    sx={{ width: 185 }}
                  >
                    {SERVICE_AVAILABILITY_FILTER_OPTIONS.map((option) => (
                      <MenuItem key={option.value} value={option.value}>
                        {option.label}
                      </MenuItem>
                    ))}
                  </TextField>
                  <TextField
                    select
                    size="small"
                    label="Link Insert Casino"
                    value={filters.linkInsertCasinoAvailability}
                    onChange={(e) => handleChange('linkInsertCasinoAvailability', e.target.value as SitesFilters['linkInsertCasinoAvailability'])}
                    sx={{ width: 185 }}
                  >
                    {SERVICE_AVAILABILITY_FILTER_OPTIONS.map((option) => (
                      <MenuItem key={option.value} value={option.value}>
                        {option.label}
                      </MenuItem>
                    ))}
                  </TextField>
                  <TextField
                    select
                    size="small"
                    label="Dating"
                    value={filters.datingAvailability}
                    onChange={(e) => handleChange('datingAvailability', e.target.value as SitesFilters['datingAvailability'])}
                    sx={{ width: 185 }}
                  >
                    {SERVICE_AVAILABILITY_FILTER_OPTIONS.map((option) => (
                      <MenuItem key={option.value} value={option.value}>
                        {option.label}
                      </MenuItem>
                    ))}
                  </TextField>
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
            <Box sx={{ display: 'flex', gap: 2, justifyContent: 'flex-end' }}>
              <BrandButton
                startIcon={<ClearIcon />}
                onClick={handleClear}
                disabled={!hasSearchOrAdvancedFilters()}
              >
                Clear All
              </BrandButton>
              <BrandButton kind="primary" onClick={handleApply} disabled={!!lastPublishedRangeError}>
                Apply Filters
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
