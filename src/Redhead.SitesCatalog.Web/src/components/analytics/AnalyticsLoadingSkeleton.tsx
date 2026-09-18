import { Box, Paper, Skeleton } from '@mui/material';

export function AnalyticsLoadingSkeleton({ tableOnly = false }: { tableOnly?: boolean }) {
  return (
    <Box aria-label="Loading analytics" sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      {!tableOnly && (
        <Paper
          variant="outlined"
          sx={{ display: 'flex', flexWrap: 'wrap', px: 2, py: 1.25, gap: 2 }}
        >
          {[0, 1, 2, 3].map((item) => (
            <Box key={item} sx={{ flex: '1 1 150px' }}>
              <Skeleton width="65%" height={18} />
              <Skeleton width={80} height={32} />
            </Box>
          ))}
        </Paper>
      )}
      <Paper variant="outlined" sx={{ p: 2 }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 2, mb: 1 }}>
          <Skeleton width={220} height={28} />
          {tableOnly && <Skeleton width="35%" height={28} />}
        </Box>
        <Skeleton variant="rounded" height={44} sx={{ mb: 1 }} />
        {[0, 1, 2, 3, 4].map((item) => (
          <Skeleton key={item} height={32} />
        ))}
      </Paper>
    </Box>
  );
}
