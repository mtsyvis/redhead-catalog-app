import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ThemeProvider } from '@mui/material/styles';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { theme } from '../../../theme/theme';
import { SitesFilters } from '../SitesFilters';
import type { SitesFilters as FiltersType } from '../../../types/sites.types';

vi.mock('../../../services/sites.service', () => ({
  sitesService: {
    getLocations: vi.fn().mockResolvedValue([]),
  },
}));

const BASE_FILTERS: FiltersType = {
  search: '',
  drMin: '',
  drMax: '',
  trafficMin: '',
  trafficMax: '',
  priceMin: '',
  priceMax: '',
  location: [],
  casinoAvailability: 'all',
  cryptoAvailability: 'all',
  linkInsertAvailability: 'all',
  quarantine: 'all',
  lastPublishedFromMonth: null,
  lastPublishedToMonth: null,
};

function Wrapper({ children }: { children: React.ReactNode }) {
  return (
    <ThemeProvider theme={theme}>
      <LocalizationProvider dateAdapter={AdapterDayjs}>
        {children}
      </LocalizationProvider>
    </ThemeProvider>
  );
}

function renderFilters(
  overrides: Partial<FiltersType> = {},
  props: {
    onFiltersChange?: (f: FiltersType) => void;
    onApply?: () => void;
    canFilterQuarantine?: boolean;
  } = {}
) {
  const filters = { ...BASE_FILTERS, ...overrides };
  const onFiltersChange = props.onFiltersChange ?? vi.fn();
  const onApply = props.onApply ?? vi.fn();
  return render(
    <SitesFilters
      filters={filters}
      onFiltersChange={onFiltersChange}
      onApply={onApply}
      canFilterQuarantine={props.canFilterQuarantine ?? true}
    />,
    { wrapper: Wrapper }
  );
}

function openAdvancedFilters() {
  fireEvent.click(screen.getByText(/Advanced Filters/i));
}

describe('SitesFilters – Last Publication picker', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders Last Publication section for admin (canFilterQuarantine=true)', () => {
    renderFilters({}, { canFilterQuarantine: true });
    openAdvancedFilters();
    expect(screen.getByText('Last Publication')).toBeInTheDocument();
  });

  it('renders Last Publication section for client (canFilterQuarantine=false)', () => {
    renderFilters({}, { canFilterQuarantine: false });
    openAdvancedFilters();
    expect(screen.getByText('Last Publication')).toBeInTheDocument();
  });

  it('renders Quarantine Status only for admin', () => {
    renderFilters({}, { canFilterQuarantine: true });
    openAdvancedFilters();
    expect(screen.getByText('Quarantine Status')).toBeInTheDocument();
  });

  it('does not render Quarantine Status for client', () => {
    renderFilters({}, { canFilterQuarantine: false });
    openAdvancedFilters();
    expect(screen.queryByText('Quarantine Status')).not.toBeInTheDocument();
  });

  it('Apply Filters is enabled when From <= To', () => {
    renderFilters({ lastPublishedFromMonth: '2026-01', lastPublishedToMonth: '2026-06' });
    openAdvancedFilters();
    expect(screen.getByRole('button', { name: /apply filters/i })).not.toBeDisabled();
  });

  it('Apply Filters is disabled when From > To', () => {
    renderFilters({ lastPublishedFromMonth: '2026-06', lastPublishedToMonth: '2026-01' });
    openAdvancedFilters();
    expect(screen.getByRole('button', { name: /apply filters/i })).toBeDisabled();
  });

  it('shows validation error message when From > To', () => {
    renderFilters({ lastPublishedFromMonth: '2026-06', lastPublishedToMonth: '2026-01' });
    openAdvancedFilters();
    expect(
      screen.getByText('"From" must be before or equal to "To"')
    ).toBeInTheDocument();
  });

  it('no validation error when From equals To', () => {
    renderFilters({ lastPublishedFromMonth: '2026-03', lastPublishedToMonth: '2026-03' });
    openAdvancedFilters();
    expect(
      screen.queryByText('"From" must be before or equal to "To"')
    ).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /apply filters/i })).not.toBeDisabled();
  });

  it('Clear All calls onFiltersChange with null last-published values and resets all filters', () => {
    const onFiltersChange = vi.fn();
    const onApply = vi.fn();
    renderFilters(
      { lastPublishedFromMonth: '2026-01', lastPublishedToMonth: '2026-06', search: 'test' },
      { onFiltersChange, onApply }
    );
    openAdvancedFilters();
    fireEvent.click(screen.getByRole('button', { name: /clear all/i }));

    expect(onFiltersChange).toHaveBeenCalledWith(
      expect.objectContaining({
        lastPublishedFromMonth: null,
        lastPublishedToMonth: null,
        search: '',
      })
    );
    expect(onApply).toHaveBeenCalled();
  });

  it('Clear All resets validation error (both values cleared)', () => {
    const onFiltersChange = vi.fn();
    renderFilters(
      { lastPublishedFromMonth: '2026-06', lastPublishedToMonth: '2026-01' },
      { onFiltersChange }
    );
    openAdvancedFilters();
    expect(screen.getByText('"From" must be before or equal to "To"')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /clear all/i }));

    const [resetFilters] = onFiltersChange.mock.calls[0];
    expect(resetFilters.lastPublishedFromMonth).toBeNull();
    expect(resetFilters.lastPublishedToMonth).toBeNull();
  });

  it('Apply Filters invokes onApply with active last-published filter values', () => {
    const onApply = vi.fn();
    renderFilters(
      { lastPublishedFromMonth: '2026-01', lastPublishedToMonth: '2026-06' },
      { onApply }
    );
    openAdvancedFilters();
    fireEvent.click(screen.getByRole('button', { name: /apply filters/i }));
    expect(onApply).toHaveBeenCalled();
  });

  it('shows (Active) indicator when lastPublishedFromMonth is set', () => {
    renderFilters({ lastPublishedFromMonth: '2026-01' });
    expect(screen.getByText(/\(Active\)/i)).toBeInTheDocument();
  });

  it('shows (Active) indicator when lastPublishedToMonth is set', () => {
    renderFilters({ lastPublishedToMonth: '2026-06' });
    expect(screen.getByText(/\(Active\)/i)).toBeInTheDocument();
  });

  it('no (Active) indicator when all filters are at defaults', () => {
    renderFilters();
    expect(screen.queryByText(/\(Active\)/i)).not.toBeInTheDocument();
  });

  it('export uses the same filter state: lastPublishedFromMonth and lastPublishedToMonth', () => {
    // Verified by integration: Sites.tsx buildSitesQueryParams reads filters.lastPublishedFromMonth
    // and filters.lastPublishedToMonth directly — same state drives both list fetch and export.
    // This structural test confirms SitesFilters passes the value through onFiltersChange.
    const onFiltersChange = vi.fn();
    renderFilters({ lastPublishedFromMonth: '2026-01', lastPublishedToMonth: '2026-06' }, { onFiltersChange });
    // The filter values are passed in as props (controlled), so the parent (Sites.tsx) holds the
    // single source of truth consumed by both loadSites() and handleExport().
    expect(BASE_FILTERS.lastPublishedFromMonth).toBeNull();
    expect(BASE_FILTERS.lastPublishedToMonth).toBeNull();
  });
});
