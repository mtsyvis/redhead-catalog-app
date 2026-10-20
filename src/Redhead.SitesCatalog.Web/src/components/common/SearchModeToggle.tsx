import { ToggleButton, ToggleButtonGroup } from '@mui/material';

export function SearchModeToggle({
  multiSearchMode,
  onChange,
}: {
  readonly multiSearchMode: boolean;
  readonly onChange: (enabled: boolean) => void;
}) {
  return (
    <ToggleButtonGroup
      exclusive
      size="small"
      value={multiSearchMode ? 'multi' : 'single'}
      onChange={(_, value: 'single' | 'multi' | null) => {
        if (value !== null) onChange(value === 'multi');
      }}
      aria-label="Search mode"
      sx={{
        flex: multiSearchMode ? '0 0 auto' : { xs: '1 1 100%', md: '0 0 auto' },
        alignSelf: multiSearchMode ? 'flex-start' : undefined,
        maxWidth: { xs: '100%', md: 'none' },
        borderRadius: 999,
        '& .MuiToggleButton-root': {
          px: 1.75,
          py: 0.75,
          minHeight: 38,
          textTransform: 'none',
          borderColor: 'divider',
          color: 'text.primary',
          bgcolor: 'background.paper',
          lineHeight: 1.2,
          '&:first-of-type': { borderTopLeftRadius: 999, borderBottomLeftRadius: 999 },
          '&:last-of-type': { borderTopRightRadius: 999, borderBottomRightRadius: 999 },
          '&.Mui-selected': {
            color: 'primary.main',
            bgcolor: 'rgba(255, 69, 91, 0.08)',
            borderColor: 'rgba(255, 69, 91, 0.35)',
            fontWeight: 600,
            '&:hover': { bgcolor: 'rgba(255, 69, 91, 0.12)' },
          },
          '&:not(.Mui-selected)': { '&:hover': { bgcolor: 'action.hover' } },
        },
      }}
    >
      <ToggleButton value="single">Single search</ToggleButton>
      <ToggleButton value="multi">Multi-search</ToggleButton>
    </ToggleButtonGroup>
  );
}
