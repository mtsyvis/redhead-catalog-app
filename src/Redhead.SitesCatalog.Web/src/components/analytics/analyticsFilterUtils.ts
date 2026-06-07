import dayjs, { type Dayjs } from 'dayjs';
import type {
  AnalyticsClientOption,
  BusinessDemandDestinationFilter,
  BusinessDemandStatusFilter,
} from '../../types/analytics.types';

export type DateRangePreset = 'last7' | 'last30' | 'last90' | 'custom';
export type DestinationFilterValue = 'all' | BusinessDemandDestinationFilter;
export type StatusFilterValue = 'all' | BusinessDemandStatusFilter;

export const ALL_CLIENT_OPTION: AnalyticsClientOption = {
  id: 'all',
  email: '',
  displayName: 'All clients',
};

export function formatApiDate(value: Dayjs): string {
  return value.format('YYYY-MM-DD');
}

export function getPresetRange(preset: Exclude<DateRangePreset, 'custom'>) {
  const today = dayjs().startOf('day');
  const days = preset === 'last7' ? 7 : preset === 'last90' ? 90 : 30;
  return {
    from: today.subtract(days - 1, 'day'),
    to: today,
  };
}

export function getClientOptionLabel(option: AnalyticsClientOption): string {
  if (option.id === ALL_CLIENT_OPTION.id) return option.displayName;
  return option.displayName === option.email
    ? option.email
    : `${option.displayName} (${option.email})`;
}
