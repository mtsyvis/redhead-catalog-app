import { PAGE_SIZE_OPTIONS } from '../../constants/pagination.constants';
import { useMemo } from 'react';
import { Box, Button, Paper, Typography } from '@mui/material';
import { DataGrid, type GridColDef, type GridPaginationModel } from '@mui/x-data-grid';
import { SyncStatusChip } from './LinkDetectiveSyncDisplay';
import type { LinkDetectiveSyncRun, SyncPage } from '../../types/linkDetectiveSync.types';
import { dataGridLocaleText, formatInteger } from '../../utils/numberFormat';
import { syncDate } from '../../utils/linkDetectiveSyncDisplay';

interface RunHistoryProps {
  runs: SyncPage<LinkDetectiveSyncRun> | null;
  pagination: GridPaginationModel;
  onPaginationChange: (value: GridPaginationModel) => void;
  loading: boolean;
  openRun: (id: string) => void;
}

export function LinkDetectiveSyncRunHistory({
  runs,
  pagination,
  onPaginationChange,
  loading,
  openRun,
}: RunHistoryProps) {
  const runColumns = useMemo<GridColDef<LinkDetectiveSyncRun>[]>(
    () =>
      (
        [
          {
            field: 'startedAtUtc',
            headerName: 'Started',
            minWidth: 180,
            flex: 1,
            valueFormatter: (value) => syncDate(value),
          },
          {
            field: 'status',
            headerName: 'Status',
            minWidth: 200,
            renderCell: ({ row }) => <SyncStatusChip status={row.status} />,
          },
          {
            field: 'processedCount',
            headerName: 'Processed / selected',
            minWidth: 180,
            valueGetter: (_value, row) =>
              `${formatInteger(row.processedCount)} / ${formatInteger(row.selectedCount)}`,
          },
          {
            field: 'failedCount',
            headerName: 'Errors',
            width: 90,
            valueFormatter: (value) => formatInteger(value as number),
          },
          {
            field: 'finishedAtUtc',
            headerName: 'Finished',
            minWidth: 180,
            flex: 1,
            valueFormatter: (value) => syncDate(value),
          },
          {
            field: 'actions',
            headerName: '',
            width: 110,
            renderCell: ({ row }) => (
              <Button
                onClick={(event) => {
                  event.stopPropagation();
                  openRun(row.id);
                }}
              >
                View details
              </Button>
            ),
          },
        ] satisfies GridColDef<LinkDetectiveSyncRun>[]
      ).map((column) => ({ ...column, sortable: false })),
    [openRun]
  );

  return (
    <Paper variant="outlined" sx={{ p: 2.5 }}>
      <Typography variant="h6">Run history</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        One run can span several days. Select a run to inspect domain results.
      </Typography>
      <Box sx={{ overflowX: 'auto' }}>
        <DataGrid
          autoHeight
          rows={runs?.items ?? []}
          columns={runColumns}
          rowCount={runs?.totalCount ?? 0}
          paginationMode="server"
          paginationModel={pagination}
          onPaginationModelChange={onPaginationChange}
          pageSizeOptions={PAGE_SIZE_OPTIONS}
          loading={loading}
          disableRowSelectionOnClick
          disableColumnFilter
          localeText={dataGridLocaleText}
          onRowClick={({ row }) => openRun(row.id)}
          sx={{ minWidth: 900, border: 0, '& .MuiDataGrid-row': { cursor: 'pointer' } }}
        />
      </Box>
    </Paper>
  );
}
