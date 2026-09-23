import React, { useState, useCallback, useEffect } from 'react';
import {
  Box,
  Chip,
  Paper,
  Typography,
  TextField,
  Alert,
  CircularProgress,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
} from '@mui/material';
import { BrandButton } from '../components/common/BrandButton';
import { useUserRoles } from '../hooks/useUserRoles';
import { roleSettingsService } from '../services/roleSettings.service';
import { ApiClientError } from '../services/api.client';
import type { RoleSettingItem, RoleSettingUpdateItem } from '../types/roleSettings.types';
import type { ExportLimitMode } from '../utils/exportLimit';

const ROLE_ORDER = ['SuperAdmin', 'Admin', 'Editor', 'Linkbuilder', 'Internal', 'Client', 'Lite'] as const;
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

export const RoleSettingsPanel: React.FC = () => {
  const { canReadRoleSettings, canManageRoleSettings } = useUserRoles();
  const canEditRoleSettings = canManageRoleSettings;
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

  const hasChanges = canEditRoleSettings && serverItems.some((serverItem) => {
    if (!serverItem.isEditable) return false;

    const local = localValues[serverItem.role];
    if (!local || local.mode !== serverItem.exportLimitMode) return true;
    if (
      local.mode === 'Limited' &&
      parsePositiveInt(local.rows) !== serverItem.exportLimitRows
    ) {
      return true;
    }
    if (serverItem.role !== CLIENT_ROLE) return false;

    return CLIENT_USAGE_LIMIT_FIELDS.some(
      (field) =>
        parsePositiveInt(local.clientUsageLimits[field.key]) !== serverItem[field.key]
    );
  });

  const handleSave = async () => {
    if (!canEditRoleSettings || !hasChanges) return;
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
      const updatesByRole = new Map(payload.map((item) => [item.role, item]));
      setServerItems((items) => items.map((item) => {
        const update = updatesByRole.get(item.role);
        if (!update) return item;

        return {
          ...item,
          exportLimitMode: update.exportLimitMode,
          exportLimitRows: update.exportLimitRows,
          dailyUniqueExportedDomainsLimit:
            update.dailyUniqueExportedDomainsLimit ?? item.dailyUniqueExportedDomainsLimit,
          weeklyUniqueExportedDomainsLimit:
            update.weeklyUniqueExportedDomainsLimit ?? item.weeklyUniqueExportedDomainsLimit,
          dailyExportOperationsLimit:
            update.dailyExportOperationsLimit ?? item.dailyExportOperationsLimit,
          weeklyExportOperationsLimit:
            update.weeklyExportOperationsLimit ?? item.weeklyExportOperationsLimit,
        };
      }));
      setSuccess('Role policies saved.');
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Failed to save.');
    } finally {
      setSaveLoading(false);
    }
  };

  if (!canReadRoleSettings) return null;

  const clientState = localValues[CLIENT_ROLE] ?? {
    mode: 'Limited' as ExportLimitMode,
    rows: '',
    clientUsageLimits: toClientUsageLimitLocalState(),
  };
  const clientUsageEditable = isEditableFor(CLIENT_ROLE);

  return (
    <>
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          gap: 2,
          flexWrap: 'wrap',
          mb: 2,
        }}
      >
        <Box>
          <Typography variant="h6">Role export policies</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            Configure export access per role. Row limits apply to each individual export.
          </Typography>
        </Box>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Chip label="Database" variant="outlined" size="small" />
          <Typography variant="caption" color="text.secondary">
            Applies immediately
          </Typography>
        </Box>
      </Box>

      {canReadRoleSettings && !canEditRoleSettings && (
        <Alert severity="info" sx={{ mb: 2 }}>
          View only. Only a Super Admin can change these settings.
        </Alert>
      )}

      {error && (
        <Alert
          severity="error"
          sx={{ mb: 2 }}
          onClose={() => setError(null)}
          action={serverItems.length === 0 ? (
            <BrandButton kind="outline" size="small" onClick={() => void loadSettings()}>
              Retry
            </BrandButton>
          ) : undefined}
        >
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
      ) : serverItems.length > 0 ? (
        <>
          <Box sx={{ mb: 2 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
              Configurable roles
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
              Choose whether each role can export and set a per-file row cap when access is limited.
            </Typography>
          </Box>

          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', md: 'repeat(3, minmax(0, 1fr))' },
              gap: 2,
              mb: 2,
            }}
          >
            {ROLE_ORDER.filter(isRowEditableFromApi).map((role) => {
              const editable = isEditableFor(role);
              const state = localValues[role] ?? {
                mode: 'Disabled' as ExportLimitMode,
                rows: '',
                clientUsageLimits: toClientUsageLimitLocalState(),
              };
              const rowsInvalid =
                state.mode === 'Limited' && parsePositiveInt(state.rows) === null;

              return (
                <Paper key={role} variant="outlined" sx={{ p: 2.25 }}>
                  <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.75 }}>
                    {role}
                  </Typography>
                  <Box
                    sx={{
                      display: 'grid',
                      gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', md: '1fr' },
                      gap: 1.5,
                    }}
                  >
                    <FormControl size="small" fullWidth>
                      <InputLabel id={`${role}-export-access-label`}>Export access</InputLabel>
                      <Select
                        labelId={`${role}-export-access-label`}
                        label="Export access"
                        value={state.mode}
                        onChange={(e) =>
                          handleModeChange(role, e.target.value as ExportLimitMode)
                        }
                        disabled={!editable}
                      >
                        <MenuItem value="Disabled">Disabled</MenuItem>
                        <MenuItem value="Limited">Limited</MenuItem>
                        <MenuItem value="Unlimited">Unlimited</MenuItem>
                      </Select>
                    </FormControl>

                    {state.mode === 'Limited' ? (
                      <TextField
                        label="Rows per export"
                        type="number"
                        size="small"
                        value={state.rows}
                        onChange={(e) => handleRowsChange(role, e.target.value)}
                        slotProps={{ htmlInput: { min: 1, step: 1 } }}
                        error={rowsInvalid}
                        helperText={rowsInvalid ? 'Positive integer required' : 'Maximum rows per file'}
                        disabled={!editable}
                        fullWidth
                      />
                    ) : (
                      <Box
                        sx={{
                          minHeight: 40,
                          px: 1.75,
                          py: 1,
                          borderRadius: 1,
                          bgcolor: 'action.hover',
                        }}
                      >
                        <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                          Rows per export
                        </Typography>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                          {state.mode === 'Unlimited' ? 'No row cap' : 'Not available'}
                        </Typography>
                      </Box>
                    )}
                  </Box>
                </Paper>
              );
            })}
          </Box>

          <Paper variant="outlined" sx={{ mb: 2, p: 2.25 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
              Fixed system roles
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25, mb: 1.75 }}>
              These export policies are built into the application and cannot be changed here.
            </Typography>
            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: {
                  xs: '1fr',
                  sm: 'repeat(2, minmax(0, 1fr))',
                  lg: 'repeat(4, minmax(0, 1fr))',
                },
                gap: 1.25,
              }}
            >
              {ROLE_ORDER.filter((role) => !isRowEditableFromApi(role)).map((role) => {
                const state = localValues[role] ?? {
                  mode: 'Disabled' as ExportLimitMode,
                  rows: '',
                  clientUsageLimits: toClientUsageLimitLocalState(),
                };

                return (
                  <Box
                    key={role}
                    sx={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      gap: 1,
                      px: 1.5,
                      py: 1.25,
                      border: 1,
                      borderColor: 'divider',
                      borderRadius: 1,
                    }}
                  >
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {role}
                    </Typography>
                    <Chip
                      label={state.mode}
                      size="small"
                      color={state.mode === 'Unlimited' ? 'success' : 'default'}
                      variant="outlined"
                    />
                  </Box>
                );
              })}
            </Box>
          </Paper>

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
                <Typography variant="h6">Client export quotas</Typography>
                <Typography variant="body2" color="text.secondary">
                  Daily and weekly quotas for the Client role. Unique-domain quotas count new
                  domains; export-operation quotas count successful and partial exports.
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
                const invalid = parsePositiveInt(value) === null;

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
              <BrandButton
                onClick={handleSave}
                disabled={saveLoading || !hasChanges || !allValid()}
              >
                {saveLoading ? (
                  <>
                    <CircularProgress size={20} sx={{ mr: 1 }} color="inherit" />
                    Saving…
                  </>
                ) : (
                  'Save role policies'
                )}
              </BrandButton>
            </Box>
          )}
        </>
      ) : null}
    </>
  );
};
