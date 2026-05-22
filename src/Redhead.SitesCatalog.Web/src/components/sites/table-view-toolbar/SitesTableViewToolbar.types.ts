import type { TableViewSettings } from '../../../types/tableViews.types';
import type { SitesColumnMetadata } from '../table-views/sitesTableColumns';
import type { useSitesTableViews } from '../table-views/useSitesTableViews';

export type SitesTableViewsState = ReturnType<typeof useSitesTableViews>;

export type NameDialogMode = 'saveAs' | 'rename' | 'duplicate';

export interface NameDialogState {
  mode: NameDialogMode;
  name: string;
  settings: TableViewSettings | null;
  closeDrawerOnSuccess: boolean;
}

export interface SitesTableViewToolbarProps {
  tableViews: SitesTableViewsState;
  hiddenFilteredColumns: SitesColumnMetadata[];
  onShowFilteredColumns: () => void;
  onClearHiddenFilters: () => void;
  onSuccess: (message: string) => void;
  onError: (message: string) => void;
}

export interface ViewColumnChanges {
  added: string[];
  hidden: string[];
  reordered: boolean;
}
