import { useState, useEffect } from 'react';
import {
  Box,
  TextField,
  FormControl,
  FormControlLabel,
  Checkbox,
  Radio,
  RadioGroup,
  FormLabel,
  Button,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Typography,
  Stack,
  Autocomplete,
} from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import type { SitesFilters } from '../../types/sites.types';
import { sitesService } from '../../services/sites.service';

interface SitesFiltersProps {
  filters: SitesFilters;
  onFiltersChange: (filters: SitesFilters) => void;
  onApply: () => void;
  multiSearchMode?: boolean;
  onMultiSearchModeChange?: (enabled: boolean) => void;
}

const INITIAL_FILTERS: SitesFilters = {
  search: '',
  drMin: '',
  drMax: '',
  trafficMin: '',
  trafficMax: '',
  priceMin: '',
  priceMax: '',
  location: [],
  casinoAllowed: false,
  cryptoAllowed: false,
  linkInsertAllowed: false,
  quarantine: 'all',
};

export function SitesFilters({
  filters,
  onFiltersChange,
  onApply,
  multiSearchMode = false,
  onMultiSearchModeChange,
}: SitesFiltersProps) {
  const [locations, setLocations] = useState<string[]>([]);
  const [expanded, setExpanded] = useState(false);

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

  const handleChange = <K extends keyof SitesFilters>(
    field: K,
    value: SitesFilters[K]
  ) => {
    onFiltersChange({ ...filters, [field]: value });
  };

  const handleClear = () => {
    onFiltersChange(INITIAL_FILTERS);
    onApply();
  };

  const hasActiveFilters = () => {
    return (
      filters.search !== '' ||
      filters.drMin !== '' ||
      filters.drMax !== '' ||
      filters.trafficMin !== '' ||
      filters.trafficMax !== '' ||
      filters.priceMin !== '' ||
      filters.priceMax !== '' ||
      filters.location.length > 0 ||
      filters.casinoAllowed ||
      filters.cryptoAllowed ||
      filters.linkInsertAllowed ||
      filters.quarantine !== 'all'
    );
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
                onApply();
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
        <Button
          variant="contained"
          onClick={onApply}
          sx={{ minWidth: 120 }}
        >
          Search
        </Button>
      </Box>

      {/* Advanced Filters */}
      <Accordion expanded={expanded} onChange={() => setExpanded(!expanded)}>
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Typography>
            Advanced Filters
            {hasActiveFilters() && (
              <Typography component="span" color="primary" sx={{ ml: 1 }}>
                (Active)
              </Typography>
            )}
          </Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Stack spacing={3}>
            {/* Range Filters Row */}
            <Box sx={{ display: 'flex', gap: 3, flexWrap: 'wrap' }}>
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
              <Box sx={{ flex: 1, minWidth: '200px' }}>
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
                /></Box>
            </Box>

            {/* Checkboxes and Radio Row */}
            <Box sx={{ display: 'flex', gap: 3, flexWrap: 'wrap' }}>
              {/* Allowed Flags */}
              <Box sx={{ flex: '1 1 300px' }}>
                <Typography variant="subtitle2" gutterBottom>
                  Allowed Types
                </Typography>
                <Box>
                  <FormControlLabel
                    control={
                      <Checkbox
                        checked={filters.casinoAllowed}
                        onChange={(e) => handleChange('casinoAllowed', e.target.checked)}
                      />
                    }
                    label="Casino Allowed"
                  />
                  <FormControlLabel
                    control={
                      <Checkbox
                        checked={filters.cryptoAllowed}
                        onChange={(e) => handleChange('cryptoAllowed', e.target.checked)}
                      />
                    }
                    label="Crypto Allowed"
                  />
                  <FormControlLabel
                    control={
                      <Checkbox
                        checked={filters.linkInsertAllowed}
                        onChange={(e) => handleChange('linkInsertAllowed', e.target.checked)}
                      />
                    }
                    label="Link Insert Allowed"
                  />
                </Box>
              </Box>

              {/* Quarantine Filter */}
              <Box sx={{ flex: '1 1 400px' }}>
                <FormControl component="fieldset">
                  <FormLabel component="legend">Quarantine Status</FormLabel>
                  <RadioGroup
                    row
                    value={filters.quarantine}
                    onChange={(e) => handleChange('quarantine', e.target.value as 'all' | 'only' | 'exclude')}
                  >
                    <FormControlLabel value="all" control={<Radio />} label="All Sites" />
                    <FormControlLabel value="exclude" control={<Radio />} label="Available Only" />
                    <FormControlLabel value="only" control={<Radio />} label="Unavailable Only" />
                  </RadioGroup>
                </FormControl>
              </Box>
            </Box>

            {/* Action Buttons */}
            <Box sx={{ display: 'flex', gap: 2, justifyContent: 'flex-end' }}>
              <Button
                variant="outlined"
                startIcon={<ClearIcon />}
                onClick={handleClear}
                disabled={!hasActiveFilters()}
              >
                Clear All
              </Button>
              <Button variant="contained" onClick={onApply}>
                Apply Filters
              </Button>
            </Box>
          </Stack>
        </AccordionDetails>
      </Accordion>
    </Box>
  );
}
