import React, { useState, useCallback, useEffect } from 'react';
import { Navigate } from 'react-router-dom';
import {
  Box,
  Paper,
  Typography,
  TextField,
  Alert,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  CircularProgress,
  Select,
  MenuItem,
  FormControl,
  FormHelperText,
} from '@mui/material';
import { PageShell } from '../components/layout/PageShell';
import { BrandButton } from '../components/common/BrandButton';
import { useUserRoles } from '../hooks/useUserRoles';
import { roleSettingsService } from '../services/roleSettings.service';
import { ApiClientError } from '../services/api.client';
import type { RoleSettingItem, RoleSettingUpdateItem } from '../types/roleSettings.types';
import type { ExportLimitMode } from '../utils/exportLimit';

const ROLE_ORDER = ['SuperAdmin', 'Admin', 'Internal', 'Client', 'Lite'] as const;
const CLIENT_ROLE = 'Client';
const DEFAULT_CLIENT_USAGE_LIMITS: ClientUsageLimitLocalState = {
  dailyUniqueExportedDomainsLimit: '1000',
  weeklyUniqueExportedDomainsLimit: '3000',
  dailyExportOperationsLimit: '20',
  weeklyExportOperationsLimit: '60',
};

type ClientUsageLimitLocalState = {
  dailyUniqueExportedDomainsLimit: string;
  weeklyUniqueExportedDomainsLimit: string;
  dailyExportOperationsLimit: string;
  weeklyExportOperationsLimit: string;
};

type ClientUsageLimitField = keyof ClientUsageLimitLocalState;

type RoleLocalState = {
  mode: ExportLimitMode;
  rows: string;
  clientUsageLimits: ClientUsageLimitLocalState;
};

const CLIENT_USAGE_LIMIT_FIELDS: Array<{
  key: ClientUsageLimitField;
  label: string;
  helperText: string;
}> = [
  {
    key: 'dailyUniqueExportedDomainsLimit',
    label: 'Daily unique exported domains',
    helperText: 'Last 24 hours',
  },
  {
    key: 'weeklyUniqueExportedDomainsLimit',
    label: 'Weekly unique exported domains',
    helperText: 'Last 7 days',
  },
  {
    key: 'dailyExportOperationsLimit',
    label: 'Daily export operations',
    helperText: 'Last 24 hours',
  },
  {
    key: 'weeklyExportOperationsLimit',
    label: 'Weekly export operations',
    helperText: 'Last 7 days',
  },
];

function parsePositiveInt(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const n = Number.parseInt(trimmed, 10);
  if (Number.isNaN(n) || n <= 0 || String(n) !== trimmed) return null;
  return n;
}

function toClientUsageLimitLocalState(row?: RoleSettingItem): ClientUsageLimitLocalState {
  return {
    dailyUniqueExportedDomainsLimit:
      row?.dailyUniqueExportedDomainsLimit != null ? String(row.dailyUniqueExportedDomainsLimit) : '',
    weeklyUniqueExportedDomainsLimit:
      row?.weeklyUniqueExportedDomainsLimit != null ? String(row.weeklyUniqueExportedDomainsLimit) : '',
    dailyExportOperationsLimit:
      row?.dailyExportOperationsLimit != null ? String(row.dailyExportOperationsLimit) : '',
    weeklyExportOperationsLimit:
      row?.weeklyExportOperationsLimit != null ? String(row.weeklyExportOperationsLimit) : '',
  };
}

export const RoleSettings: React.FC = () => {
  const { isAdmin, isSuperAdmin } = useUserRoles();
  const canEditRoleSettings = isSuperAdmin;
  const [loading, setLoading] = useState(true);
  const [saveLoading, setSaveLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [serverItems, setServerItems] = useState<RoleSettingItem[]>([]);
  const [localValues, setLocalValues] = useState<Record<string, RoleLocalState>>({});

  const loadSettings = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await roleSettingsService.list();
      setServerItems(list);
      const initial: Record<string, RoleLocalState> = {};
      list.forEach((row) => {
        initial[row.role] = {
          mode: row.exportLimitMode,
          rows: row.exportLimitRows !== null ? String(row.exportLimitRows) : '',
          clientUsageLimits: toClientUsageLimitLocalState(row),
        };
      });
      setLocalValues(initial);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load role settings');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadSettings();
  }, [loadSettings]);

  const handleModeChange = (role: string, mode: ExportLimitMode) => {
    setLocalValues((prev) => ({
      ...prev,
      [role]: {
        mode,
        rows: mode === 'Limited' ? (prev[role]?.rows ?? '') : '',
        clientUsageLimits: prev[role]?.clientUsageLimits ?? toClientUsageLimitLocalState(),
      },
    }));
    setSuccess(null);
    setError(null);
  };

  const handleRowsChange = (role: string, value: string) => {
    setLocalValues((prev) => ({ ...prev, [role]: { ...prev[role], rows: value } }));
    setSuccess(null);
    setError(null);
  };

  const handleClientUsageLimitChange = (
    role: string,
    field: ClientUsageLimitField,
    value: string
  ) => {
    setLocalValues((prev) => ({
      ...prev,
      [role]: {
        ...prev[role],
        clientUsageLimits: {
          ...(prev[role]?.clientUsageLimits ?? toClientUsageLimitLocalState()),
          [field]: value,
        },
      },
    }));
    setSuccess(null);
    setError(null);
  };

  const handleResetClientUsageDefaults = () => {
    setLocalValues((prev) => ({
      ...prev,
      [CLIENT_ROLE]: {
        ...(prev[CLIENT_ROLE] ?? {
          mode: 'Limited' as ExportLimitMode,
          rows: '',
          clientUsageLimits: toClientUsageLimitLocalState(),
        }),
        clientUsageLimits: { ...DEFAULT_CLIENT_USAGE_LIMITS },
      },
    }));
    setSuccess(null);
    setError(null);
  };

  const isRowEditableFromApi = (role: string) =>
    serverItems.find((r) => r.role === role)?.isEditable ?? true;

  const isEditableFor = (role: string) => isRowEditableFromApi(role) && canEditRoleSettings;

  const allValid = (): boolean =>
    ROLE_ORDER.every((role) => {
      if (!isRowEditableFromApi(role) || !canEditRoleSettings) return true;
      const state = localValues[role];
      if (!state) return false;
      const rowsValid = state.mode !== 'Limited' || parsePositiveInt(state.rows) !== null;
      const clientUsageLimitsValid =
        role !== CLIENT_ROLE ||
        CLIENT_USAGE_LIMIT_FIELDS.every(
          (field) => parsePositiveInt(state.clientUsageLimits[field.key]) !== null
        );

      return rowsValid && clientUsageLimitsValid;
    });

  const handleSave = async () => {
    if (!canEditRoleSettings) return;
    if (!allValid()) {
      setError('Enter positive integers for all Limited rows and Client usage limits.');
      return;
    }
    setError(null);
    setSuccess(null);
    setSaveLoading(true);
    try {
      const payload: RoleSettingUpdateItem[] = ROLE_ORDER.filter((role) =>
        isRowEditableFromApi(role)
      ).map((role) => {
        const state = localValues[role];
        const item: RoleSettingUpdateItem = {
          role,
          exportLimitMode: state.mode,
          exportLimitRows: state.mode === 'Limited' ? parsePositiveInt(state.rows) : null,
        };

        if (role !== CLIENT_ROLE) {
          return item;
        }

        return {
          ...item,
          dailyUniqueExportedDomainsLimit: parsePositiveInt(
            state.clientUsageLimits.dailyUniqueExportedDomainsLimit
          ),
          weeklyUniqueExportedDomainsLimit: parsePositiveInt(
            state.clientUsageLimits.weeklyUniqueExportedDomainsLimit
          ),
          dailyExportOperationsLimit: parsePositiveInt(
            state.clientUsageLimits.dailyExportOperationsLimit
          ),
          weeklyExportOperationsLimit: parsePositiveInt(
            state.clientUsageLimits.weeklyExportOperationsLimit
          ),
        };
      });
      await roleSettingsService.update(payload);
      setSuccess('Role settings saved.');
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Failed to save.');
    } finally {
      setSaveLoading(false);
    }
  };

  if (!isAdmin) {
    return <Navigate to="/sites" replace />;
  }

  const clientState = localValues[CLIENT_ROLE] ?? {
    mode: 'Limited' as ExportLimitMode,
    rows: '',
    clientUsageLimits: toClientUsageLimitLocalState(),
  };
  const clientUsageEditable = isEditableFor(CLIENT_ROLE);

  return (
    <PageShell title="Role Settings" maxWidth="lg">
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Configure export access per role. Row limits apply to each individual export.
      </Typography>

      {isAdmin && !canEditRoleSettings && (
        <Alert severity="info" sx={{ mb: 2 }}>
          View only. Only a Super Admin can change these settings.
        </Alert>
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}
      {success && (
        <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccess(null)}>
          {success}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        <>
          <Box sx={{ mb: 1 }}>
            <Typography variant="subtitle2">Rows per export</Typography>
            <Typography variant="body2" color="text.secondary">
              Limited caps how many matching site rows a user can export in one export file.
              Daily and weekly Client usage limits are configured below.
            </Typography>
          </Box>
          <TableContainer component={Paper} variant="outlined" sx={{ mb: 2 }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Role</TableCell>
                  <TableCell>Export access</TableCell>
                  <TableCell>Rows per export</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {ROLE_ORDER.map((role) => {
                  const editable = isEditableFor(role);
                  const state = localValues[role] ?? {
                    mode: 'Disabled' as ExportLimitMode,
                    rows: '',
                    clientUsageLimits: toClientUsageLimitLocalState(),
                  };
                  const rowsInvalid =
                    state.mode === 'Limited' &&
                    state.rows !== '' &&
                    parsePositiveInt(state.rows) === null;

                  return (
                    <TableRow key={role}>
                      <TableCell sx={{ width: 124 }}>{role}</TableCell>
                      <TableCell>
                        <FormControl size="small">
                          <Select
                            value={state.mode}
                            onChange={(e) =>
                              handleModeChange(role, e.target.value as ExportLimitMode)
                            }
                            disabled={!editable}
                            sx={{ minWidth: 146 }}
                          >
                            <MenuItem value="Disabled">Disabled</MenuItem>
                            <MenuItem value="Limited">Limited</MenuItem>
                            <MenuItem value="Unlimited">Unlimited</MenuItem>
                          </Select>
                          {!isRowEditableFromApi(role) && (
                            <FormHelperText>Fixed system setting</FormHelperText>
                          )}
                        </FormControl>
                      </TableCell>
                      <TableCell>
                        {state.mode === 'Limited' ? (
                          <TextField
                            type="number"
                            size="small"
                            value={state.rows}
                            onChange={(e) => handleRowsChange(role, e.target.value)}
                            slotProps={{ htmlInput: { min: 1, step: 1 } }}
                            error={rowsInvalid}
                            helperText={rowsInvalid ? 'Positive integer required' : undefined}
                            disabled={!editable}
                            sx={{ width: 160 }}
                            placeholder="Rows"
                          />
                        ) : (
                          <Typography variant="body2" color="text.secondary">
                            -
                          </Typography>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>

          <Paper variant="outlined" sx={{ mb: 2, p: 2 }}>
            <Box
              sx={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: { xs: 'flex-start', sm: 'center' },
                gap: 1.5,
                mb: 2,
                flexDirection: { xs: 'column', sm: 'row' },
              }}
            >
              <Box>
                <Typography variant="h6">Client usage limits</Typography>
                <Typography variant="body2" color="text.secondary">
                  Applies to Client role only. Unique-domain limits count new domains. Export
                  operations count successful and partial exports.
                </Typography>
              </Box>
              {canEditRoleSettings && (
                <BrandButton
                  kind="outline"
                  size="small"
                  onClick={handleResetClientUsageDefaults}
                  disabled={saveLoading || !clientUsageEditable}
                  sx={{ flexShrink: 0 }}
                >
                  Reset defaults
                </BrandButton>
              )}
            </Box>

            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', md: 'repeat(4, 1fr)' },
                gap: 2,
              }}
            >
              {CLIENT_USAGE_LIMIT_FIELDS.map((field) => {
                const value = clientState.clientUsageLimits[field.key];
                const invalid = value !== '' && parsePositiveInt(value) === null;

                return (
                  <TextField
                    key={field.key}
                    label={field.label}
                    type="number"
                    size="small"
                    value={value}
                    onChange={(e) =>
                      handleClientUsageLimitChange(CLIENT_ROLE, field.key, e.target.value)
                    }
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    error={invalid}
                    helperText={invalid ? 'Positive integer required' : field.helperText}
                    disabled={!clientUsageEditable}
                    required={clientUsageEditable}
                    fullWidth
                  />
                );
              })}
            </Box>
          </Paper>

          {canEditRoleSettings && (
            <Box>
              <BrandButton onClick={handleSave} disabled={saveLoading || !allValid()}>
                {saveLoading ? (
                  <>
                    <CircularProgress size={20} sx={{ mr: 1 }} color="inherit" />
                    Saving…
                  </>
                ) : (
                  'Save'
                )}
              </BrandButton>
            </Box>
          )}
        </>
      )}
    </PageShell>
  );
};
