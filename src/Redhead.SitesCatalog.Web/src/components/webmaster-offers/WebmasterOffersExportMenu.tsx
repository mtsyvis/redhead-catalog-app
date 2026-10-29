import { useState } from 'react';
import {
  Box, Button, Divider, ListItemIcon, Menu, MenuItem, Typography,
} from '@mui/material';
import AddToDriveIcon from '@mui/icons-material/AddToDrive';
import ArrowDropDownIcon from '@mui/icons-material/ArrowDropDown';
import DownloadIcon from '@mui/icons-material/Download';

interface WebmasterOffersExportMenuProps {
  exporting: boolean;
  disabled: boolean;
  resultCount: number | null;
  onExport: (googleDrive: boolean) => void;
}

export function WebmasterOffersExportMenu({
  exporting, disabled, resultCount, onExport,
}: Readonly<WebmasterOffersExportMenuProps>) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);

  const handleExport = (googleDrive: boolean) => {
    setAnchor(null);
    onExport(googleDrive);
  };

  return (
    <Box sx={{ flexShrink: 0 }}>
      <Button
        size="small"
        variant="outlined"
        endIcon={<ArrowDropDownIcon />}
        onClick={(event) => setAnchor(event.currentTarget)}
        disabled={exporting || disabled}
        aria-controls={anchor ? 'webmaster-offers-export-menu' : undefined}
        aria-haspopup="menu"
        aria-expanded={anchor ? 'true' : undefined}
        sx={{ borderColor: 'divider', color: 'text.primary', bgcolor: 'background.paper', flexShrink: 0 }}
      >
        {exporting ? 'Exporting...' : 'Export'}
      </Button>
      <Menu
        id="webmaster-offers-export-menu"
        anchorEl={anchor}
        open={Boolean(anchor)}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { width: 360, maxWidth: 'calc(100vw - 32px)', mt: 0.5 } } }}
      >
        <Box sx={{ px: 2, py: 1.5 }}>
          <Typography variant="subtitle2">
            {resultCount === null
              ? 'All webmaster offers'
              : `Search results · ${resultCount.toLocaleString()} ${resultCount === 1 ? 'offer' : 'offers'}`}
          </Typography>
        </Box>
        <Divider />
        <MenuItem onClick={() => handleExport(false)} disabled={exporting || disabled} sx={{ py: 1, typography: 'body2' }}>
          <ListItemIcon><DownloadIcon fontSize="small" /></ListItemIcon>
          Download Excel
        </MenuItem>
        <MenuItem onClick={() => handleExport(true)} disabled={exporting || disabled} sx={{ py: 1, typography: 'body2' }}>
          <ListItemIcon><AddToDriveIcon fontSize="small" /></ListItemIcon>
          Save to Google Drive
        </MenuItem>
      </Menu>
    </Box>
  );
}
