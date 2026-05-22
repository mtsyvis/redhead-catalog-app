import { useState } from 'react';
import type { MouseEvent } from 'react';
import { Box, Menu, MenuItem } from '@mui/material';
import AddToDriveIcon from '@mui/icons-material/AddToDrive';
import ArrowDropDownIcon from '@mui/icons-material/ArrowDropDown';
import DownloadIcon from '@mui/icons-material/Download';
import { BrandButton } from '../../common/BrandButton';

interface SitesExportMenuProps {
  exporting: boolean;
  loading: boolean;
  onDownloadExcel: () => void;
  onSaveToGoogleDrive: () => void;
}

export function SitesExportMenu({
  exporting,
  loading,
  onDownloadExcel,
  onSaveToGoogleDrive,
}: Readonly<SitesExportMenuProps>) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);

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
    <Box>
      <BrandButton
        kind="outline"
        endIcon={<ArrowDropDownIcon />}
        onClick={handleOpen}
        disabled={exporting || loading}
        aria-controls={anchor ? 'sites-export-menu' : undefined}
        aria-haspopup="menu"
        aria-expanded={anchor ? 'true' : undefined}
      >
        {exporting ? 'Exporting...' : 'Export'}
      </BrandButton>
      <Menu
        id="sites-export-menu"
        anchorEl={anchor}
        open={Boolean(anchor)}
        onClose={handleClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        <MenuItem onClick={handleDownloadExcel} disabled={exporting}>
          <DownloadIcon fontSize="small" sx={{ mr: 1 }} />
          Download Excel
        </MenuItem>
        <MenuItem onClick={handleSaveToGoogleDrive} disabled={exporting}>
          <AddToDriveIcon fontSize="small" sx={{ mr: 1 }} />
          Save to Google Drive
        </MenuItem>
      </Menu>
    </Box>
  );
}
