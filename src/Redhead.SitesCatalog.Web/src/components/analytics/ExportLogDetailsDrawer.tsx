import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  CircularProgress,
  Divider,
  Drawer,
  IconButton,
  Stack,
  Typography,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { BrandButton } from '../common/BrandButton';
import { adminAnalyticsService } from '../../services/adminAnalytics.service';
import type { ExportLogDetails, ExportLogDetailsRow } from '../../types/analytics.types';
import { formatInteger } from '../../utils/numberFormat';
import { ExportStatusChip } from './AnalyticsShared';
import {
  formatClientName,
  formatDateTime,
  formatDestination,
  formatExportMode,
  formatNullableText,
} from './analyticsDisplayUtils';

interface ExportLogDetailsDrawerProps {
  logId: string | null;
  open: boolean;
  onClose: () => void;
}

export function ExportLogDetailsDrawer({
  logId,
  open,
  onClose,
}: ExportLogDetailsDrawerProps) {
  const [details, setDetails] = useState<ExportLogDetails | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadDetails = useCallback(async () => {
    if (!logId) return;

    setLoading(true);
    setError(null);
    setDetails(null);
    try {
      const data = await adminAnalyticsService.getExportLogDetails(logId);
      setDetails(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load export log details.');
    } finally {
      setLoading(false);
    }
  }, [logId]);

  useEffect(() => {
    if (!open || !logId) {
      setDetails(null);
      setError(null);
      setLoading(false);
      return;
    }

    void loadDetails();
  }, [loadDetails, logId, open]);

  const summaryRows = useMemo(
    () =>
      details
        ? [
            { label: 'Status', value: <ExportStatusChip status={details.status} /> },
            { label: 'Time', value: formatDateTime(details.timestampUtc) },
            { label: 'Client', value: formatClientName(details.email, details.displayName) },
            { label: 'Email', value: details.email },
            { label: 'Destination', value: formatDestination(details.destination) },
            { label: 'Requested rows', value: formatInteger(details.requestedRows) },
            { label: 'Exported rows', value: formatInteger(details.exportedRows) },
            { label: 'Reason', value: formatNullableText(details.outcomeReason) },
            { label: 'Export mode/type', value: formatExportMode(details.exportMode) },
          ]
        : [],
    [details]
  );

  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
      PaperProps={{
        sx: {
          width: { xs: '100%', sm: 520 },
          maxWidth: '100%',
          display: 'flex',
          flexDirection: 'column',
          bgcolor: 'background.paper',
          overflow: 'hidden',
        },
      }}
    >
      <Box
        sx={{
          px: 2,
          py: 1.5,
          display: 'flex',
          alignItems: 'flex-start',
          gap: 1,
          borderBottom: 1,
          borderColor: 'divider',
        }}
      >
        <Box sx={{ flexGrow: 1, minWidth: 0 }}>
          <Typography variant="h6">Export request details</Typography>
          {details && (
            <Typography variant="body2" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>
              {formatClientName(details.email, details.displayName)}
            </Typography>
          )}
        </Box>
        <IconButton aria-label="Close export log details" onClick={onClose} size="small">
          <CloseIcon fontSize="small" />
        </IconButton>
      </Box>

      <Box sx={{ flex: 1, minHeight: 0, overflow: 'auto', p: 2 }}>
        {loading && (
          <Stack alignItems="center" justifyContent="center" spacing={1.5} sx={{ minHeight: 180 }}>
            <CircularProgress size={28} />
            <Typography variant="body2" color="text.secondary">
              Loading export details...
            </Typography>
          </Stack>
        )}

        {!loading && error && (
          <Alert
            severity="error"
            action={
              <BrandButton kind="outline" size="small" onClick={loadDetails}>
                Retry
              </BrandButton>
            }
          >
            {error}
          </Alert>
        )}

        {!loading && !error && details && (
          <Stack spacing={2.25}>
            <DetailSection title="Summary" rows={summaryRows} />

            <Box>
              <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1 }}>
                Applied filters
              </Typography>
              <Stack spacing={1.75}>
                {details.appliedFilters.map((section) => (
                  <DetailSection key={section.title} title={section.title} rows={section.rows} />
                ))}
              </Stack>
            </Box>

            <DetailSection
              title="Sort"
              rows={
                details.sort.items.length > 0
                  ? details.sort.items
                  : [{ label: 'Applied sort', value: details.sort.summary || 'No sort' }]
              }
            />

            <TechnicalDetailsAccordion details={details} />
          </Stack>
        )}
      </Box>
    </Drawer>
  );
}

function DetailSection({
  title,
  rows,
}: {
  title: string;
  rows: ReadonlyArray<ExportLogDetailsRow | { label: string; value: ReactNode }>;
}) {
  return (
    <Box>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.75 }}>
        {title}
      </Typography>
      <Box sx={{ borderTop: 1, borderColor: 'divider' }}>
        {rows.map((row) => (
          <Box
            key={row.label}
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', sm: '170px minmax(0, 1fr)' },
              gap: { xs: 0.25, sm: 1.5 },
              py: 0.85,
              borderBottom: 1,
              borderColor: 'divider',
            }}
          >
            <Typography variant="body2" color="text.secondary">
              {row.label}
            </Typography>
            <Typography variant="body2" sx={{ overflowWrap: 'anywhere' }} component="div">
              {row.value}
            </Typography>
          </Box>
        ))}
      </Box>
    </Box>
  );
}

function TechnicalDetailsAccordion({ details }: { details: ExportLogDetails }) {
  const technicalDetails = details.technicalDetails;
  const rawItems = [
    { label: 'Filters snapshot', value: technicalDetails?.filtersSnapshotJson },
    { label: 'Sort snapshot', value: technicalDetails?.sortSnapshotJson },
    { label: 'Search snapshot', value: technicalDetails?.searchSnapshotJson },
  ].filter((item) => item.value?.trim());

  return (
    <Accordion variant="outlined" disableGutters>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          Technical details
        </Typography>
      </AccordionSummary>
      <AccordionDetails>
        {rawItems.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            No technical snapshot data available.
          </Typography>
        ) : (
          <Stack spacing={1.5}>
            {rawItems.map((item, index) => (
              <Box key={item.label}>
                {index > 0 && <Divider sx={{ mb: 1.5 }} />}
                <Typography variant="body2" sx={{ fontWeight: 700, mb: 0.75 }}>
                  {item.label}
                </Typography>
                <Box
                  component="pre"
                  sx={{
                    m: 0,
                    p: 1.25,
                    borderRadius: 1,
                    bgcolor: 'grey.100',
                    color: 'text.primary',
                    fontSize: 12,
                    lineHeight: 1.5,
                    overflow: 'auto',
                    whiteSpace: 'pre-wrap',
                    wordBreak: 'break-word',
                  }}
                >
                  {formatRawJson(item.value ?? '')}
                </Box>
              </Box>
            ))}
          </Stack>
        )}
      </AccordionDetails>
    </Accordion>
  );
}

function formatRawJson(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}
