import {
  Alert,
  Box,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import type {
  BusinessDemandAnalytics,
  BusinessDemandCount,
  FilterStrictness,
  QualityDemand,
  ServiceDemand,
} from '../../types/analytics.types';
import { getLanguageOption } from '../../utils/language';
import { formatInteger } from '../../utils/numberFormat';
import { AnalyticsSection, EmptyState, KpiCard } from './AnalyticsShared';

const KPI_HELPERS = {
  exportRequests:
    'All export attempts in the selected period, including successful, partial, and blocked requests.',
  clientsWithExportActivity:
    'Unique Client users who made at least one export request in the selected period.',
  requestedRows: 'Total rows clients attempted to receive before export limits were applied.',
  exportedDomains: 'Domains actually exported after export limits were applied.',
};

function formatLanguageName(value: string): string {
  return getLanguageOption(value)?.label ?? value;
}

export function BusinessDemandTab({ analytics }: { analytics: BusinessDemandAnalytics }) {
  const noExportRequests = analytics.summary.exportRequests === 0;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      {noExportRequests && (
        <Alert severity="info">No export requests found for the selected filters.</Alert>
      )}

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', xl: 'repeat(4, 1fr)' },
          gap: 2,
        }}
      >
        <KpiCard
          label="Export requests"
          value={analytics.summary.exportRequests}
          helperText={KPI_HELPERS.exportRequests}
        />
        <KpiCard
          label="Clients with export activity"
          value={analytics.summary.clientsWithExportActivity}
          helperText={KPI_HELPERS.clientsWithExportActivity}
        />
        <KpiCard
          label="Requested rows"
          value={analytics.summary.requestedRows}
          helperText={KPI_HELPERS.requestedRows}
        />
        <KpiCard
          label="Exported domains"
          value={analytics.summary.exportedDomains}
          helperText={KPI_HELPERS.exportedDomains}
        />
      </Box>

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', lg: 'repeat(2, minmax(0, 1fr))' },
          gap: 3,
        }}
      >
        <AnalyticsSection
          title="Top locations"
          helperText="Counts export requests where this location was used in filters. If multiple locations were selected in one export, each selected location is counted once."
        >
          <RankedDemandTable
            items={analytics.topLocations}
            nameLabel="Location"
            emptyText="No location filter data available for the selected period."
          />
        </AnalyticsSection>

        <AnalyticsSection
          title="Top niches"
          helperText="Counts export requests where this niche was used in filters. If multiple niches were selected in one export, each selected niche is counted once."
        >
          <RankedDemandTable
            items={analytics.topNiches}
            nameLabel="Niche"
            emptyText="No niche filter data available for the selected period."
          />
        </AnalyticsSection>

        <AnalyticsSection
          title="Top categories"
          helperText="Counts export requests where this category was used in filters. If multiple categories were selected in one export, each selected category is counted once."
        >
          <RankedDemandTable
            items={analytics.topCategories}
            nameLabel="Category"
            emptyText="No category filter data available for the selected period."
          />
        </AnalyticsSection>

        <AnalyticsSection
          title="Top languages"
          helperText="Counts export requests where this language was used in filters. If multiple languages were selected in one export, each selected language is counted once."
        >
          <RankedDemandTable
            items={analytics.topLanguages.map((item) => ({
              ...item,
              name: formatLanguageName(item.name),
            }))}
            nameLabel="Language"
            emptyText="No language filter data available for the selected period."
          />
        </AnalyticsSection>
      </Box>

      <AnalyticsSection
        title="Service demand"
        helperText={'Counts only export requests where the service filter was explicitly used. "Wanted / Available" means the client filtered for sites where the service is available, has a price, or is marked as YES. "Explicitly NO" means the client filtered for sites where the service is not available. Exports without this service filter are not counted.'}
      >
        <ServiceDemandTable items={analytics.serviceDemand} />
      </AnalyticsSection>

      <AnalyticsSection
        title="Quality demand"
        helperText="Shows exact ranges used in export filters. This counts how often clients requested each range, not how many domains matched that range."
      >
        <QualityDemandTables qualityDemand={analytics.qualityDemand} />
      </AnalyticsSection>

      <AnalyticsSection
        title="Filter result size"
        helperText={`Groups exports by whether clients used filters and how many rows matched before export limits were applied. Large filtered results matched more than ${formatInteger(
          analytics.filterStrictness.broadExportThreshold
        )} rows.`}
      >
        <FilterResultSizeTable resultSize={analytics.filterStrictness} />
      </AnalyticsSection>
    </Box>
  );
}

interface RankedDemandTableProps {
  items: BusinessDemandCount[];
  nameLabel: string;
  emptyText: string;
}

function RankedDemandTable({ items, nameLabel, emptyText }: RankedDemandTableProps) {
  if (items.length === 0) {
    return <EmptyState text={emptyText} />;
  }

  const max = Math.max(...items.map((item) => item.exportRequests));

  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          <TableCell>{nameLabel}</TableCell>
          <TableCell align="right">Export requests</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {items.map((item) => (
          <TableRow key={`${nameLabel}-${item.name}`}>
            <TableCell>
              <Typography variant="body2">{item.name}</Typography>
              <Box
                sx={{
                  mt: 0.75,
                  height: 6,
                  borderRadius: 1,
                  bgcolor: 'grey.100',
                  overflow: 'hidden',
                }}
              >
                <Box
                  sx={{
                    height: '100%',
                    width: `${Math.max((item.exportRequests / max) * 100, 4)}%`,
                    bgcolor: 'primary.main',
                  }}
                />
              </Box>
            </TableCell>
            <TableCell align="right">{formatInteger(item.exportRequests)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

function ServiceDemandTable({ items }: { items: ServiceDemand[] }) {
  if (items.length === 0) {
    return <EmptyState text="No service demand data available for the selected period." />;
  }

  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          <TableCell>Service</TableCell>
          <TableCell align="right">Wanted / Available</TableCell>
          <TableCell align="right">Explicitly NO</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {items.map((item) => (
          <TableRow key={item.service}>
            <TableCell>{item.service}</TableCell>
            <TableCell align="right">{formatInteger(item.wantedOrAvailableRequests)}</TableCell>
            <TableCell align="right">{formatInteger(item.explicitlyNoRequests)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

function QualityDemandTables({ qualityDemand }: { qualityDemand: QualityDemand }) {
  return (
    <Box
      sx={{
        display: 'grid',
        gridTemplateColumns: { xs: '1fr', lg: 'repeat(3, minmax(0, 1fr))' },
        gap: 3,
      }}
    >
      <Box>
        <Typography variant="subtitle2" sx={{ mb: 1 }}>
          DR ranges
        </Typography>
        <RankedDemandTable
          items={qualityDemand.drRanges}
          nameLabel="Range"
          emptyText="No DR range data available for the selected period."
        />
      </Box>
      <Box>
        <Typography variant="subtitle2" sx={{ mb: 1 }}>
          Traffic ranges
        </Typography>
        <RankedDemandTable
          items={qualityDemand.trafficRanges}
          nameLabel="Range"
          emptyText="No traffic range data available for the selected period."
        />
      </Box>
      <Box>
        <Typography variant="subtitle2" sx={{ mb: 1 }}>
          Price ranges
        </Typography>
        <RankedDemandTable
          items={qualityDemand.priceRanges}
          nameLabel="Range"
          emptyText="No price range data available for the selected period."
        />
      </Box>
    </Box>
  );
}

function FilterResultSizeTable({ resultSize }: { resultSize: FilterStrictness }) {
  const items: BusinessDemandCount[] = [
    { name: 'Unfiltered exports', exportRequests: resultSize.noFilters },
    { name: 'Filtered, large result', exportRequests: resultSize.broadExports },
    { name: 'Filtered, narrow result', exportRequests: resultSize.filteredExports },
  ].filter((item) => item.exportRequests > 0);

  if (items.length === 0) {
    return <EmptyState text="No filter result size data available for the selected period." />;
  }

  return (
    <RankedDemandTable
      items={items}
      nameLabel="Result size"
      emptyText="No filter result size data available for the selected period."
    />
  );
}
