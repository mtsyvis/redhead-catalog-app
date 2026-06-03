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
  canExport: boolean;
  exporting: boolean;
  loading: boolean;
  resultCount: number;
  resultSearchedCount?: number;
  resultNotFoundCount?: number;
  resultHiddenNotFoundCount?: number;
  resultLoading: boolean;
  onShowFilteredColumns: () => void;
  onClearHiddenFilters: () => void;
  onDownloadExcel: () => void;
  onSaveToGoogleDrive: () => void;
  onSuccess: (message: string) => void;
  onError: (message: string) => void;
}

export interface ViewColumnChanges {
  added: string[];
  hidden: string[];
  resized: string[];
  reordered: boolean;
}
