import { useMemo } from 'react';
import {
  Alert,
  Box,
  Chip,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import {
  DataGrid,
  type GridColDef,
  type GridPaginationModel,
} from '@mui/x-data-grid';
import type {
  ExportActivityAnalytics,
  ExportActivityClientUsageItem,
  ExportActivityClientUsageStatus,
  ExportActivityOverTimeItem,
  ExportActivityRecentExportItem,
  ExportActivityStatus,
} from '../../types/analytics.types';
import { dataGridLocaleText, formatInteger } from '../../utils/numberFormat';
import { AnalyticsSection, EmptyState, KpiCard } from './AnalyticsShared';

const EXPORT_ACTIVITY_KPI_HELPERS = {
  completedExports: 'Successful and partial exports where the client received data.',
  partialExports: 'Exports that were reduced because an export limit was reached.',
  blockedExports: 'Export attempts that did not produce a file.',
  uniqueExportedDomains: 'Unique domains actually exported in the selected period.',
  requestedVsExported:
    'Requested rows show what clients attempted to receive before limits. Exported rows show what clients actually received.',
};

const ROLLING_USAGE_HELPER =
  '24h and 7d usage use rolling windows and may differ from the selected date range.';

export interface ExportActivityTabProps {
  analytics: ExportActivityAnalytics;
  recentExportsPaginationModel: GridPaginationModel;
  onRecentExportsPaginationChange: (model: GridPaginationModel) => void;
}

export function ExportActivityTab({
  analytics,
  recentExportsPaginationModel,
  onRecentExportsPaginationChange,
}: ExportActivityTabProps) {
  const noExportActivity =
    analytics.summary.completedExports === 0 && analytics.summary.blockedExports === 0;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      {noExportActivity && (
        <Alert severity="info">No export activity found for the selected filters.</Alert>
      )}

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', xl: 'repeat(4, 1fr)' },
          gap: 2,
        }}
      >
        <KpiCard
          label="Completed exports"
          value={analytics.summary.completedExports}
          helperText={EXPORT_ACTIVITY_KPI_HELPERS.completedExports}
        />
        <KpiCard
          label="Partial exports"
          value={analytics.summary.partialExports}
          helperText={EXPORT_ACTIVITY_KPI_HELPERS.partialExports}
        />
        <KpiCard
          label="Blocked exports"
          value={analytics.summary.blockedExports}
          helperText={EXPORT_ACTIVITY_KPI_HELPERS.blockedExports}
        />
        <KpiCard
          label="Unique exported domains"
          value={analytics.summary.uniqueExportedDomains}
          helperText={EXPORT_ACTIVITY_KPI_HELPERS.uniqueExportedDomains}
        />
      </Box>

      <RequestedVsExportedCard
        requestedRows={analytics.summary.requestedRows}
        exportedRows={analytics.summary.exportedRows}
      />

      <AnalyticsSection
        title="Exports over time"
        helperText="Groups export activity by day based on export timestamp."
      >
        <ExportsOverTimeTable items={analytics.exportsOverTime} />
      </AnalyticsSection>

      <AnalyticsSection title="Client usage" helperText={ROLLING_USAGE_HELPER}>
        <ClientUsageTable rows={analytics.clientUsage} />
      </AnalyticsSection>

      <AnalyticsSection
        title="Recent export logs"
        helperText="Shows recent client export requests for the selected filters."
      >
        <RecentExportsTable
          rows={analytics.recentExports.items}
          totalCount={analytics.recentExports.totalCount}
          paginationModel={recentExportsPaginationModel}
          onPaginationModelChange={onRecentExportsPaginationChange}
        />
      </AnalyticsSection>
    </Box>
  );
}

function RequestedVsExportedCard({
  requestedRows,
  exportedRows,
}: {
  requestedRows: number;
  exportedRows: number;
}) {
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 0.75 }}>
        Requested vs exported
      </Typography>
      <Box sx={{ display: 'flex', gap: 3, flexWrap: 'wrap', mb: 1 }}>
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          Requested: {formatInteger(requestedRows)}
        </Typography>
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          Exported: {formatInteger(exportedRows)}
        </Typography>
      </Box>
      <Typography variant="body2" color="text.secondary">
        {EXPORT_ACTIVITY_KPI_HELPERS.requestedVsExported}
      </Typography>
    </Paper>
  );
}

function ExportsOverTimeTable({ items }: { items: ExportActivityOverTimeItem[] }) {
  if (items.length === 0) {
    return <EmptyState text="No export activity found for the selected filters." />;
  }

  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          <TableCell>Date</TableCell>
          <TableCell align="right">Successful</TableCell>
          <TableCell align="right">Partial</TableCell>
          <TableCell align="right">Blocked</TableCell>
          <TableCell align="right">Exported domains</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {items.map((item) => (
          <TableRow key={item.date}>
            <TableCell>{item.date}</TableCell>
            <TableCell align="right">{formatInteger(item.successfulExports)}</TableCell>
            <TableCell align="right">{formatInteger(item.partialExports)}</TableCell>
            <TableCell align="right">{formatInteger(item.blockedExports)}</TableCell>
            <TableCell align="right">{formatInteger(item.exportedDomains)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

function ClientUsageTable({ rows }: { rows: ExportActivityClientUsageItem[] }) {
  const columns = useMemo<GridColDef<ExportActivityClientUsageItem>[]>(
    () => [
      {
        field: 'client',
        headerName: 'Client',
        minWidth: 240,
        flex: 1.2,
        sortable: false,
        valueGetter: (_value, row) => formatClientName(row.email, row.displayName),
        renderCell: (params) => (
          <Typography variant="body2" sx={{ overflowWrap: 'anywhere' }}>
            {formatClientName(params.row.email, params.row.displayName)}
          </Typography>
        ),
      },
      {
        field: 'dailyUniqueDomainsUsed',
        headerName: '24h domains',
        minWidth: 130,
        valueGetter: (_value, row) =>
          formatUsageValue(row.dailyUniqueDomainsUsed, row.dailyUniqueDomainsLimit),
      },
      {
        field: 'weeklyUniqueDomainsUsed',
        headerName: '7d domains',
        minWidth: 130,
        valueGetter: (_value, row) =>
          formatUsageValue(row.weeklyUniqueDomainsUsed, row.weeklyUniqueDomainsLimit),
      },
      {
        field: 'dailyExportOperationsUsed',
        headerName: '24h exports',
        minWidth: 125,
        valueGetter: (_value, row) =>
          formatUsageValue(row.dailyExportOperationsUsed, row.dailyExportOperationsLimit),
      },
      {
        field: 'weeklyExportOperationsUsed',
        headerName: '7d exports',
        minWidth: 125,
        valueGetter: (_value, row) =>
          formatUsageValue(row.weeklyExportOperationsUsed, row.weeklyExportOperationsLimit),
      },
      {
        field: 'partialExports',
        headerName: 'Partial',
        minWidth: 95,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'blockedExports',
        headerName: 'Blocked',
        minWidth: 95,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'requestedRows',
        headerName: 'Requested rows',
        minWidth: 140,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'exportedRows',
        headerName: 'Exported rows',
        minWidth: 135,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'lastExportAtUtc',
        headerName: 'Last export',
        minWidth: 180,
        valueFormatter: (value) => formatDateTime(value as string | null | undefined),
      },
      {
        field: 'status',
        headerName: 'Status',
        minWidth: 130,
        renderCell: (params) => <ClientUsageStatusChip status={params.row.status} />,
      },
    ],
    []
  );

  if (rows.length === 0) {
    return <EmptyState text="No client usage data available." />;
  }

  return (
    <DataGrid
      rows={rows}
      columns={columns}
      getRowId={(row) => row.userId}
      pageSizeOptions={[10, 25, 50, 100]}
      initialState={{
        pagination: { paginationModel: { page: 0, pageSize: 25 } },
      }}
      localeText={dataGridLocaleText}
      disableRowSelectionOnClick
      disableColumnMenu
      autoHeight
      getRowHeight={() => 'auto'}
      sx={analyticsGridSx}
    />
  );
}

function RecentExportsTable({
  rows,
  totalCount,
  paginationModel,
  onPaginationModelChange,
}: {
  rows: ExportActivityRecentExportItem[];
  totalCount: number;
  paginationModel: GridPaginationModel;
  onPaginationModelChange: (model: GridPaginationModel) => void;
}) {
  const columns = useMemo<GridColDef<ExportActivityRecentExportItem>[]>(
    () => [
      {
        field: 'timestampUtc',
        headerName: 'Time',
        minWidth: 180,
        valueFormatter: (value) => formatDateTime(value as string | null | undefined),
      },
      {
        field: 'client',
        headerName: 'Client',
        minWidth: 240,
        flex: 1,
        sortable: false,
        valueGetter: (_value, row) => formatClientName(row.email, row.displayName),
        renderCell: (params) => (
          <Typography variant="body2" sx={{ overflowWrap: 'anywhere' }}>
            {formatClientName(params.row.email, params.row.displayName)}
          </Typography>
        ),
      },
      {
        field: 'destination',
        headerName: 'Destination',
        minWidth: 130,
        valueFormatter: (value) => formatDestination(value as string | null | undefined),
      },
      {
        field: 'status',
        headerName: 'Status',
        minWidth: 120,
        renderCell: (params) => <ExportStatusChip status={params.row.status} />,
      },
      {
        field: 'requestedRows',
        headerName: 'Requested',
        minWidth: 115,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'exportedRows',
        headerName: 'Exported',
        minWidth: 110,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'reason',
        headerName: 'Reason',
        minWidth: 160,
        valueFormatter: (value) => formatNullableText(value as string | null | undefined),
      },
      {
        field: 'filtersSummary',
        headerName: 'Filters',
        minWidth: 220,
        flex: 1,
        valueFormatter: (value) => formatNullableText(value as string | null | undefined),
      },
      {
        field: 'sortSummary',
        headerName: 'Sort',
        minWidth: 150,
        valueFormatter: (value) => formatNullableText(value as string | null | undefined),
      },
    ],
    []
  );

  if (totalCount === 0) {
    return <EmptyState text="No recent exports found." />;
  }

  return (
    <DataGrid
      rows={rows}
      columns={columns}
      getRowId={(row) => row.id}
      rowCount={totalCount}
      pageSizeOptions={[10, 25, 50, 100]}
      paginationModel={paginationModel}
      paginationMode="server"
      onPaginationModelChange={onPaginationModelChange}
      localeText={dataGridLocaleText}
      disableRowSelectionOnClick
      disableColumnMenu
      autoHeight
      getRowHeight={() => 'auto'}
      sx={analyticsGridSx}
    />
  );
}

function ClientUsageStatusChip({ status }: { status: ExportActivityClientUsageStatus }) {
  const label = status === 'NearLimit'
    ? 'Near limit'
    : status === 'LimitReached'
      ? 'Limit reached'
      : 'Normal';
  const color = status === 'LimitReached'
    ? 'error'
    : status === 'NearLimit'
      ? 'warning'
      : 'success';

  return <Chip size="small" label={label} color={color} variant="outlined" />;
}

function ExportStatusChip({ status }: { status: ExportActivityStatus }) {
  const color = status === 'Blocked'
    ? 'error'
    : status === 'Partial'
      ? 'warning'
      : 'success';

  return <Chip size="small" label={status} color={color} variant="outlined" />;
}

function formatDateTime(value: string | null | undefined): string {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString();
}

function formatDestination(value: string | null | undefined): string {
  if (value === 'GoogleDrive') return 'Google Drive';
  if (value === 'Download') return 'Download';
  return value?.trim() || '—';
}

function formatNullableText(value: string | null | undefined): string {
  return value?.trim() || '—';
}

function formatUsageValue(used: number, limit: number | null | undefined): string {
  if (limit == null) return '—';
  return `${formatInteger(used)} / ${formatInteger(limit)}`;
}

function formatClientName(email: string, displayName?: string | null): string {
  const name = displayName?.trim();
  if (!name || name === email) return email;
  return `${name} (${email})`;
}

const analyticsGridSx = {
  '& .MuiDataGrid-cell': {
    display: 'flex',
    alignItems: 'center',
    py: 0.75,
  },
  '& .MuiDataGrid-cell:focus': {
    outline: 'none',
  },
  '& .MuiDataGrid-cell:focus-within': {
    outline: 'none',
  },
  '& .MuiDataGrid-columnHeader': {
    backgroundColor: 'action.hover',
  },
  '& .MuiDataGrid-columnHeader:focus': {
    outline: 'none',
  },
  '& .MuiDataGrid-columnHeader:focus-within': {
    outline: 'none',
  },
};
