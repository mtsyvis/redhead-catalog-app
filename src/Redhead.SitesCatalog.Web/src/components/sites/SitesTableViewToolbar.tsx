import { useMemo, useState } from 'react';
import type { DragEvent, KeyboardEvent, MouseEvent } from 'react';
import {
  Box,
  Button,
  Checkbox,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Drawer,
  IconButton,
  InputAdornment,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  ListSubheader,
  Menu,
  MenuItem,
  Popover,
  Stack,
  Tab,
  Tabs,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import CheckIcon from '@mui/icons-material/Check';
import DragIndicatorIcon from '@mui/icons-material/DragIndicator';
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown';
import KeyboardArrowUpIcon from '@mui/icons-material/KeyboardArrowUp';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import SearchIcon from '@mui/icons-material/Search';
import { BrandButton } from '../common/BrandButton';
import { ApiClientError } from '../../services/api.client';
import type {
  TableViewDensity,
  TableViewSettings,
  TableViewType,
} from '../../types/tableViews.types';
import {
  createSitesViewSettings,
  insertSitesColumnsByDefaultOrder,
  normalizeSitesVisibleColumnIds,
} from './sitesTableColumns';
import type { SitesColumnGroup, SitesColumnMetadata } from './sitesTableColumns';
import type { useSitesTableViews } from './useSitesTableViews';

type SitesTableViewsState = ReturnType<typeof useSitesTableViews>;

type NameDialogMode = 'saveAs' | 'rename' | 'duplicate';

interface NameDialogState {
  mode: NameDialogMode;
  name: string;
  settings: TableViewSettings | null;
  closeDrawerOnSuccess: boolean;
}

interface SitesTableViewToolbarProps {
  tableViews: SitesTableViewsState;
  hiddenFilteredColumns: SitesColumnMetadata[];
  onShowFilteredColumns: () => void;
  onClearHiddenFilters: () => void;
  onSuccess: (message: string) => void;
  onError: (message: string) => void;
}

interface ViewColumnChanges {
  added: string[];
  hidden: string[];
  reordered: boolean;
}

interface ToolbarPillProps {
  label: string;
  tooltip?: string;
  onClick: (event: MouseEvent<HTMLElement>) => void;
}

interface ViewChangesPopoverProps {
  anchorEl: HTMLElement | null;
  activeViewName: string;
  actionLoading: boolean;
  isCustomView: boolean;
  changes: ViewColumnChanges;
  hasOnlyDisplaySettingChanges: boolean;
  onClose: () => void;
  onReset: () => void;
  onSave: () => void;
}

interface HiddenFiltersPopoverProps {
  anchorEl: HTMLElement | null;
  hiddenFilteredColumns: SitesColumnMetadata[];
  hiddenFiltersCount: number;
  hiddenColumnsLabel: string;
  onClose: () => void;
  onClear: () => void;
  onShow: () => void;
}

const DENSITY_OPTIONS: Array<{ value: TableViewDensity; label: string }> = [
  { value: 'compact', label: 'Compact' },
  { value: 'standard', label: 'Standard' },
  { value: 'comfortable', label: 'Comfortable' },
];

const GROUP_ORDER: SitesColumnGroup[] = ['Main', 'SEO metrics', 'Prices', 'Publication', 'Admin'];

const toolbarControlSx = {
  borderColor: 'divider',
  color: 'text.primary',
  bgcolor: 'background.paper',
};

const toolbarPillSx = {
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

const popoverPaperSx = {
  width: 340,
  maxWidth: 'calc(100vw - 32px)',
  p: 2,
};

const searchInputSx = {
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

export function SitesTableViewToolbar({
  tableViews,
  hiddenFilteredColumns,
  onShowFilteredColumns,
  onClearHiddenFilters,
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
      reordered:
        haveSameColumnSet(tableViews.visibleColumnIds, tableViews.activeSettings.visibleColumnIds) &&
        !areColumnListsEqual(tableViews.visibleColumnIds, tableViews.activeSettings.visibleColumnIds),
    };
  }, [columnById, tableViews.activeSettings.visibleColumnIds, tableViews.visibleColumnIds]);

  const hasOnlyDisplaySettingChanges =
    tableViews.hasUnsavedChanges &&
    viewColumnChanges.added.length === 0 &&
    viewColumnChanges.hidden.length === 0 &&
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
    createSitesViewSettings(visibleColumnIds, tableViews.density);

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
  const nameDialogTitle =
    nameDialog?.mode === 'rename'
      ? 'Rename view'
      : nameDialog?.mode === 'duplicate'
        ? 'Duplicate view'
        : 'Save as custom view';
  const nameDialogAction =
    nameDialog?.mode === 'rename'
      ? 'Rename'
      : nameDialog?.mode === 'duplicate'
        ? 'Duplicate'
        : 'Save as custom view';

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
          <ToolbarPill
            label="View changed"
            tooltip="This view has unsaved display changes"
            onClick={(event) => setEditedAnchor(event.currentTarget)}
          />
        )}

        {hiddenFiltersCount > 0 && (
          <ToolbarPill
            label={hiddenFiltersLabel}
            onClick={(event) => setHiddenFiltersAnchor(event.currentTarget)}
          />
        )}

        <Box sx={{ flexGrow: 1 }} />

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

      <Menu
        anchorEl={viewAnchor}
        open={Boolean(viewAnchor)}
        onClose={() => setViewAnchor(null)}
        MenuListProps={{ dense: true }}
      >
        <ListSubheader>System views</ListSubheader>
        {tableViews.systemViews.map((view) => {
          const active =
            tableViews.activeView.type === 'system' && tableViews.activeView.key === view.key;
          return (
            <MenuItem
              key={view.key}
              selected={active}
              onClick={() => requestViewSwitch('system', view.key)}
            >
              <ListItemIcon>{active ? <CheckIcon fontSize="small" /> : null}</ListItemIcon>
              <ListItemText>{view.name}</ListItemText>
            </MenuItem>
          );
        })}
        <Divider />
        <ListSubheader>My views</ListSubheader>
        {tableViews.customViews.length === 0 && (
          <MenuItem disabled>
            <ListItemText secondary="No custom views yet" />
          </MenuItem>
        )}
        {tableViews.customViews.map((view) => {
          const active =
            tableViews.activeView.type === 'custom' && tableViews.activeView.key === view.id;
          return (
            <MenuItem
              key={view.id}
              selected={active}
              onClick={() => requestViewSwitch('custom', view.id)}
            >
              <ListItemIcon>{active ? <CheckIcon fontSize="small" /> : null}</ListItemIcon>
              <ListItemText>{view.name}</ListItemText>
            </MenuItem>
          );
        })}
      </Menu>

      <Menu
        anchorEl={overflowAnchor}
        open={Boolean(overflowAnchor)}
        onClose={() => setOverflowAnchor(null)}
        MenuListProps={{ dense: true }}
      >
        {isCustomView && (
          <MenuItem onClick={() => openNameDialog('rename', activeCustomViewName)}>
            Rename view
          </MenuItem>
        )}
        <MenuItem
          onClick={() =>
            openNameDialog('duplicate', `${tableViews.activeView.name} copy`, buildSettings())
          }
        >
          Duplicate view
        </MenuItem>
        <Divider />
        <ListSubheader>Density</ListSubheader>
        {DENSITY_OPTIONS.map((option) => (
          <MenuItem key={option.value} onClick={() => handleDensityChange(option.value)}>
            <ListItemIcon>
              {tableViews.density === option.value ? <CheckIcon fontSize="small" /> : null}
            </ListItemIcon>
            <ListItemText>{option.label}</ListItemText>
          </MenuItem>
        ))}
        {isCustomView && (
          <>
            <Divider />
            <MenuItem
              onClick={() => {
                setOverflowAnchor(null);
                setDeleteConfirmOpen(true);
              }}
              sx={{ color: 'error.main' }}
            >
              Delete view
            </MenuItem>
          </>
        )}
      </Menu>

      <ViewChangesPopover
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

      <HiddenFiltersPopover
        anchorEl={hiddenFiltersAnchor}
        hiddenFilteredColumns={hiddenFilteredColumns}
        hiddenFiltersCount={hiddenFiltersCount}
        hiddenColumnsLabel={hiddenColumnsLabel}
        onClose={() => setHiddenFiltersAnchor(null)}
        onClear={handleClearHiddenFilters}
        onShow={handleShowHiddenFilterColumns}
      />

      <Drawer
        anchor="right"
        open={columnsDrawerOpen}
        onClose={closeColumnsDrawer}
        PaperProps={{
          sx: {
            width: { xs: '100%', sm: 420 },
            maxWidth: '100%',
            display: 'flex',
            flexDirection: 'column',
            bgcolor: 'background.paper',
            overflow: 'hidden',
          },
        }}
      >
        <Box sx={{ px: 2, pt: 1.5, pb: 0.75, bgcolor: 'background.paper' }}>
          <Typography variant="h6">Columns</Typography>
          <Typography variant="body2" color="text.secondary">
            {drawerVisibleColumnIds.length} of {totalConfigurableCount} visible
          </Typography>
        </Box>

        <Tabs
          value={columnsDrawerTab}
          onChange={(_event, value: number) => setColumnsDrawerTab(value)}
          variant="fullWidth"
          sx={{
            minHeight: 34,
            borderBottom: 1,
            borderColor: 'divider',
            bgcolor: 'background.paper',
          }}
          slotProps={{ indicator: { sx: { height: 2 } } }}
        >
          <Tab
            label="Columns"
            sx={{ minHeight: 34, py: 0.5, textTransform: 'none', fontSize: 13 }}
          />
          <Tab
            label="Order"
            sx={{ minHeight: 34, py: 0.5, textTransform: 'none', fontSize: 13 }}
          />
        </Tabs>

        {columnsDrawerTab === 0 && (
          <Box sx={{ px: 2, pt: 0.75, pb: 0.5, bgcolor: 'background.paper' }}>
            <TextField
              size="small"
              placeholder="Search columns..."
              value={columnsSearch}
              onChange={(event) => setColumnsSearch(event.target.value)}
              fullWidth
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <SearchIcon fontSize="small" sx={{ fontSize: 18 }} />
                    </InputAdornment>
                  ),
                  sx: {
                    ...searchInputSx,
                    height: 36,
                    fontSize: 14,
                  },
                },
              }}
            />
          </Box>
        )}

        {columnsDrawerTab === 1 && (
          <Box sx={{ px: 2, pt: 1.25, pb: 0.75, bgcolor: 'background.paper' }}>
            <Typography variant="subtitle2">Visible column order</Typography>
          </Box>
        )}

        {columnsDrawerTab === 0 ? (
          <List
            dense
            subheader={null}
            sx={{
              flex: 1,
              minHeight: 0,
              overflow: 'auto',
              py: 0,
              bgcolor: 'background.paper',
            }}
          >
            {columnsByGroup.length === 0 && (
              <ListItem>
                <ListItemText secondary="No columns match your search." />
              </ListItem>
            )}
            {columnsByGroup.map(({ group, columns }) => (
              <Box key={group}>
                <ListSubheader
                  sx={{
                    bgcolor: 'background.paper',
                    lineHeight: '30px',
                    fontWeight: 700,
                    fontSize: 12,
                  }}
                >
                  {group}
                </ListSubheader>
                {columns.map((column) => {
                  const checked = column.required || drawerVisibleSet.has(column.id);
                  const filtered = hiddenFilteredColumnIds.has(column.id);
                  return (
                    <ListItemButton
                      key={column.id}
                      dense
                      component="div"
                      onClick={() => toggleDrawerColumn(column, !checked)}
                      sx={{
                        minHeight: 40,
                        py: 0.25,
                        px: 2,
                        cursor: column.required ? 'default' : 'pointer',
                        '&.Mui-disabled': { opacity: 1 },
                      }}
                    >
                      <ListItemIcon sx={{ minWidth: 36 }}>
                        <Checkbox
                          edge="start"
                          size="small"
                          checked={checked}
                          disabled={column.required}
                          tabIndex={-1}
                        />
                      </ListItemIcon>
                      <ListItemText
                        primary={column.label}
                        primaryTypographyProps={{
                          variant: 'body2',
                          color: 'text.primary',
                          noWrap: true,
                        }}
                        sx={{ my: 0 }}
                      />
                      <Stack direction="row" spacing={0.5} sx={{ ml: 1 }}>
                        {column.required && (
                          <Chip label="Required" size="small" variant="outlined" />
                        )}
                        {filtered && (
                          <Chip label="Filtered" size="small" color="info" variant="outlined" />
                        )}
                      </Stack>
                    </ListItemButton>
                  );
                })}
              </Box>
            ))}
          </List>
        ) : (
          <List
            dense
            sx={{
              flex: 1,
              minHeight: 0,
              overflow: 'auto',
              py: 0.5,
              bgcolor: 'background.paper',
            }}
          >
            {orderedDrawerColumns.map((column) => {
              const locked = Boolean(column.required);
              const active = draggedColumnId === column.id;
              const columnIndex = drawerVisibleColumnIds.indexOf(column.id);
              const canMoveUp = !locked && columnIndex > 1;
              const canMoveDown = !locked && columnIndex < drawerVisibleColumnIds.length - 1;

              return (
                <ListItem
                  key={column.id}
                  disablePadding
                  onDragOver={(event) => handleOrderDragOver(event, column.id)}
                  onDrop={(event) => handleOrderDrop(event, column.id)}
                  sx={{ px: 1.5, py: 0.25, bgcolor: 'background.paper' }}
                >
                  <Box
                    draggable={!locked}
                    tabIndex={locked ? undefined : 0}
                    onDragStart={(event) => handleOrderDragStart(event, column.id)}
                    onDragEnd={() => setDraggedColumnId(null)}
                    onKeyDown={(event) => handleOrderKeyDown(event, column)}
                    sx={{
                      width: '100%',
                      minHeight: 46,
                      display: 'flex',
                      alignItems: 'center',
                      gap: 1,
                      px: 1.25,
                      borderRadius: 1,
                      border: 1,
                      borderColor: active ? 'primary.light' : 'divider',
                      bgcolor: active ? 'action.selected' : 'background.paper',
                      boxShadow: active ? 3 : 'none',
                      position: 'relative',
                      zIndex: active ? 2 : 1,
                      opacity: active ? 0.96 : 1,
                      cursor: locked ? 'default' : active ? 'grabbing' : 'grab',
                      transition:
                        'background-color 120ms ease, border-color 120ms ease, box-shadow 120ms ease, opacity 120ms ease',
                      '&:focus-visible': {
                        outline: 2,
                        outlineColor: 'primary.main',
                        outlineOffset: 1,
                      },
                      '&:hover': {
                        bgcolor: active ? 'action.selected' : 'action.hover',
                        borderColor: active ? 'primary.light' : 'text.disabled',
                      },
                      '&:hover .order-row-actions, &:focus-within .order-row-actions': {
                        opacity: 1,
                      },
                    }}
                  >
                    {locked ? (
                      <Box
                        component="span"
                        sx={{
                          width: 32,
                          height: 32,
                          display: 'inline-flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          color: 'text.secondary',
                        }}
                      >
                        <LockOutlinedIcon fontSize="small" />
                      </Box>
                    ) : (
                      <Box
                        component="span"
                        aria-hidden="true"
                        sx={{
                          width: 32,
                          height: 32,
                          display: 'inline-flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          color: 'text.secondary',
                          flexShrink: 0,
                        }}
                      >
                        <DragIndicatorIcon fontSize="small" />
                      </Box>
                    )}

                    <ListItemText
                      primary={column.label}
                      primaryTypographyProps={{ variant: 'body2', noWrap: true }}
                      sx={{ my: 0, minWidth: 0 }}
                    />

                    {!locked && (
                      <Stack
                        className="order-row-actions"
                        direction="row"
                        spacing={0.25}
                        sx={{
                          ml: 0.5,
                          opacity: 0.72,
                          transition: 'opacity 120ms ease',
                        }}
                      >
                        <IconButton
                          size="small"
                          aria-label={`Move ${column.label} up`}
                          draggable={false}
                          onClick={(event) => {
                            event.stopPropagation();
                            moveDrawerColumnByDelta(column.id, -1);
                          }}
                          disabled={!canMoveUp}
                          sx={{ width: 28, height: 28 }}
                        >
                          <KeyboardArrowUpIcon fontSize="small" />
                        </IconButton>
                        <IconButton
                          size="small"
                          aria-label={`Move ${column.label} down`}
                          draggable={false}
                          onClick={(event) => {
                            event.stopPropagation();
                            moveDrawerColumnByDelta(column.id, 1);
                          }}
                          disabled={!canMoveDown}
                          sx={{ width: 28, height: 28 }}
                        >
                          <KeyboardArrowDownIcon fontSize="small" />
                        </IconButton>
                      </Stack>
                    )}
                  </Box>
                </ListItem>
              );
            })}
          </List>
        )}

        <Divider />
        <Stack
          direction="row"
          spacing={1}
          sx={{ p: 2, alignItems: 'center', bgcolor: 'background.paper' }}
        >
          <Button
            size="small"
            variant="text"
            onClick={resetDrawerDraft}
            disabled={actionLoading || !drawerCanReset}
            sx={{ color: 'text.secondary' }}
          >
            Reset
          </Button>
          <Box sx={{ flexGrow: 1 }} />
          <Button
            size="small"
            variant="outlined"
            onClick={closeColumnsDrawer}
            disabled={actionLoading}
            sx={{ borderColor: 'divider', color: 'text.primary' }}
          >
            Cancel
          </Button>
          <BrandButton
            size="small"
            kind="primary"
            onClick={handleApplyDrawer}
            disabled={actionLoading || !drawerHasChanges}
          >
            Apply
          </BrandButton>
        </Stack>
      </Drawer>

      <Dialog
        open={Boolean(nameDialog)}
        onClose={() => !actionLoading && setNameDialog(null)}
        maxWidth="xs"
        fullWidth
      >
        <DialogTitle>{nameDialogTitle}</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            fullWidth
            margin="dense"
            label="View name"
            value={nameDialog?.name ?? ''}
            error={Boolean(nameDialogError)}
            helperText={nameDialogError}
            onChange={(event) => {
              setNameDialog((current) =>
                current ? { ...current, name: event.target.value } : current
              );
              setNameDialogError(null);
            }}
            slotProps={{ htmlInput: { maxLength: 80 } }}
          />
        </DialogContent>
        <DialogActions>
          <BrandButton size="small" onClick={() => setNameDialog(null)} disabled={actionLoading}>
            Cancel
          </BrandButton>
          <BrandButton
            size="small"
            kind="primary"
            onClick={handleSubmitNameDialog}
            disabled={actionLoading}
          >
            {nameDialogAction}
          </BrandButton>
        </DialogActions>
      </Dialog>

      <Dialog
        open={deleteConfirmOpen}
        onClose={() => !actionLoading && setDeleteConfirmOpen(false)}
        maxWidth="xs"
        fullWidth
      >
        <DialogTitle>Delete “{tableViews.activeView.name}”?</DialogTitle>
        <DialogContent>
          <Typography variant="body2" color="text.secondary">
            This custom view will be permanently deleted.
          </Typography>
        </DialogContent>
        <DialogActions>
          <BrandButton
            size="small"
            onClick={() => setDeleteConfirmOpen(false)}
            disabled={actionLoading}
          >
            Cancel
          </BrandButton>
          <BrandButton
            size="small"
            kind="primary"
            onClick={handleDeleteView}
            disabled={actionLoading}
          >
            Delete
          </BrandButton>
        </DialogActions>
      </Dialog>

    </>
  );
}

function ToolbarPill({ label, tooltip, onClick }: ToolbarPillProps) {
  const button = (
    <Button
      size="small"
      variant="outlined"
      onClick={onClick}
      aria-haspopup="dialog"
      sx={toolbarPillSx}
    >
      {label}
    </Button>
  );

  return tooltip ? <Tooltip title={tooltip}>{button}</Tooltip> : button;
}

function ViewChangesPopover({
  anchorEl,
  activeViewName,
  actionLoading,
  isCustomView,
  changes,
  hasOnlyDisplaySettingChanges,
  onClose,
  onReset,
  onSave,
}: ViewChangesPopoverProps) {
  return (
    <Popover
      open={Boolean(anchorEl)}
      anchorEl={anchorEl}
      onClose={onClose}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
      transformOrigin={{ vertical: 'top', horizontal: 'left' }}
      PaperProps={{ sx: popoverPaperSx }}
    >
      <Typography variant="subtitle2" sx={{ mb: 1 }}>
        Unsaved view changes
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
        Changes to “{activeViewName}” are not saved yet.
      </Typography>

      <ColumnChangeList title="Added columns" labels={changes.added} />
      <ColumnChangeList title="Hidden columns" labels={changes.hidden} />

      {changes.reordered && (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          Column order was changed.
        </Typography>
      )}

      {hasOnlyDisplaySettingChanges && (
        <Typography variant="body2" color="text.secondary">
          Display settings were changed.
        </Typography>
      )}

      <Stack direction="row" spacing={1} sx={{ mt: 2, justifyContent: 'flex-end' }}>
        <Button size="small" variant="text" onClick={onReset}>
          Reset changes
        </Button>
        <BrandButton size="small" kind="primary" onClick={onSave} disabled={actionLoading}>
          {isCustomView ? 'Save changes' : 'Save as custom view'}
        </BrandButton>
      </Stack>
    </Popover>
  );
}

function HiddenFiltersPopover({
  anchorEl,
  hiddenFilteredColumns,
  hiddenFiltersCount,
  hiddenColumnsLabel,
  onClose,
  onClear,
  onShow,
}: HiddenFiltersPopoverProps) {
  return (
    <Popover
      open={Boolean(anchorEl)}
      anchorEl={anchorEl}
      onClose={onClose}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
      transformOrigin={{ vertical: 'top', horizontal: 'left' }}
      PaperProps={{ sx: { ...popoverPaperSx, width: 320 } }}
    >
      <Typography variant="subtitle2" sx={{ mb: 1 }}>
        {pluralize('Filter', hiddenFiltersCount)} on hidden {hiddenColumnsLabel}
      </Typography>
      {hiddenFilteredColumns.length > 0 ? (
        <Stack component="ul" spacing={0.5} sx={{ m: 0, pl: 2.25 }}>
          {hiddenFilteredColumns.map((column) => (
            <Typography key={column.id} component="li" variant="body2">
              {column.label}
            </Typography>
          ))}
        </Stack>
      ) : (
        <Typography variant="body2" color="text.secondary">
          Some active filters use columns that are not visible in the current view.
        </Typography>
      )}
      <Stack direction="row" spacing={1} sx={{ mt: 2, justifyContent: 'flex-end' }}>
        <Button size="small" variant="text" onClick={onClear}>
          Clear {pluralize('filter', hiddenFiltersCount)}
        </Button>
        <BrandButton size="small" kind="primary" onClick={onShow}>
          Show {hiddenColumnsLabel}
        </BrandButton>
      </Stack>
    </Popover>
  );
}

function ColumnChangeList({ title, labels }: { title: string; labels: string[] }) {
  if (labels.length === 0) return null;

  return (
    <Box sx={{ mb: 1.5 }}>
      <Typography variant="caption" color="text.secondary">
        {title}
      </Typography>
      <Stack component="ul" spacing={0.5} sx={{ m: 0, mt: 0.5, pl: 2.25 }}>
        {labels.map((label) => (
          <Typography key={label} component="li" variant="body2">
            {label}
          </Typography>
        ))}
      </Stack>
    </Box>
  );
}

function areColumnListsEqual(left: string[], right: string[]): boolean {
  if (left.length !== right.length) return false;
  return left.every((columnId, index) => columnId === right[index]);
}

function haveSameColumnSet(left: string[], right: string[]): boolean {
  if (left.length !== right.length) return false;
  const rightColumnIds = new Set(right);
  return left.every((columnId) => rightColumnIds.has(columnId));
}

function pluralize(word: string, count: number): string {
  return count === 1 ? word : `${word}s`;
}
