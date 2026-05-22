import type { TableViewDensity } from '../../../types/tableViews.types';
import type { SitesColumnGroup } from '../table-views/sitesTableColumns';

export const DENSITY_OPTIONS: Array<{ value: TableViewDensity; label: string }> = [
  { value: 'compact', label: 'Compact' },
  { value: 'standard', label: 'Standard' },
  { value: 'comfortable', label: 'Comfortable' },
];

export const GROUP_ORDER: SitesColumnGroup[] = [
  'Main',
  'SEO metrics',
  'Prices',
  'Publication',
  'Admin',
];

export const toolbarControlSx = {
  borderColor: 'divider',
  color: 'text.primary',
  bgcolor: 'background.paper',
};

export const toolbarPillSx = {
  minHeight: 30,
  px: 1.25,
  borderColor: 'divider',
  color: 'text.secondary',
  bgcolor: 'background.paper',
  borderRadius: 999,
  '&:hover': {
    borderColor: 'text.secondary',
    bgcolor: 'action.hover',
    color: 'text.primary',
  },
};

export const popoverPaperSx = {
  width: 340,
  maxWidth: 'calc(100vw - 32px)',
  p: 2,
};

export const searchInputSx = {
  '& .MuiOutlinedInput-notchedOutline': {
    borderColor: 'divider',
  },
  '&:hover .MuiOutlinedInput-notchedOutline': {
    borderColor: 'text.secondary',
  },
  '&.Mui-focused .MuiOutlinedInput-notchedOutline': {
    borderColor: 'primary.main',
    borderWidth: 1,
  },
};

export function areColumnListsEqual(left: string[], right: string[]): boolean {
  if (left.length !== right.length) return false;
  return left.every((columnId, index) => columnId === right[index]);
}

export function haveSameColumnSet(left: string[], right: string[]): boolean {
  if (left.length !== right.length) return false;
  const rightColumnIds = new Set(right);
  return left.every((columnId) => rightColumnIds.has(columnId));
}

export function pluralize(word: string, count: number): string {
  return count === 1 ? word : `${word}s`;
}
