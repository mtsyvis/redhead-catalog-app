import { useMemo, useState } from 'react';
import type { DragEvent, KeyboardEvent } from 'react';
import { Box, Button, CircularProgress, IconButton, Tooltip, Typography } from '@mui/material';
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import { ApiClientError } from '../../../services/api.client';
import type {
  TableViewDensity,
  TableViewSettings,
  TableViewType,
} from '../../../types/tableViews.types';
import {
  createSitesViewSettings,
  insertSitesColumnsByDefaultOrder,
  normalizeSitesVisibleColumnIds,
} from '../table-views/sitesTableColumns';
import type { SitesColumnMetadata } from '../table-views/sitesTableColumns';
import { SitesColumnsDrawer } from './SitesColumnsDrawer';
import { SitesDeleteViewDialog, SitesViewNameDialog } from './SitesTableViewDialogs';
import {
  GROUP_ORDER,
  areColumnListsEqual,
  haveSameColumnSet,
  pluralize,
  toolbarControlSx,
} from './SitesTableViewToolbar.helpers';
import type {
  NameDialogMode,
  NameDialogState,
  SitesTableViewToolbarProps,
} from './SitesTableViewToolbar.types';
import { SitesHiddenFiltersPopover } from './SitesHiddenFiltersPopover';
import { SitesToolbarPill } from './SitesToolbarPill';
import { SitesViewSelectorMenu, SitesViewOverflowMenu } from './SitesTableViewMenus';
import { SitesViewChangesPopover } from './SitesViewChangesPopover';
import { SitesExportMenu } from '../export/SitesExportMenu';

export function SitesTableViewToolbar({
  tableViews,
  hiddenFilteredColumns,
  canExport,
  exporting,
  loading,
  resultCount,
  resultLoading,
  onShowFilteredColumns,
  onClearHiddenFilters,
  onDownloadExcel,
  onSaveToGoogleDrive,
  onSuccess,
  onError,
}: SitesTableViewToolbarProps) {
  const [viewAnchor, setViewAnchor] = useState<HTMLElement | null>(null);
  const [overflowAnchor, setOverflowAnchor] = useState<HTMLElement | null>(null);
  const [columnsDrawerOpen, setColumnsDrawerOpen] = useState(false);
  const [columnsDrawerTab, setColumnsDrawerTab] = useState(0);
  const [columnsSearch, setColumnsSearch] = useState('');
  const [drawerVisibleColumnIds, setDrawerVisibleColumnIds] = useState<string[]>([]);
  const [draggedColumnId, setDraggedColumnId] = useState<string | null>(null);
  const [nameDialog, setNameDialog] = useState<NameDialogState | null>(null);
  const [nameDialogError, setNameDialogError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false);
  const [hiddenFiltersAnchor, setHiddenFiltersAnchor] = useState<HTMLElement | null>(null);
  const [editedAnchor, setEditedAnchor] = useState<HTMLElement | null>(null);

  const isCustomView = tableViews.activeView.type === 'custom';
  const visibleCount = tableViews.visibleColumnIds.length;
  const totalConfigurableCount = tableViews.allowedViewColumns.length;
  const hiddenFilteredColumnIds = useMemo(
    () => new Set(hiddenFilteredColumns.map((column) => column.id)),
    [hiddenFilteredColumns]
  );

  const drawerVisibleSet = useMemo(() => new Set(drawerVisibleColumnIds), [drawerVisibleColumnIds]);

  const columnById = useMemo(
    () => new Map(tableViews.allowedViewColumns.map((column) => [column.id, column])),
    [tableViews.allowedViewColumns]
  );

  const filteredColumns = useMemo(() => {
    const query = columnsSearch.trim().toLowerCase();
    if (!query) return tableViews.allowedViewColumns;

    return tableViews.allowedViewColumns.filter(
      (column) =>
        column.label.toLowerCase().includes(query) || column.group.toLowerCase().includes(query)
    );
  }, [columnsSearch, tableViews.allowedViewColumns]);

  const columnsByGroup = useMemo(
    () =>
      GROUP_ORDER.map((group) => ({
        group,
        columns: filteredColumns.filter((column) => column.group === group),
      })).filter((item) => item.columns.length > 0),
    [filteredColumns]
  );

  const drawerHasChanges = useMemo(
    () => !areColumnListsEqual(drawerVisibleColumnIds, tableViews.visibleColumnIds),
    [drawerVisibleColumnIds, tableViews.visibleColumnIds]
  );

  const drawerCanReset = useMemo(
    () => !areColumnListsEqual(drawerVisibleColumnIds, tableViews.activeSettings.visibleColumnIds),
    [drawerVisibleColumnIds, tableViews.activeSettings.visibleColumnIds]
  );

  const orderedDrawerColumns = useMemo(
    () =>
      drawerVisibleColumnIds
        .map((columnId) => columnById.get(columnId))
        .filter((column): column is SitesColumnMetadata => Boolean(column)),
    [columnById, drawerVisibleColumnIds]
  );

  const viewColumnChanges = useMemo(() => {
    const activeColumnIds = new Set(tableViews.activeSettings.visibleColumnIds);
    const visibleColumnIds = new Set(tableViews.visibleColumnIds);

    return {
      added: tableViews.visibleColumnIds
        .filter((columnId) => !activeColumnIds.has(columnId))
        .map((columnId) => columnById.get(columnId)?.label ?? columnId),
      hidden: tableViews.activeSettings.visibleColumnIds
        .filter((columnId) => !visibleColumnIds.has(columnId))
        .map((columnId) => columnById.get(columnId)?.label ?? columnId),
      resized: tableViews.allowedViewColumns
        .filter(
          (column) =>
            tableViews.columnWidths[column.id] !== tableViews.activeSettings.columnWidths[column.id]
        )
        .map((column) => column.label),
      reordered:
        haveSameColumnSet(tableViews.visibleColumnIds, tableViews.activeSettings.visibleColumnIds) &&
        !areColumnListsEqual(tableViews.visibleColumnIds, tableViews.activeSettings.visibleColumnIds),
    };
  }, [
    columnById,
    tableViews.activeSettings.columnWidths,
    tableViews.activeSettings.visibleColumnIds,
    tableViews.allowedViewColumns,
    tableViews.columnWidths,
    tableViews.visibleColumnIds,
  ]);

  const hasOnlyDisplaySettingChanges =
    tableViews.hasUnsavedChanges &&
    viewColumnChanges.added.length === 0 &&
    viewColumnChanges.hidden.length === 0 &&
    viewColumnChanges.resized.length === 0 &&
    !viewColumnChanges.reordered;

  const openColumnsDrawer = () => {
    setDrawerVisibleColumnIds(tableViews.visibleColumnIds);
    setColumnsDrawerTab(0);
    setColumnsSearch('');
    setColumnsDrawerOpen(true);
  };

  const closeColumnsDrawer = () => {
    if (actionLoading) return;
    setColumnsDrawerOpen(false);
  };

  const openNameDialog = (
    mode: NameDialogMode,
    name = '',
    settings: TableViewSettings | null = null,
    closeDrawerOnSuccess = false
  ) => {
    setNameDialog({ mode, name, settings, closeDrawerOnSuccess });
    setNameDialogError(null);
    setOverflowAnchor(null);
  };

  const buildSettings = (visibleColumnIds = tableViews.visibleColumnIds) =>
    createSitesViewSettings(visibleColumnIds, tableViews.density, tableViews.columnWidths);

  const getErrorMessage = (error: unknown, fallback: string) => {
    if (error instanceof ApiClientError) {
      return error.errors?.join(' ') || error.message;
    }

    return error instanceof Error ? error.message : fallback;
  };

  const applyViewSwitch = async (viewType: TableViewType, viewKey: string) => {
    setActionLoading(true);
    try {
      await tableViews.setActiveView(viewType, viewKey);
      setViewAnchor(null);
    } catch (error) {
      onError(getErrorMessage(error, 'Failed to switch table view'));
    } finally {
      setActionLoading(false);
    }
  };

  const requestViewSwitch = (viewType: TableViewType, viewKey: string) => {
    if (tableViews.activeView.type === viewType && tableViews.activeView.key === viewKey) {
      setViewAnchor(null);
      return;
    }

    void applyViewSwitch(viewType, viewKey);
  };

  const handleSaveCurrentDraft = async () => {
    if (!tableViews.hasUnsavedChanges) return;

    if (!isCustomView) {
      setEditedAnchor(null);
      openNameDialog('saveAs');
      return;
    }

    setActionLoading(true);
    setOverflowAnchor(null);
    try {
      await tableViews.updateCustomView(tableViews.activeView.key, undefined, buildSettings());
      setEditedAnchor(null);
      onSuccess('Table view saved');
    } catch (error) {
      onError(getErrorMessage(error, 'Failed to save table view'));
    } finally {
      setActionLoading(false);
    }
  };

  const handleSubmitNameDialog = async () => {
    if (!nameDialog) return;
    const trimmedName = nameDialog.name.trim();
    if (!trimmedName) {
      setNameDialogError('Enter a view name.');
      return;
    }

    setActionLoading(true);
    setNameDialogError(null);
    try {
      if (nameDialog.mode === 'rename') {
        if (!isCustomView) return;
        await tableViews.updateCustomView(tableViews.activeView.key, trimmedName);
        onSuccess('Table view renamed');
      } else {
        const created = await tableViews.createCustomView(
          trimmedName,
          nameDialog.settings ?? buildSettings()
        );
        onSuccess(`Saved table view: ${created.name}`);
      }

      if (nameDialog.closeDrawerOnSuccess) {
        setColumnsDrawerOpen(false);
      }
      setNameDialog(null);
    } catch (error) {
      setNameDialogError(getErrorMessage(error, 'Failed to save table view'));
    } finally {
      setActionLoading(false);
    }
  };

  const handleApplyDrawer = () => {
    tableViews.updateDraftVisibleColumns(drawerVisibleColumnIds);
    setColumnsDrawerOpen(false);
  };

  const handleDeleteView = async () => {
    if (!isCustomView) return;
    setActionLoading(true);
    try {
      await tableViews.deleteCustomView(tableViews.activeView.key);
      setDeleteConfirmOpen(false);
      onSuccess('Custom table view deleted');
    } catch (error) {
      onError(getErrorMessage(error, 'Failed to delete table view'));
    } finally {
      setActionLoading(false);
    }
  };

  const toggleDrawerColumn = (column: SitesColumnMetadata, checked: boolean) => {
    if (column.required) return;
    setDrawerVisibleColumnIds((current) =>
      checked
        ? insertSitesColumnsByDefaultOrder(current, [column.id], tableViews.allowedViewColumns)
        : normalizeSitesVisibleColumnIds(
            current.filter((columnId) => columnId !== column.id),
            tableViews.allowedViewColumns
          )
    );
  };

  const resetDrawerDraft = () => {
    setDrawerVisibleColumnIds(tableViews.activeSettings.visibleColumnIds);
  };

  const moveDrawerColumn = (columnId: string, targetIndex: number) => {
    const column = columnById.get(columnId);
    if (!column || column.required) return;

    setDrawerVisibleColumnIds((current) => {
      const normalized = normalizeSitesVisibleColumnIds(current, tableViews.allowedViewColumns);
      const fromIndex = normalized.indexOf(columnId);
      if (fromIndex === -1) return normalized;

      const lockedCount = normalized.filter((id) => columnById.get(id)?.required).length;
      const next = [...normalized];
      const [movedColumnId] = next.splice(fromIndex, 1);
      const nextTargetIndex = Math.max(lockedCount, Math.min(targetIndex, next.length));
      next.splice(nextTargetIndex, 0, movedColumnId);
      return normalizeSitesVisibleColumnIds(next, tableViews.allowedViewColumns);
    });
  };

  const moveDrawerColumnByDelta = (columnId: string, delta: number) => {
    const currentIndex = drawerVisibleColumnIds.indexOf(columnId);
    if (currentIndex === -1) return;
    moveDrawerColumn(columnId, currentIndex + delta);
  };

  const handleOrderDragStart = (event: DragEvent<HTMLElement>, columnId: string) => {
    setDraggedColumnId(columnId);
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', columnId);
  };

  const handleOrderDragOver = (event: DragEvent<HTMLElement>, targetColumnId: string) => {
    const sourceColumnId = draggedColumnId ?? event.dataTransfer.getData('text/plain');
    if (!sourceColumnId || sourceColumnId === targetColumnId) return;

    const sourceColumn = columnById.get(sourceColumnId);
    if (!sourceColumn || sourceColumn.required) return;

    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';

    const targetColumn = columnById.get(targetColumnId);
    const sourceIndex = drawerVisibleColumnIds.indexOf(sourceColumnId);
    const targetIndex = drawerVisibleColumnIds.indexOf(targetColumnId);
    if (sourceIndex === -1 || targetIndex === -1) return;

    const targetRect = event.currentTarget.getBoundingClientRect();
    const insertAfter = event.clientY > targetRect.top + targetRect.height / 2;
    const rawTargetIndex = targetColumn?.required ? 1 : targetIndex + (insertAfter ? 1 : 0);
    const adjustedTargetIndex = sourceIndex < rawTargetIndex ? rawTargetIndex - 1 : rawTargetIndex;

    if (adjustedTargetIndex !== sourceIndex) {
      moveDrawerColumn(sourceColumnId, adjustedTargetIndex);
    }
  };

  const handleOrderDrop = (event: DragEvent<HTMLElement>, targetColumnId: string) => {
    event.preventDefault();
    void targetColumnId;
    setDraggedColumnId(null);
  };

  const handleOrderKeyDown = (
    event: KeyboardEvent<HTMLElement>,
    column: SitesColumnMetadata
  ) => {
    if (column.required) return;

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      moveDrawerColumnByDelta(column.id, -1);
    }

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      moveDrawerColumnByDelta(column.id, 1);
    }
  };

  const resetToolbarDraft = () => {
    tableViews.resetDraftToActive();
    setOverflowAnchor(null);
    setEditedAnchor(null);
  };

  const handleDensityChange = (density: TableViewDensity) => {
    tableViews.updateDraftDensity(density);
    setOverflowAnchor(null);
  };

  const handleShowHiddenFilterColumns = () => {
    onShowFilteredColumns();
    setHiddenFiltersAnchor(null);
  };

  const handleClearHiddenFilters = () => {
    onClearHiddenFilters();
    setHiddenFiltersAnchor(null);
  };

  const activeCustomViewName = isCustomView ? tableViews.activeView.name : '';
  const hiddenFiltersCount = hiddenFilteredColumns.length;
  const hiddenFiltersLabel = `${hiddenFiltersCount} hidden ${pluralize(
    'filter',
    hiddenFiltersCount
  )}`;
  const hiddenColumnsLabel = pluralize('column', hiddenFiltersCount);
  const columnsButtonLabel = pluralize('Column', visibleCount);
  const formattedResultCount = resultCount.toLocaleString();

  return (
    <>
      <Box
        sx={{
          display: 'flex',
          gap: 1,
          alignItems: 'center',
          flexWrap: 'wrap',
          px: 1.5,
          py: 0.75,
          borderBottom: 1,
          borderColor: 'divider',
        }}
      >
        <Button
          size="small"
          variant="outlined"
          endIcon={<KeyboardArrowDownIcon fontSize="small" />}
          onClick={(event) => setViewAnchor(event.currentTarget)}
          disabled={tableViews.loading || actionLoading}
          sx={toolbarControlSx}
        >
          <Box
            component="span"
            sx={{
              display: 'inline-flex',
              alignItems: 'center',
              minWidth: 0,
              maxWidth: { xs: 180, sm: 260 },
            }}
          >
            <Box component="span" sx={{ flexShrink: 0 }}>
              View:&nbsp;
            </Box>
            <Box
              component="span"
              sx={{
                minWidth: 0,
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap',
              }}
            >
              {tableViews.activeView.name}
            </Box>
          </Box>
        </Button>

        <Button
          size="small"
          variant="outlined"
          onClick={openColumnsDrawer}
          disabled={tableViews.loading || actionLoading}
          sx={toolbarControlSx}
        >
          {columnsButtonLabel}: {visibleCount} / {totalConfigurableCount}
        </Button>

        {tableViews.hasUnsavedChanges && (
          <SitesToolbarPill
            label="View changed"
            tooltip="This view has unsaved column changes"
            onClick={(event) => setEditedAnchor(event.currentTarget)}
          />
        )}

        {hiddenFiltersCount > 0 && (
          <SitesToolbarPill
            label={hiddenFiltersLabel}
            onClick={(event) => setHiddenFiltersAnchor(event.currentTarget)}
          />
        )}

        <Typography
          variant="body2"
          color="text.secondary"
          sx={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: 0.75,
            minHeight: 32,
            whiteSpace: 'nowrap',
            ml: { xs: 0, sm: 0.5 },
          }}
        >
          {formattedResultCount} {pluralize('result', resultCount)}
          {resultLoading && (
            <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}>
              <CircularProgress size={12} color="inherit" />
              Updating...
            </Box>
          )}
        </Typography>

        <Box sx={{ flexGrow: 1 }} />

        {canExport && (
          <SitesExportMenu
            exporting={exporting}
            loading={loading}
            onDownloadExcel={onDownloadExcel}
            onSaveToGoogleDrive={onSaveToGoogleDrive}
          />
        )}

        <Tooltip title="More view options">
          <span>
            <IconButton
              size="small"
              aria-label="More table view options"
              onClick={(event) => setOverflowAnchor(event.currentTarget)}
              disabled={tableViews.loading || actionLoading}
            >
              <MoreVertIcon fontSize="small" />
            </IconButton>
          </span>
        </Tooltip>
      </Box>

      <SitesViewSelectorMenu
        anchorEl={viewAnchor}
        tableViews={tableViews}
        onClose={() => setViewAnchor(null)}
        onSwitchView={requestViewSwitch}
      />

      <SitesViewOverflowMenu
        anchorEl={overflowAnchor}
        activeCustomViewName={activeCustomViewName}
        isCustomView={isCustomView}
        tableViews={tableViews}
        onClose={() => setOverflowAnchor(null)}
        onOpenNameDialog={openNameDialog}
        onBuildSettings={buildSettings}
        onDensityChange={handleDensityChange}
        onDeleteView={() => {
          setOverflowAnchor(null);
          setDeleteConfirmOpen(true);
        }}
      />

      <SitesViewChangesPopover
        anchorEl={editedAnchor}
        activeViewName={tableViews.activeView.name}
        actionLoading={actionLoading}
        isCustomView={isCustomView}
        changes={viewColumnChanges}
        hasOnlyDisplaySettingChanges={hasOnlyDisplaySettingChanges}
        onClose={() => setEditedAnchor(null)}
        onReset={resetToolbarDraft}
        onSave={handleSaveCurrentDraft}
      />

      <SitesHiddenFiltersPopover
        anchorEl={hiddenFiltersAnchor}
        hiddenFilteredColumns={hiddenFilteredColumns}
        hiddenFiltersCount={hiddenFiltersCount}
        hiddenColumnsLabel={hiddenColumnsLabel}
        onClose={() => setHiddenFiltersAnchor(null)}
        onClear={handleClearHiddenFilters}
        onShow={handleShowHiddenFilterColumns}
      />

      <SitesColumnsDrawer
        actionLoading={actionLoading}
        columnsByGroup={columnsByGroup}
        drawerCanReset={drawerCanReset}
        drawerHasChanges={drawerHasChanges}
        draggedColumnId={draggedColumnId}
        hiddenFilteredColumnIds={hiddenFilteredColumnIds}
        open={columnsDrawerOpen}
        orderedColumns={orderedDrawerColumns}
        search={columnsSearch}
        tab={columnsDrawerTab}
        totalConfigurableCount={totalConfigurableCount}
        visibleColumnIds={drawerVisibleColumnIds}
        visibleColumnSet={drawerVisibleSet}
        onApply={handleApplyDrawer}
        onChangeSearch={setColumnsSearch}
        onChangeTab={setColumnsDrawerTab}
        onClose={closeColumnsDrawer}
        onDragEnd={() => setDraggedColumnId(null)}
        onMoveColumnByDelta={moveDrawerColumnByDelta}
        onOrderDragOver={handleOrderDragOver}
        onOrderDragStart={handleOrderDragStart}
        onOrderDrop={handleOrderDrop}
        onOrderKeyDown={handleOrderKeyDown}
        onReset={resetDrawerDraft}
        onToggleColumn={toggleDrawerColumn}
      />

      <SitesViewNameDialog
        actionLoading={actionLoading}
        error={nameDialogError}
        nameDialog={nameDialog}
        onClose={() => {
          if (!actionLoading) setNameDialog(null);
        }}
        onChangeName={(name) => {
          setNameDialog((current) => (current ? { ...current, name } : current));
          setNameDialogError(null);
        }}
        onSubmit={handleSubmitNameDialog}
      />

      <SitesDeleteViewDialog
        actionLoading={actionLoading}
        open={deleteConfirmOpen}
        viewName={tableViews.activeView.name}
        onClose={() => {
          if (!actionLoading) setDeleteConfirmOpen(false);
        }}
        onDelete={handleDeleteView}
      />

    </>
  );
}
