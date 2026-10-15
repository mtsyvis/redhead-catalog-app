import { Box } from '@mui/material';
import { alpha } from '@mui/material/styles';

export function StatusBadge({
  label,
  tone,
  minWidth = 72,
}: {
  readonly label: string;
  readonly tone: 'success' | 'error';
  readonly minWidth?: number;
}) {
  return (
    <Box
      component="span"
      sx={(theme) => ({
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
        boxSizing: 'border-box',
        minWidth,
        height: theme.custom.table.statusHeight,
        px: 1.25,
        borderRadius: '999px',
        fontSize: theme.custom.table.secondaryFontSize,
        fontWeight: 600,
        lineHeight: 1.2,
        whiteSpace: 'nowrap',
        color: theme.palette[tone].dark,
        backgroundColor: alpha(theme.palette[tone].main, tone === 'success' ? 0.14 : 0.18),
        border: `1px solid ${alpha(theme.palette[tone].main, tone === 'success' ? 0.28 : 0.32)}`,
      })}
    >
      {label}
    </Box>
  );
}
