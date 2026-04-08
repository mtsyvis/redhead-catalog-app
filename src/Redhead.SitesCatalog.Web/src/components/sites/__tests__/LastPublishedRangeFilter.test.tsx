import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { LastPublishedRangeFilter } from '../LastPublishedRangeFilter';
import { toApiMonth, fromApiMonth } from '../../../utils/lastPublishedMonth';
import dayjs from 'dayjs';

// MUI X v8 DatePicker renders a section-based field (role="group") with a hidden
// <input aria-hidden="true" value="MMM YYYY"> for the formatted value.

function Wrapper({ children }: { children: React.ReactNode }) {
  return (
    <LocalizationProvider dateAdapter={AdapterDayjs}>{children}</LocalizationProvider>
  );
}

function renderFilter(props: Partial<React.ComponentProps<typeof LastPublishedRangeFilter>> = {}) {
  return render(
    <LastPublishedRangeFilter
      fromValue={null}
      toValue={null}
      onFromChange={vi.fn()}
      onToChange={vi.fn()}
      {...props}
    />,
    { wrapper: Wrapper }
  );
}

describe('toApiMonth', () => {
  it('returns null for null input', () => {
    expect(toApiMonth(null)).toBeNull();
  });

  it('formats a dayjs value to yyyy-MM', () => {
    expect(toApiMonth(dayjs('2026-01', 'YYYY-MM'))).toBe('2026-01');
  });

  it('formats June 2025 correctly', () => {
    expect(toApiMonth(dayjs('2025-06', 'YYYY-MM'))).toBe('2025-06');
  });

  it('formats December correctly with zero-padding', () => {
    expect(toApiMonth(dayjs('2025-12', 'YYYY-MM'))).toBe('2025-12');
  });

  it('returns null for invalid dayjs value', () => {
    expect(toApiMonth(dayjs('invalid'))).toBeNull();
  });
});

describe('fromApiMonth', () => {
  it('returns null for null input', () => {
    expect(fromApiMonth(null)).toBeNull();
  });

  it('parses yyyy-MM string to dayjs', () => {
    const result = fromApiMonth('2026-03');
    expect(result).not.toBeNull();
    expect(result!.year()).toBe(2026);
    expect(result!.month()).toBe(2); // dayjs months are 0-indexed
  });

  it('round-trips through toApiMonth', () => {
    expect(toApiMonth(fromApiMonth('2025-11'))).toBe('2025-11');
  });
});

describe('LastPublishedRangeFilter', () => {
  it('renders Last Publication label for admin view', () => {
    renderFilter();
    expect(screen.getByText('Last Publication')).toBeInTheDocument();
  });

  it('renders Last Publication label for client view (same component)', () => {
    renderFilter();
    expect(screen.getByText('Last Publication')).toBeInTheDocument();
  });

  it('renders From picker group', () => {
    renderFilter();
    // MUI X v8 renders each DatePicker as role="group" labeled by the field label
    expect(screen.getByRole('group', { name: /^From/i })).toBeInTheDocument();
  });

  it('renders To picker group', () => {
    renderFilter();
    expect(screen.getByRole('group', { name: /^To/i })).toBeInTheDocument();
  });

  it('displays human-friendly "Jan 2026" for fromValue "2026-01"', () => {
    // MUI X v8 keeps a hidden <input aria-hidden="true"> with the formatted value
    renderFilter({ fromValue: '2026-01' });
    expect(screen.getByDisplayValue('Jan 2026')).toBeInTheDocument();
  });

  it('displays human-friendly "Jun 2026" for toValue "2026-06"', () => {
    renderFilter({ toValue: '2026-06' });
    expect(screen.getByDisplayValue('Jun 2026')).toBeInTheDocument();
  });

  it('displays both From and To formatted values simultaneously', () => {
    renderFilter({ fromValue: '2026-01', toValue: '2026-12' });
    expect(screen.getByDisplayValue('Jan 2026')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Dec 2026')).toBeInTheDocument();
  });

  it('onFromChange prop is not called on initial render', () => {
    const onFromChange = vi.fn();
    renderFilter({ onFromChange });
    expect(onFromChange).not.toHaveBeenCalled();
  });

  it('onToChange prop is not called on initial render', () => {
    const onToChange = vi.fn();
    renderFilter({ onToChange });
    expect(onToChange).not.toHaveBeenCalled();
  });

  it('does not show error text by default', () => {
    renderFilter();
    expect(screen.queryByText(/"From" must be before/i)).not.toBeInTheDocument();
  });

  it('shows error message when error prop is provided (invalid From > To)', () => {
    renderFilter({
      fromValue: '2026-06',
      toValue: '2026-01',
      error: '"From" must be before or equal to "To"',
    });
    expect(
      screen.getByText('"From" must be before or equal to "To"')
    ).toBeInTheDocument();
  });
});
