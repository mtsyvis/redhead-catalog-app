import { useCallback, useMemo, useState } from 'react';
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
import { DataGrid, type GridColDef, type GridPaginationModel } from '@mui/x-data-grid';
import type {
  ExportActivityAnalytics,
  ExportActivityClientSummaryItem,
  ExportActivityOverTimeItem,
  ExportActivityRecentExportItem,
} from '../../types/analytics.types';
import { dataGridLocaleText, formatInteger } from '../../utils/numberFormat';
import {
  AnalyticsSection,
  EmptyState,
  ExportStatusChip,
  AnalyticsMetrics,
} from './AnalyticsShared';
import { analyticsGridSx } from './analyticsGridStyles';
import {
  formatClientName,
  formatDateTime,
  formatDestination,
  formatNullableText,
} from './analyticsDisplayUtils';
import { ExportLogDetailsDrawer } from './ExportLogDetailsDrawer';

const EXPORT_ACTIVITY_KPI_HELPERS = {
  completedExports: 'Successful and partial exports where the client received data.',
  partialExports: 'Exports that were reduced because an export limit was reached.',
  blockedExports: 'Export attempts that did not produce a file.',
  uniqueExportedDomains: 'Unique domains actually exported in the selected period.',
};

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
  const [selectedExportLogId, setSelectedExportLogId] = useState<string | null>(null);
  const noExportActivity =
    analytics.summary.completedExports === 0 && analytics.summary.blockedExports === 0;
  const handleOpenDetails = useCallback((id: string) => {
    setSelectedExportLogId(id);
  }, []);
  const handleCloseDetails = useCallback(() => {
    setSelectedExportLogId(null);
  }, []);

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      {noExportActivity && (
        <Alert severity="info">No export activity found for the selected filters.</Alert>
      )}

      <AnalyticsMetrics
        metrics={[
          {
            label: 'Completed exports',
            value: analytics.summary.completedExports,
            helperText: EXPORT_ACTIVITY_KPI_HELPERS.completedExports,
          },
          {
            label: 'Partial exports',
            value: analytics.summary.partialExports,
            helperText: EXPORT_ACTIVITY_KPI_HELPERS.partialExports,
          },
          {
            label: 'Blocked exports',
            value: analytics.summary.blockedExports,
            helperText: EXPORT_ACTIVITY_KPI_HELPERS.blockedExports,
          },
          {
            label: 'Unique exported domains',
            value: analytics.summary.uniqueExportedDomains,
            helperText: EXPORT_ACTIVITY_KPI_HELPERS.uniqueExportedDomains,
          },
          {
            label: 'Requested rows',
            value: analytics.summary.requestedRows,
            helperText: 'Rows clients requested before export limits were applied.',
          },
          {
            label: 'Exported rows',
            value: analytics.summary.exportedRows,
            helperText: 'Rows clients actually received after export limits were applied.',
          },
        ]}
      />
      <AnalyticsSection
        title="Exports over time"
        helperText="Groups export activity by day based on export timestamp."
      >
        <ExportsOverTimeTable items={analytics.exportsOverTime} />
      </AnalyticsSection>

      <AnalyticsSection
        title="Client export summary"
        helperText="Summarizes client export results in the selected period."
      >
        <ClientExportSummaryTable rows={analytics.clientSummaries} />
      </AnalyticsSection>

      <AnalyticsSection
        title="Recent export logs"
        helperText="Shows recent client export requests for the selected filters. Click a row to view details."
      >
        <RecentExportsTable
          rows={analytics.recentExports.items}
          totalCount={analytics.recentExports.totalCount}
          paginationModel={recentExportsPaginationModel}
          onPaginationModelChange={onRecentExportsPaginationChange}
          onViewDetails={handleOpenDetails}
        />
      </AnalyticsSection>

      <ExportLogDetailsDrawer
        open={selectedExportLogId !== null}
        logId={selectedExportLogId}
        onClose={handleCloseDetails}
      />
    </Box>
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

function ClientExportSummaryTable({ rows }: { rows: ExportActivityClientSummaryItem[] }) {
  const columns = useMemo<GridColDef<ExportActivityClientSummaryItem>[]>(
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
        field: 'successfulExports',
        headerName: 'Successful',
        minWidth: 105,
        flex: 0.6,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'partialExports',
        headerName: 'Partial',
        minWidth: 95,
        flex: 0.55,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'blockedExports',
        headerName: 'Blocked',
        minWidth: 95,
        flex: 0.55,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'requestedRows',
        headerName: 'Requested rows',
        minWidth: 135,
        flex: 0.75,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'exportedRows',
        headerName: 'Exported rows',
        minWidth: 130,
        flex: 0.75,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'lastExportAtUtc',
        headerName: 'Last export in period',
        minWidth: 180,
        flex: 0.95,
        valueFormatter: (value) => formatDateTime(value as string | null | undefined),
      },
    ],
    []
  );

  if (rows.length === 0) {
    return <EmptyState text="No client export activity found for the selected filters." />;
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
  onViewDetails,
}: {
  rows: ExportActivityRecentExportItem[];
  totalCount: number;
  paginationModel: GridPaginationModel;
  onPaginationModelChange: (model: GridPaginationModel) => void;
  onViewDetails: (id: string) => void;
}) {
  const columns = useMemo<GridColDef<ExportActivityRecentExportItem>[]>(
    () => [
      {
        field: 'timestampUtc',
        headerName: 'Time',
        minWidth: 160,
        valueFormatter: (value) => formatDateTime(value as string | null | undefined),
      },
      {
        field: 'client',
        headerName: 'Client',
        minWidth: 220,
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
        field: 'destination',
        headerName: 'Destination',
        minWidth: 110,
        flex: 0.75,
        valueFormatter: (value) => formatDestination(value as string | null | undefined),
      },
      {
        field: 'status',
        headerName: 'Status',
        minWidth: 100,
        flex: 0.75,
        renderCell: (params) => <ExportStatusChip status={params.row.status} />,
      },
      {
        field: 'requestedRows',
        headerName: 'Requested',
        minWidth: 100,
        flex: 0.7,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'exportedRows',
        headerName: 'Exported',
        minWidth: 95,
        flex: 0.7,
        align: 'right',
        headerAlign: 'right',
        valueFormatter: (value) => formatInteger(value as number),
      },
      {
        field: 'reason',
        headerName: 'Reason',
        minWidth: 140,
        flex: 0.95,
        renderCell: (params) => (
          <Typography variant="body2" sx={multiLineCellSx}>
            {formatNullableText(params.row.reason)}
          </Typography>
        ),
      },
      {
        field: 'filtersSummary',
        headerName: 'Filters',
        minWidth: 130,
        flex: 0.9,
        renderCell: (params) => (
          <Typography variant="body2" sx={multiLineCellSx}>
            {formatNullableText(params.row.filtersSummary)}
          </Typography>
        ),
      },
      {
        field: 'sortSummary',
        headerName: 'Sort',
        minWidth: 120,
        flex: 0.8,
        renderCell: (params) => (
          <Typography variant="body2" sx={multiLineCellSx}>
            {formatNullableText(params.row.sortSummary)}
          </Typography>
        ),
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
      onRowClick={(params) => onViewDetails(params.row.id)}
      localeText={dataGridLocaleText}
      disableRowSelectionOnClick
      disableColumnMenu
      autoHeight
      getRowHeight={() => 'auto'}
      sx={clickableAnalyticsGridSx}
    />
  );
}

const clickableAnalyticsGridSx = {
  ...analyticsGridSx,
  '& .MuiDataGrid-row': {
    cursor: 'pointer',
  },
  '& .MuiDataGrid-row:hover': {
    backgroundColor: 'action.hover',
  },
};

const multiLineCellSx = {
  lineHeight: 1.4,
  whiteSpace: 'normal',
  overflowWrap: 'anywhere',
};
