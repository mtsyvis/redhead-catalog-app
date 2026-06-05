import { useState } from 'react';
import type { MouseEvent } from 'react';
import { Box, Button, Divider, ListItemIcon, Menu, MenuItem, Typography } from '@mui/material';
import AddToDriveIcon from '@mui/icons-material/AddToDrive';
import ArrowDropDownIcon from '@mui/icons-material/ArrowDropDown';
import DownloadIcon from '@mui/icons-material/Download';
import type { CurrentUserProfileLimits } from '../../../types/auth.types';
import { formatUsageLimitPair, hasClientExportUsage } from '../../../utils/exportUsageLimits';

interface SitesExportMenuProps {
  exporting: boolean;
  loading: boolean;
  exportUsageLimits: CurrentUserProfileLimits | null;
  onDownloadExcel: () => void;
  onSaveToGoogleDrive: () => void;
}

export function SitesExportMenu({
  exporting,
  loading,
  exportUsageLimits,
  onDownloadExcel,
  onSaveToGoogleDrive,
}: Readonly<SitesExportMenuProps>) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const usageRows = exportUsageLimits && hasClientExportUsage(exportUsageLimits)
    ? [
        {
          label: 'Today',
          value: formatUsageLimitPair(
            exportUsageLimits.dailyUniqueExportedDomainsUsed,
            exportUsageLimits.dailyUniqueExportedDomainsLimit
          ),
          suffix: 'domains',
        },
        {
          label: 'This week',
          value: formatUsageLimitPair(
            exportUsageLimits.weeklyUniqueExportedDomainsUsed,
            exportUsageLimits.weeklyUniqueExportedDomainsLimit
          ),
          suffix: 'domains',
        },
        {
          label: 'Exports today',
          value: formatUsageLimitPair(
            exportUsageLimits.dailyExportOperationsUsed,
            exportUsageLimits.dailyExportOperationsLimit
          ),
          suffix: '',
        },
        {
          label: 'Exports this week',
          value: formatUsageLimitPair(
            exportUsageLimits.weeklyExportOperationsUsed,
            exportUsageLimits.weeklyExportOperationsLimit
          ),
          suffix: '',
        },
      ].filter((row): row is { label: string; value: string; suffix: string } => row.value != null)
    : [];

  const handleOpen = (event: MouseEvent<HTMLElement>) => {
    setAnchor(event.currentTarget);
  };

  const handleClose = () => {
    setAnchor(null);
  };

  const handleDownloadExcel = () => {
    handleClose();
    onDownloadExcel();
  };

  const handleSaveToGoogleDrive = () => {
    handleClose();
    onSaveToGoogleDrive();
  };

  return (
    <Box sx={{ flexShrink: 0 }}>
      <Button
        size="small"
        variant="outlined"
        endIcon={<ArrowDropDownIcon />}
        onClick={handleOpen}
        disabled={exporting || loading}
        aria-controls={anchor ? 'sites-export-menu' : undefined}
        aria-haspopup="menu"
        aria-expanded={anchor ? 'true' : undefined}
        sx={{
          borderColor: 'divider',
          color: 'text.primary',
          bgcolor: 'background.paper',
          flexShrink: 0,
        }}
      >
        {exporting ? 'Exporting...' : 'Export'}
      </Button>
      <Menu
        id="sites-export-menu"
        anchorEl={anchor}
        open={Boolean(anchor)}
        onClose={handleClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{
          paper: {
            sx: {
              width: 320,
              maxWidth: 'calc(100vw - 32px)',
              mt: 0.5,
            },
          },
        }}
      >
        <Box sx={{ px: 2, py: 1.5 }}>
          <Typography variant="subtitle2" sx={{ color: 'text.primary' }}>
            Export results
          </Typography>
          <Typography variant="body2" sx={{ mt: 0.5, color: 'text.secondary' }}>
            Exports all matching rows with only the columns visible in this table. Search, filters,
            sorting, and column order are used.
          </Typography>
          {usageRows.length > 0 && (
            <Box
              sx={{
                mt: 1.25,
                display: 'grid',
                gridTemplateColumns: '1fr',
                gap: 0.5,
                p: 1,
                borderRadius: 1,
                bgcolor: 'action.hover',
              }}
            >
              {usageRows.map((row) => (
                <Typography key={row.label} variant="caption" color="text.secondary">
                  {row.label}: {row.value}{row.suffix ? ` ${row.suffix}` : ''}
                </Typography>
              ))}
            </Box>
          )}
        </Box>
        <Divider />
        <MenuItem onClick={handleDownloadExcel} disabled={exporting}>
          <ListItemIcon>
            <DownloadIcon fontSize="small" />
          </ListItemIcon>
          Download Excel
        </MenuItem>
        <MenuItem onClick={handleSaveToGoogleDrive} disabled={exporting}>
          <ListItemIcon>
            <AddToDriveIcon fontSize="small" />
          </ListItemIcon>
          Save to Google Drive
        </MenuItem>
      </Menu>
    </Box>
  );
}
