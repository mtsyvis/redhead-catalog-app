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
import type { RoleSettingItem } from '../types/roleSettings.types';
import type { ExportLimitMode } from '../utils/exportLimit';

const ROLE_ORDER = ['SuperAdmin', 'Admin', 'Internal', 'Client'] as const;

type RoleLocalState = { mode: ExportLimitMode; rows: string };

function parsePositiveInt(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const n = Number.parseInt(trimmed, 10);
  if (Number.isNaN(n) || n <= 0 || String(n) !== trimmed) return null;
  return n;
}

export const RoleSettings: React.FC = () => {
  const { isAdmin } = useUserRoles();
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
      [role]: { mode, rows: mode === 'Limited' ? (prev[role]?.rows ?? '') : '' },
    }));
    setSuccess(null);
    setError(null);
  };

  const handleRowsChange = (role: string, value: string) => {
    setLocalValues((prev) => ({ ...prev, [role]: { ...prev[role], rows: value } }));
    setSuccess(null);
    setError(null);
  };

  const isEditableFor = (role: string) =>
    serverItems.find((r) => r.role === role)?.isEditable ?? true;

  const allValid = (): boolean =>
    ROLE_ORDER.every((role) => {
      if (!isEditableFor(role)) return true;
      const state = localValues[role];
      if (!state) return false;
      if (state.mode !== 'Limited') return true;
      return parsePositiveInt(state.rows) !== null;
    });

  const handleSave = async () => {
    if (!allValid()) {
      setError('Enter a positive number of rows for all Limited roles.');
      return;
    }
    setError(null);
    setSuccess(null);
    setSaveLoading(true);
    try {
      const payload = ROLE_ORDER.filter((role) => isEditableFor(role)).map((role) => ({
        role,
        exportLimitMode: localValues[role].mode,
        exportLimitRows:
          localValues[role].mode === 'Limited' ? parsePositiveInt(localValues[role].rows) : null,
      }));
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

  return (
    <PageShell title="Role Settings" maxWidth="md">
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Configure export access per role: Disabled, Limited by rows, or Unlimited.
      </Typography>

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
          <TableContainer component={Paper} variant="outlined" sx={{ mb: 2 }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Role</TableCell>
                  <TableCell>Export access</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {ROLE_ORDER.map((role) => {
                  const editable = isEditableFor(role);
                  const state = localValues[role] ?? { mode: 'Disabled' as ExportLimitMode, rows: '' };
                  const rowsInvalid =
                    state.mode === 'Limited' &&
                    state.rows !== '' &&
                    parsePositiveInt(state.rows) === null;

                  return (
                    <TableRow key={role}>
                      <TableCell>{role}</TableCell>
                      <TableCell>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
                          <FormControl size="small">
                            <Select
                              value={state.mode}
                              onChange={(e) =>
                                handleModeChange(role, e.target.value as ExportLimitMode)
                              }
                              disabled={!editable}
                              sx={{ minWidth: 130 }}
                            >
                              <MenuItem value="Disabled">Disabled</MenuItem>
                              <MenuItem value="Limited">Limited</MenuItem>
                              <MenuItem value="Unlimited">Unlimited</MenuItem>
                            </Select>
                            {!editable && (
                              <FormHelperText>Fixed system setting</FormHelperText>
                            )}
                          </FormControl>

                          {state.mode === 'Limited' && (
                            <TextField
                              type="number"
                              size="small"
                              value={state.rows}
                              onChange={(e) => handleRowsChange(role, e.target.value)}
                              slotProps={{ htmlInput: { min: 1, step: 1 } }}
                              error={rowsInvalid}
                              helperText={rowsInvalid ? 'Positive integer required' : undefined}
                              disabled={!editable}
                              sx={{ width: 140 }}
                              placeholder="Rows"
                            />
                          )}
                        </Box>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
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
        </>
      )}
    </PageShell>
  );
};
