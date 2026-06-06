import { Box, Paper, Skeleton } from '@mui/material';

const KPI_SKELETON_ITEMS = [0, 1, 2, 3];
const SECTION_SKELETON_ITEMS = [0, 1, 2, 3];

export function AnalyticsLoadingSkeleton() {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', xl: 'repeat(4, 1fr)' },
          gap: 2,
        }}
      >
        {KPI_SKELETON_ITEMS.map((item) => (
          <KpiSkeletonCard key={item} />
        ))}
      </Box>

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', lg: 'repeat(2, minmax(0, 1fr))' },
          gap: 3,
        }}
      >
        {SECTION_SKELETON_ITEMS.map((item) => (
          <AnalyticsSectionSkeleton key={item} rows={5} />
        ))}
      </Box>

      <AnalyticsSectionSkeleton rows={4} />
      <AnalyticsSectionSkeleton rows={5} />
      <AnalyticsSectionSkeleton rows={3} />
    </Box>
  );
}

function KpiSkeletonCard() {
  return (
    <Paper variant="outlined" sx={{ p: 2, height: '100%' }}>
      <Skeleton animation="wave" variant="text" width="52%" height={20} sx={{ mb: 0.75 }} />
      <Skeleton animation="wave" variant="text" width="38%" height={44} sx={{ mb: 1 }} />
      <Skeleton animation="wave" variant="text" width="92%" height={18} />
      <Skeleton animation="wave" variant="text" width="70%" height={18} />
    </Paper>
  );
}

function AnalyticsSectionSkeleton({ rows }: { rows: number }) {
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Skeleton animation="wave" variant="text" width={180} height={28} />
      <Skeleton animation="wave" variant="text" width="82%" height={18} sx={{ mt: 0.5 }} />
      <Skeleton animation="wave" variant="text" width="56%" height={18} sx={{ mb: 2 }} />
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.25 }}>
        {Array.from({ length: rows }, (_value, index) => (
          <Box
            key={index}
            sx={{
              display: 'grid',
              gridTemplateColumns: 'minmax(0, 1fr) 88px',
              gap: 2,
              alignItems: 'center',
            }}
          >
            <Box>
              <Skeleton animation="wave" variant="text" width={`${72 - index * 7}%`} height={20} />
              <Skeleton animation="wave" variant="rounded" width="100%" height={6} />
            </Box>
            <Skeleton animation="wave" variant="text" width="100%" height={20} />
          </Box>
        ))}
      </Box>
    </Paper>
  );
}
