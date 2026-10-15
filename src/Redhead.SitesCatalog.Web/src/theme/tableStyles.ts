import { alpha } from '@mui/material/styles';
import type { CSSObject, Theme } from '@mui/material/styles';

export function getTableCellStyles(theme: Theme): CSSObject {
  const tokens = theme.custom.table;
  return {
    fontFamily: theme.typography.fontFamily,
    fontSize: tokens.fontSize,
    lineHeight: tokens.lineHeight,
    padding: `${tokens.cellPaddingY}px ${tokens.cellPaddingX}px`,
    borderColor: theme.palette.divider,
  };
}

export function getTableHeaderStyles(theme: Theme): CSSObject {
  return {
    ...getTableCellStyles(theme),
    fontWeight: theme.custom.table.headerFontWeight,
    color: theme.palette.text.primary,
    backgroundColor: theme.palette.grey[100],
  };
}

export function getTableSurfaceStyles(theme: Theme): CSSObject {
  return {
    border: `1px solid ${theme.palette.divider}`,
    borderRadius: `${theme.shape.borderRadius}px`,
    backgroundColor: theme.palette.background.paper,
    boxShadow: 'none',
    '&:focus-visible': {
      outline: `2px solid ${theme.palette.primary.main}`,
      outlineOffset: 2,
    },
  };
}

export function getPinnedTableCellStyles(theme: Theme, header = false): CSSObject {
  return {
    position: 'sticky',
    left: 0,
    zIndex: header ? 3 : 2,
    backgroundColor: header ? theme.palette.grey[100] : theme.palette.background.paper,
    borderRight: `1px solid ${theme.palette.divider}`,
    boxShadow: '6px 0 10px -12px rgba(0, 0, 0, 0.5)',
  };
}

export function getDataGridStyles(theme: Theme): CSSObject {
  return {
    ...getTableSurfaceStyles(theme),
    fontSize: theme.custom.table.fontSize,
    // Leave DataGrid's empty filler cell unpadded so it cannot extend horizontal scrolling.
    '& .MuiDataGrid-cell[data-field]': {
      ...getTableCellStyles(theme),
      display: 'flex',
      alignItems: 'center',
    },
    '& .MuiDataGrid-columnHeader': {
      ...getTableHeaderStyles(theme),
      paddingTop: 0,
      paddingBottom: 0,
    },
    '& .MuiDataGrid-columnHeaderTitle': { fontWeight: theme.custom.table.headerFontWeight },
    // Keep icon buttons inside narrow action columns such as the 48px Sites column.
    '& .MuiDataGrid-cell[data-field="actions"], & .MuiDataGrid-columnHeader[data-field="actions"]': {
      paddingLeft: 8,
      paddingRight: 8,
    },
    '& .MuiDataGrid-columnHeaders, & .MuiDataGrid-container--top': {
      backgroundColor: theme.palette.grey[100],
    },
    '& .MuiDataGrid-row:hover': { backgroundColor: theme.palette.grey[50] },
    '& .MuiDataGrid-cell:focus, & .MuiDataGrid-cell:focus-within, & .MuiDataGrid-columnHeader:focus, & .MuiDataGrid-columnHeader:focus-within':
      {
        outline: `2px solid ${alpha(theme.palette.primary.main, 0.55)}`,
        outlineOffset: -2,
      },
    '&.MuiDataGrid-root--densityCompact .MuiDataGrid-cell[data-field]': { paddingTop: 3, paddingBottom: 3 },
  };
}
