import {
  Divider,
  ListItemIcon,
  ListItemText,
  ListSubheader,
  Menu,
  MenuItem,
} from '@mui/material';
import CheckIcon from '@mui/icons-material/Check';
import type {
  TableViewDensity,
  TableViewSettings,
  TableViewType,
} from '../../../types/tableViews.types';
import { DENSITY_OPTIONS } from './SitesTableViewToolbar.helpers';
import type { NameDialogMode, SitesTableViewsState } from './SitesTableViewToolbar.types';

interface SitesViewSelectorMenuProps {
  anchorEl: HTMLElement | null;
  tableViews: SitesTableViewsState;
  onClose: () => void;
  onSwitchView: (viewType: TableViewType, viewKey: string) => void;
}

interface SitesViewOverflowMenuProps {
  anchorEl: HTMLElement | null;
  activeCustomViewName: string;
  isCustomView: boolean;
  tableViews: SitesTableViewsState;
  onClose: () => void;
  onOpenNameDialog: (
    mode: NameDialogMode,
    name?: string,
    settings?: TableViewSettings | null,
    closeDrawerOnSuccess?: boolean
  ) => void;
  onBuildSettings: () => TableViewSettings;
  onDensityChange: (density: TableViewDensity) => void;
  onDeleteView: () => void;
}

export function SitesViewSelectorMenu({
  anchorEl,
  tableViews,
  onClose,
  onSwitchView,
}: SitesViewSelectorMenuProps) {
  return (
    <Menu
      anchorEl={anchorEl}
      open={Boolean(anchorEl)}
      onClose={onClose}
      MenuListProps={{ dense: true }}
    >
      <ListSubheader>System views</ListSubheader>
      {tableViews.systemViews.map((view) => {
        const active = tableViews.activeView.type === 'system' && tableViews.activeView.key === view.key;
        return (
          <MenuItem key={view.key} selected={active} onClick={() => onSwitchView('system', view.key)}>
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
        const active = tableViews.activeView.type === 'custom' && tableViews.activeView.key === view.id;
        return (
          <MenuItem key={view.id} selected={active} onClick={() => onSwitchView('custom', view.id)}>
            <ListItemIcon>{active ? <CheckIcon fontSize="small" /> : null}</ListItemIcon>
            <ListItemText>{view.name}</ListItemText>
          </MenuItem>
        );
      })}
    </Menu>
  );
}

export function SitesViewOverflowMenu({
  anchorEl,
  activeCustomViewName,
  isCustomView,
  tableViews,
  onClose,
  onOpenNameDialog,
  onBuildSettings,
  onDensityChange,
  onDeleteView,
}: SitesViewOverflowMenuProps) {
  return (
    <Menu
      anchorEl={anchorEl}
      open={Boolean(anchorEl)}
      onClose={onClose}
      MenuListProps={{ dense: true }}
    >
      {isCustomView && (
        <MenuItem onClick={() => onOpenNameDialog('rename', activeCustomViewName)}>
          Rename view
        </MenuItem>
      )}
      <MenuItem
        onClick={() =>
          onOpenNameDialog('duplicate', `${tableViews.activeView.name} copy`, onBuildSettings())
        }
      >
        Duplicate view
      </MenuItem>
      <Divider />
      <ListSubheader>Density</ListSubheader>
      {DENSITY_OPTIONS.map((option) => (
        <MenuItem key={option.value} onClick={() => onDensityChange(option.value)}>
          <ListItemIcon>
            {tableViews.density === option.value ? <CheckIcon fontSize="small" /> : null}
          </ListItemIcon>
          <ListItemText>{option.label}</ListItemText>
        </MenuItem>
      ))}
      {isCustomView && (
        <>
          <Divider />
          <MenuItem onClick={onDeleteView} sx={{ color: 'error.main' }}>
            Delete view
          </MenuItem>
        </>
      )}
    </Menu>
  );
}
