import { TableViewToolbar } from '../../common/table-views/TableViewToolbar';
import { SitesExportMenu } from '../export/SitesExportMenu';
import { GROUP_ORDER, pluralize } from './SitesTableViewToolbar.helpers';
import { formatInteger } from '../../../utils/numberFormat';
import type { SitesTableViewToolbarProps } from './SitesTableViewToolbar.types';

export function SitesTableViewToolbar({
  tableViews,
  hiddenFilteredColumns,
  canExport,
  exporting,
  loading,
  includeQuarantined,
  quarantineFilter,
  exportUsageLimits,
  selectionCount,
  resultCount,
  resultSearchedCount = 0,
  resultNotFoundCount = 0,
  resultHiddenNotFoundCount = 0,
  resultLoading,
  onShowFilteredColumns,
  onClearHiddenFilters,
  onIncludeQuarantinedChange,
  onDownloadExcel,
  onSaveToGoogleDrive,
  onSuccess,
  onError,
}: SitesTableViewToolbarProps) {
  const formattedResultCount = formatInteger(resultCount);
  const formattedSearchedResultCount = formatInteger(resultSearchedCount);
  const formattedNotFoundResultCount = formatInteger(resultNotFoundCount);
  const formattedHiddenNotFoundResultCount = formatInteger(resultHiddenNotFoundCount);
  const searchedFoundCount = Math.max(
    0,
    resultSearchedCount - resultNotFoundCount - resultHiddenNotFoundCount
  );
  const formattedSearchedFoundCount = formatInteger(searchedFoundCount);
  const resultLabel =
    resultSearchedCount > 0 && resultHiddenNotFoundCount > 0
      ? `${formattedResultCount} visible of ${formattedSearchedResultCount} searched · ${formattedHiddenNotFoundResultCount} not found hidden by filters`
      : resultSearchedCount > 0 && resultNotFoundCount > 0
        ? `${formattedSearchedResultCount} searched · ${formattedSearchedFoundCount} found · ${formattedNotFoundResultCount} not found`
        : resultSearchedCount > 0
          ? `${formattedSearchedResultCount} searched · ${formattedSearchedFoundCount} found`
          : `${formattedResultCount} ${pluralize('result', resultCount)}`;

  return (
    <TableViewToolbar
      tableViews={tableViews}
      columnGroups={GROUP_ORDER}
      resultLabel={resultLabel}
      resultLoading={resultLoading}
      hiddenFilteredColumns={hiddenFilteredColumns}
      onShowFilteredColumns={onShowFilteredColumns}
      onClearHiddenFilters={onClearHiddenFilters}
      onSuccess={onSuccess}
      onError={onError}
      actions={
        canExport ? (
          <SitesExportMenu
            exporting={exporting}
            loading={loading}
            includeQuarantined={includeQuarantined}
            quarantineFilter={quarantineFilter}
            exportUsageLimits={exportUsageLimits}
            selectionCount={selectionCount}
            onIncludeQuarantinedChange={onIncludeQuarantinedChange}
            onDownloadExcel={onDownloadExcel}
            onSaveToGoogleDrive={onSaveToGoogleDrive}
          />
        ) : undefined
      }
    />
  );
}
