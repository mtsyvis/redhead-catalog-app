import type { ReactNode } from 'react';
import { Box, Stack } from '@mui/material';

export function SearchBarLayout({
  multiSearchMode,
  modeControl,
  input,
  searchAction,
}: {
  readonly multiSearchMode: boolean;
  readonly modeControl?: ReactNode;
  readonly input: ReactNode;
  readonly searchAction?: ReactNode;
}) {
  if (multiSearchMode) {
    return (
      <Stack spacing={0.75}>
        {modeControl}
        {input}
        {searchAction && (
          <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>{searchAction}</Box>
        )}
      </Stack>
    );
  }

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, flexWrap: 'wrap' }}>
      {modeControl}
      {input}
    </Box>
  );
}
