import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Navigate } from 'react-router-dom';
import {
  Alert,
  Autocomplete,
  Box,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import dayjs, { type Dayjs } from 'dayjs';
import { PageShell } from '../components/layout/PageShell';
import { BrandButton } from '../components/common/BrandButton';
import { AnalyticsLoadingSkeleton } from '../components/analytics/AnalyticsLoadingSkeleton';
import { useUserRoles } from '../hooks/useUserRoles';
import { adminAnalyticsService } from '../services/adminAnalytics.service';
import type {
  AnalyticsClientOption,
  BusinessDemandAnalytics,
  BusinessDemandCount,
  BusinessDemandDestinationFilter,
  BusinessDemandStatusFilter,
  FilterStrictness,
  QualityDemand,
  ServiceDemand,
} from '../types/analytics.types';
import { formatInteger } from '../utils/numberFormat';
import { getLanguageOption } from '../utils/language';

type DateRangePreset = 'last7' | 'last30' | 'last90' | 'custom';
type DestinationFilterValue = 'all' | BusinessDemandDestinationFilter;
type StatusFilterValue = 'all' | BusinessDemandStatusFilter;

const ALL_CLIENT_OPTION: AnalyticsClientOption = {
  id: 'all',
  email: '',
  displayName: 'All clients',
};

const KPI_HELPERS = {
  exportRequests:
    'All export attempts in the selected period, including successful, partial, and blocked requests.',
  clientsWithExportActivity:
    'Unique Client users who made at least one export request in the selected period.',
  requestedRows: 'Total rows clients attempted to receive before export limits were applied.',
  exportedDomains: 'Domains actually exported after export limits were applied.',
};

function formatApiDate(value: Dayjs): string {
  return value.format('YYYY-MM-DD');
}

function getPresetRange(preset: Exclude<DateRangePreset, 'custom'>) {
  const today = dayjs().startOf('day');
  const days = preset === 'last7' ? 7 : preset === 'last90' ? 90 : 30;
  return {
    from: today.subtract(days - 1, 'day'),
    to: today,
  };
}

function getClientOptionLabel(option: AnalyticsClientOption): string {
  if (option.id === ALL_CLIENT_OPTION.id) return option.displayName;
  return option.displayName === option.email
    ? option.email
    : `${option.displayName} (${option.email})`;
}

function formatLanguageName(value: string): string {
  return getLanguageOption(value)?.label ?? value;
}

export const Analytics: React.FC = () => {
  const { isSuperAdmin } = useUserRoles();
  const [datePreset, setDatePreset] = useState<DateRangePreset>('last30');
  const [customFrom, setCustomFrom] = useState<Dayjs | null>(() => getPresetRange('last30').from);
  const [customTo, setCustomTo] = useState<Dayjs | null>(() => getPresetRange('last30').to);
  const [clientId, setClientId] = useState<string | null>(null);
  const [destination, setDestination] = useState<DestinationFilterValue>('all');
  const [status, setStatus] = useState<StatusFilterValue>('all');
  const [clientOptions, setClientOptions] = useState<AnalyticsClientOption[]>([]);
  const [clientsLoading, setClientsLoading] = useState(false);
  const [clientsError, setClientsError] = useState<string | null>(null);
  const [analytics, setAnalytics] = useState<BusinessDemandAnalytics | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const clientSelectOptions = useMemo(
    () => [ALL_CLIENT_OPTION, ...clientOptions],
    [clientOptions]
  );
  const selectedClient = useMemo(
    () => clientSelectOptions.find((option) => option.id === (clientId ?? 'all')) ?? ALL_CLIENT_OPTION,
    [clientId, clientSelectOptions]
  );

  const selectedRange = useMemo(() => {
    if (datePreset !== 'custom') {
      return getPresetRange(datePreset);
    }

    if (!customFrom || !customTo || !customFrom.isValid() || !customTo.isValid()) {
      return null;
    }

    return {
      from: customFrom.startOf('day'),
      to: customTo.startOf('day'),
    };
  }, [customFrom, customTo, datePreset]);

  const dateRangeError = useMemo(() => {
    if (datePreset !== 'custom') return null;
    if (!customFrom || !customTo) return 'Choose both custom dates.';
    if (!customFrom.isValid() || !customTo.isValid()) return 'Choose valid custom dates.';
    if (customFrom.startOf('day').isAfter(customTo.startOf('day'))) {
      return 'From date must be earlier than or equal to to date.';
    }
    return null;
  }, [customFrom, customTo, datePreset]);

  const loadAnalytics = useCallback(async () => {
    if (!isSuperAdmin || !selectedRange || dateRangeError) return;

    setLoading(true);
    setError(null);
    try {
      const data = await adminAnalyticsService.getBusinessDemand({
        from: formatApiDate(selectedRange.from),
        to: formatApiDate(selectedRange.to),
        clientId: clientId ?? undefined,
        destination: destination === 'all' ? undefined : destination,
        status: status === 'all' ? undefined : status,
      });
      setAnalytics(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load analytics.');
    } finally {
      setLoading(false);
    }
  }, [clientId, dateRangeError, destination, isSuperAdmin, selectedRange, status]);

  useEffect(() => {
    if (!isSuperAdmin) return;

    setClientsLoading(true);
    setClientsError(null);
    adminAnalyticsService
      .listClients()
      .then(setClientOptions)
      .catch((err) =>
        setClientsError(err instanceof Error ? err.message : 'Failed to load clients.')
      )
      .finally(() => setClientsLoading(false));
  }, [isSuperAdmin]);

  useEffect(() => {
    loadAnalytics();
  }, [loadAnalytics]);

  if (!isSuperAdmin) {
    return <Navigate to="/sites" replace />;
  }

  const noExportRequests = analytics?.summary.exportRequests === 0;

  return (
    <PageShell title="Analytics" maxWidth="xl">
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Business demand based on client export requests.
      </Typography>

      <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: {
              xs: '1fr',
              sm: 'repeat(2, minmax(0, 1fr))',
              lg: '1fr 1.4fr 1fr 1fr',
            },
            gap: 2,
            alignItems: 'flex-start',
          }}
        >
          <FormControl size="small" fullWidth>
            <InputLabel>Date range</InputLabel>
            <Select
              value={datePreset}
              label="Date range"
              onChange={(event) => setDatePreset(event.target.value as DateRangePreset)}
            >
              <MenuItem value="last7">Last 7 days</MenuItem>
              <MenuItem value="last30">Last 30 days</MenuItem>
              <MenuItem value="last90">Last 90 days</MenuItem>
              <MenuItem value="custom">Custom</MenuItem>
            </Select>
          </FormControl>

          <Autocomplete
            size="small"
            options={clientSelectOptions}
            value={selectedClient}
            getOptionLabel={getClientOptionLabel}
            isOptionEqualToValue={(option, value) => option.id === value.id}
            onChange={(_event, option) => setClientId(option?.id === 'all' ? null : option?.id ?? null)}
            loading={clientsLoading}
            renderInput={(params) => (
              <TextField {...params} label="Client" placeholder="All clients" />
            )}
          />

          <FormControl size="small" fullWidth>
            <InputLabel>Destination</InputLabel>
            <Select
              value={destination}
              label="Destination"
              onChange={(event) => setDestination(event.target.value as DestinationFilterValue)}
            >
              <MenuItem value="all">All destinations</MenuItem>
              <MenuItem value="Download">Download</MenuItem>
              <MenuItem value="GoogleDrive">Google Drive</MenuItem>
            </Select>
          </FormControl>

          <FormControl size="small" fullWidth>
            <InputLabel>Status</InputLabel>
            <Select
              value={status}
              label="Status"
              onChange={(event) => setStatus(event.target.value as StatusFilterValue)}
            >
              <MenuItem value="all">All statuses</MenuItem>
              <MenuItem value="successful">Successful</MenuItem>
              <MenuItem value="partial">Partial</MenuItem>
              <MenuItem value="blocked">Blocked</MenuItem>
            </Select>
          </FormControl>
        </Box>

        {datePreset === 'custom' && (
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, minmax(0, 240px))' },
              gap: 2,
              mt: 2,
            }}
          >
            <DatePicker
              label="From"
              value={customFrom}
              onChange={setCustomFrom}
              slotProps={{
                textField: {
                  size: 'small',
                  error: !!dateRangeError,
                  helperText: dateRangeError ?? undefined,
                },
              }}
            />
            <DatePicker
              label="To"
              value={customTo}
              onChange={setCustomTo}
              slotProps={{
                textField: {
                  size: 'small',
                  error: !!dateRangeError,
                },
              }}
            />
          </Box>
        )}
      </Paper>

      {clientsError && (
        <Alert severity="warning" sx={{ mb: 2 }} onClose={() => setClientsError(null)}>
          {clientsError}
        </Alert>
      )}

      {error && (
        <Alert
          severity="error"
          sx={{ mb: 2, alignItems: 'center' }}
          action={
            <BrandButton kind="outline" size="small" onClick={loadAnalytics}>
              Retry
            </BrandButton>
          }
        >
          {error}
        </Alert>
      )}

      {loading ? (
        <AnalyticsLoadingSkeleton />
      ) : (
        analytics && (
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
              title="Filter strictness"
              helperText={`This is based on requested rows before export limits are applied. Broad exports are requests with filters that still matched more than ${formatInteger(
                analytics.filterStrictness.broadExportThreshold
              )} rows.`}
            >
              <FilterStrictnessTable strictness={analytics.filterStrictness} />
            </AnalyticsSection>
          </Box>
        )
      )}
    </PageShell>
  );
};

interface KpiCardProps {
  label: string;
  value: number;
  helperText: string;
}

function KpiCard({ label, value, helperText }: KpiCardProps) {
  return (
    <Paper variant="outlined" sx={{ p: 2, height: '100%' }}>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 0.75 }}>
        {label}
      </Typography>
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 1 }}>
        {formatInteger(value)}
      </Typography>
      <Typography variant="body2" color="text.secondary">
        {helperText}
      </Typography>
    </Paper>
  );
}

interface AnalyticsSectionProps {
  title: string;
  helperText: string;
  children: React.ReactNode;
}

function AnalyticsSection({ title, helperText, children }: AnalyticsSectionProps) {
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Typography variant="h6" sx={{ fontWeight: 700 }}>
        {title}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 2 }}>
        {helperText}
      </Typography>
      {children}
    </Paper>
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

function FilterStrictnessTable({ strictness }: { strictness: FilterStrictness }) {
  const items: BusinessDemandCount[] = [
    { name: 'No filters', exportRequests: strictness.noFilters },
    { name: 'Broad exports', exportRequests: strictness.broadExports },
    { name: 'Filtered exports', exportRequests: strictness.filteredExports },
  ].filter((item) => item.exportRequests > 0);

  if (items.length === 0) {
    return <EmptyState text="No filter strictness data available for the selected period." />;
  }

  return (
    <RankedDemandTable
      items={items}
      nameLabel="Strictness"
      emptyText="No filter strictness data available for the selected period."
    />
  );
}

function EmptyState({ text }: { text: string }) {
  return (
    <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
      {text}
    </Typography>
  );
}
