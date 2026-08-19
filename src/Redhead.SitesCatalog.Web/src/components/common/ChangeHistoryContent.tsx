import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Stack,
  Typography,
} from '@mui/material';
import { alpha } from '@mui/material/styles';
import type { EntityChangeHistoryItem } from '../../types/changeHistory.types';

interface Props {
  readonly items: EntityChangeHistoryItem[] | null;
  readonly loading: boolean;
  readonly error: string | null;
}

function formatDate(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleString(undefined, {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
}

function formatSource(value: string) {
  return value.toLowerCase() === 'manual' ? 'Manual edit' : value;
}

function changesLabel(count: number) {
  return `${count} ${count === 1 ? 'field' : 'fields'} changed`;
}

interface HistoryValueProps {
  readonly value: string | null;
  readonly kind: 'before' | 'after';
}

function HistoryValue({ value, kind }: HistoryValueProps) {
  const [expanded, setExpanded] = useState(false);
  const text = value?.trim() ?? '';
  const isEmpty = text.length === 0;
  const isLong = text.length > 240 || text.split('\n').length > 4;

  return (
    <Box
      sx={(theme) => ({
        minWidth: 0,
        px: 1.5,
        py: 1.25,
        borderLeft: 3,
        borderColor: kind === 'after' ? 'success.main' : 'divider',
        bgcolor: kind === 'after'
          ? alpha(theme.palette.success.main, 0.06)
          : alpha(theme.palette.text.primary, 0.025),
        borderRadius: 0.5,
      })}
    >
      <Typography
        variant="body2"
        color={isEmpty || kind === 'before' ? 'text.secondary' : 'text.primary'}
        sx={{
          whiteSpace: 'pre-wrap',
          overflowWrap: 'anywhere',
          fontStyle: isEmpty ? 'italic' : 'normal',
          ...(!expanded && isLong ? {
            display: '-webkit-box',
            WebkitBoxOrient: 'vertical',
            WebkitLineClamp: 4,
            overflow: 'hidden',
          } : {}),
        }}
      >
        {isEmpty ? 'Not set' : text}
      </Typography>
      {isLong && (
        <Button
          size="small"
          variant="text"
          onClick={() => setExpanded((current) => !current)}
          sx={{ minWidth: 0, mt: 0.5, p: 0, fontSize: '0.75rem' }}
        >
          {expanded ? 'Show less' : 'Show more'}
        </Button>
      )}
    </Box>
  );
}

export function ChangeHistoryContent({ items, loading, error }: Props) {
  return (
    <>
      {loading && (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 1 }}>
          <CircularProgress size={18} />
          <Typography variant="body2" color="text.secondary">Loading history…</Typography>
        </Box>
      )}
      {error && <Alert severity="error">{error}</Alert>}
      {items?.length === 0 && <Alert severity="info">No manual changes recorded yet.</Alert>}
      {items && items.length > 0 && (
        <Stack spacing={2} sx={{ width: '100%' }}>
          {items.map((item) => (
            <Box
              key={item.id}
              sx={{ border: 1, borderColor: 'divider', borderRadius: 1, overflow: 'hidden' }}
            >
              <Box
                sx={{
                  display: 'flex',
                  alignItems: { xs: 'flex-start', sm: 'center' },
                  justifyContent: 'space-between',
                  flexDirection: { xs: 'column', sm: 'row' },
                  gap: 1,
                  px: 2,
                  py: 1.5,
                  bgcolor: 'action.hover',
                }}
              >
                <Box sx={{ minWidth: 0 }}>
                  <Typography variant="subtitle2">{formatDate(item.changedAtUtc)}</Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>
                    {item.changedBy}
                  </Typography>
                </Box>
                <Stack direction="row" spacing={0.75} useFlexGap flexWrap="wrap">
                  {item.action.toLowerCase() !== 'updated' && (
                    <Chip label={item.action} size="small" variant="outlined" />
                  )}
                  <Chip label={formatSource(item.source)} size="small" variant="outlined" />
                  <Chip label={changesLabel(item.changes.length)} size="small" color="primary" />
                </Stack>
              </Box>

              <Box
                sx={{
                  display: { xs: 'none', md: 'grid' },
                  gridTemplateColumns: 'minmax(150px, 0.65fr) minmax(0, 1fr) minmax(0, 1fr)',
                  gap: 1.5,
                  px: 2,
                  py: 1,
                  borderTop: 1,
                  borderBottom: 1,
                  borderColor: 'divider',
                  bgcolor: 'background.default',
                }}
              >
                {['Field', 'Before', 'After'].map((label) => (
                  <Typography key={label} variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
                    {label}
                  </Typography>
                ))}
              </Box>

              <Stack divider={<Box sx={{ borderTop: 1, borderColor: 'divider' }} />}>
                {item.changes.map((change) => (
                  <Box
                    key={change.field}
                    sx={{
                      display: 'grid',
                      gridTemplateColumns: { xs: '1fr', md: 'minmax(150px, 0.65fr) minmax(0, 1fr) minmax(0, 1fr)' },
                      gap: { xs: 1, md: 1.5 },
                      px: 2,
                      py: 1.5,
                      alignItems: 'start',
                    }}
                  >
                    <Typography variant="body2" sx={{ fontWeight: 600, pt: { md: 1.25 } }}>
                      {change.field}
                    </Typography>
                    <Box>
                      <Typography variant="caption" color="text.secondary" sx={{ display: { md: 'none' } }}>
                        Before
                      </Typography>
                      <HistoryValue value={change.oldValue} kind="before" />
                    </Box>
                    <Box>
                      <Typography variant="caption" color="text.secondary" sx={{ display: { md: 'none' } }}>
                        After
                      </Typography>
                      <HistoryValue value={change.newValue} kind="after" />
                    </Box>
                  </Box>
                ))}
              </Stack>
            </Box>
          ))}
        </Stack>
      )}
    </>
  );
}
