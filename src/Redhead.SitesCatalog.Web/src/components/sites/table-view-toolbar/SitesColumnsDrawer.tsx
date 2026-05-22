import type { DragEvent, KeyboardEvent } from 'react';
import {
  Box,
  Button,
  Checkbox,
  Chip,
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
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material';
import DragIndicatorIcon from '@mui/icons-material/DragIndicator';
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown';
import KeyboardArrowUpIcon from '@mui/icons-material/KeyboardArrowUp';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import SearchIcon from '@mui/icons-material/Search';
import { BrandButton } from '../../common/BrandButton';
import { searchInputSx } from './SitesTableViewToolbar.helpers';
import type { SitesColumnGroup, SitesColumnMetadata } from '../table-views/sitesTableColumns';

interface SitesColumnsDrawerProps {
  actionLoading: boolean;
  columnsByGroup: Array<{ group: SitesColumnGroup; columns: SitesColumnMetadata[] }>;
  drawerCanReset: boolean;
  drawerHasChanges: boolean;
  draggedColumnId: string | null;
  hiddenFilteredColumnIds: Set<string>;
  open: boolean;
  orderedColumns: SitesColumnMetadata[];
  search: string;
  tab: number;
  totalConfigurableCount: number;
  visibleColumnIds: string[];
  visibleColumnSet: Set<string>;
  onApply: () => void;
  onChangeSearch: (value: string) => void;
  onChangeTab: (value: number) => void;
  onClose: () => void;
  onDragEnd: () => void;
  onMoveColumnByDelta: (columnId: string, delta: number) => void;
  onOrderDragOver: (event: DragEvent<HTMLElement>, targetColumnId: string) => void;
  onOrderDragStart: (event: DragEvent<HTMLElement>, columnId: string) => void;
  onOrderDrop: (event: DragEvent<HTMLElement>, targetColumnId: string) => void;
  onOrderKeyDown: (event: KeyboardEvent<HTMLElement>, column: SitesColumnMetadata) => void;
  onReset: () => void;
  onToggleColumn: (column: SitesColumnMetadata, checked: boolean) => void;
}

export function SitesColumnsDrawer({
  actionLoading,
  columnsByGroup,
  drawerCanReset,
  drawerHasChanges,
  draggedColumnId,
  hiddenFilteredColumnIds,
  open,
  orderedColumns,
  search,
  tab,
  totalConfigurableCount,
  visibleColumnIds,
  visibleColumnSet,
  onApply,
  onChangeSearch,
  onChangeTab,
  onClose,
  onDragEnd,
  onMoveColumnByDelta,
  onOrderDragOver,
  onOrderDragStart,
  onOrderDrop,
  onOrderKeyDown,
  onReset,
  onToggleColumn,
}: SitesColumnsDrawerProps) {
  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
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
          {visibleColumnIds.length} of {totalConfigurableCount} visible
        </Typography>
      </Box>

      <Tabs
        value={tab}
        onChange={(_event, value: number) => onChangeTab(value)}
        variant="fullWidth"
        sx={{
          minHeight: 34,
          borderBottom: 1,
          borderColor: 'divider',
          bgcolor: 'background.paper',
        }}
        slotProps={{ indicator: { sx: { height: 2 } } }}
      >
        <Tab label="Columns" sx={{ minHeight: 34, py: 0.5, textTransform: 'none', fontSize: 13 }} />
        <Tab label="Order" sx={{ minHeight: 34, py: 0.5, textTransform: 'none', fontSize: 13 }} />
      </Tabs>

      {tab === 0 && (
        <Box sx={{ px: 2, pt: 0.75, pb: 0.5, bgcolor: 'background.paper' }}>
          <TextField
            size="small"
            placeholder="Search columns..."
            value={search}
            onChange={(event) => onChangeSearch(event.target.value)}
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

      {tab === 1 && (
        <Box sx={{ px: 2, pt: 1.25, pb: 0.75, bgcolor: 'background.paper' }}>
          <Typography variant="subtitle2">Visible column order</Typography>
        </Box>
      )}

      {tab === 0 ? (
        <ColumnToggleList
          columnsByGroup={columnsByGroup}
          hiddenFilteredColumnIds={hiddenFilteredColumnIds}
          visibleColumnSet={visibleColumnSet}
          onToggleColumn={onToggleColumn}
        />
      ) : (
        <ColumnOrderList
          draggedColumnId={draggedColumnId}
          orderedColumns={orderedColumns}
          visibleColumnIds={visibleColumnIds}
          onDragEnd={onDragEnd}
          onMoveColumnByDelta={onMoveColumnByDelta}
          onOrderDragOver={onOrderDragOver}
          onOrderDragStart={onOrderDragStart}
          onOrderDrop={onOrderDrop}
          onOrderKeyDown={onOrderKeyDown}
        />
      )}

      <Divider />
      <Stack direction="row" spacing={1} sx={{ p: 2, alignItems: 'center', bgcolor: 'background.paper' }}>
        <Button
          size="small"
          variant="text"
          onClick={onReset}
          disabled={actionLoading || !drawerCanReset}
          sx={{ color: 'text.secondary' }}
        >
          Reset
        </Button>
        <Box sx={{ flexGrow: 1 }} />
        <Button
          size="small"
          variant="outlined"
          onClick={onClose}
          disabled={actionLoading}
          sx={{ borderColor: 'divider', color: 'text.primary' }}
        >
          Cancel
        </Button>
        <BrandButton size="small" kind="primary" onClick={onApply} disabled={actionLoading || !drawerHasChanges}>
          Apply
        </BrandButton>
      </Stack>
    </Drawer>
  );
}

function ColumnToggleList({
  columnsByGroup,
  hiddenFilteredColumnIds,
  visibleColumnSet,
  onToggleColumn,
}: {
  columnsByGroup: Array<{ group: SitesColumnGroup; columns: SitesColumnMetadata[] }>;
  hiddenFilteredColumnIds: Set<string>;
  visibleColumnSet: Set<string>;
  onToggleColumn: (column: SitesColumnMetadata, checked: boolean) => void;
}) {
  return (
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
            const checked = column.required || visibleColumnSet.has(column.id);
            const filtered = hiddenFilteredColumnIds.has(column.id);
            return (
              <ListItemButton
                key={column.id}
                dense
                component="div"
                onClick={() => onToggleColumn(column, !checked)}
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
                  {column.required && <Chip label="Required" size="small" variant="outlined" />}
                  {filtered && <Chip label="Filtered" size="small" color="info" variant="outlined" />}
                </Stack>
              </ListItemButton>
            );
          })}
        </Box>
      ))}
    </List>
  );
}

function ColumnOrderList({
  draggedColumnId,
  orderedColumns,
  visibleColumnIds,
  onDragEnd,
  onMoveColumnByDelta,
  onOrderDragOver,
  onOrderDragStart,
  onOrderDrop,
  onOrderKeyDown,
}: {
  draggedColumnId: string | null;
  orderedColumns: SitesColumnMetadata[];
  visibleColumnIds: string[];
  onDragEnd: () => void;
  onMoveColumnByDelta: (columnId: string, delta: number) => void;
  onOrderDragOver: (event: DragEvent<HTMLElement>, targetColumnId: string) => void;
  onOrderDragStart: (event: DragEvent<HTMLElement>, columnId: string) => void;
  onOrderDrop: (event: DragEvent<HTMLElement>, targetColumnId: string) => void;
  onOrderKeyDown: (event: KeyboardEvent<HTMLElement>, column: SitesColumnMetadata) => void;
}) {
  return (
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
      {orderedColumns.map((column) => {
        const locked = Boolean(column.required);
        const active = draggedColumnId === column.id;
        const columnIndex = visibleColumnIds.indexOf(column.id);
        const canMoveUp = !locked && columnIndex > 1;
        const canMoveDown = !locked && columnIndex < visibleColumnIds.length - 1;

        return (
          <ListItem
            key={column.id}
            disablePadding
            onDragOver={(event) => onOrderDragOver(event, column.id)}
            onDrop={(event) => onOrderDrop(event, column.id)}
            sx={{ px: 1.5, py: 0.25, bgcolor: 'background.paper' }}
          >
            <Box
              draggable={!locked}
              tabIndex={locked ? undefined : 0}
              onDragStart={(event) => onOrderDragStart(event, column.id)}
              onDragEnd={onDragEnd}
              onKeyDown={(event) => onOrderKeyDown(event, column)}
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
                      onMoveColumnByDelta(column.id, -1);
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
                      onMoveColumnByDelta(column.id, 1);
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
  );
}
