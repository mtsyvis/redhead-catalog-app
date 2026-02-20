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
} from '@mui/material';
import { PageShell } from '../components/layout/PageShell';
import { BrandButton } from '../components/common/BrandButton';
import { useAuth } from '../contexts/AuthContext';
import { roleSettingsService } from '../services/roleSettings.service';
import { ApiClientError } from '../services/api.client';

const ROLE_ORDER = ['SuperAdmin', 'Admin', 'Internal', 'Client'] as const;

export const RoleSettings: React.FC = () => {
  const { user } = useAuth();
  const [loading, setLoading] = useState(true);
  const [saveLoading, setSaveLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [localValues, setLocalValues] = useState<Record<string, string>>({});

  const isAdmin = user?.roles?.some((r) => r === 'Admin' || r === 'SuperAdmin');

  const loadSettings = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await roleSettingsService.list();
      const initial: Record<string, string> = {};
      list.forEach((row) => {
        initial[row.role] = String(row.exportLimitRows);
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

  const handleExportLimitChange = (role: string, value: string) => {
    setLocalValues((prev) => ({ ...prev, [role]: value }));
    setSuccess(null);
    setError(null);
  };

  const validateRow = (role: string): number | null => {
    const raw = localValues[role]?.trim() ?? '';
    if (raw === '') return null;
    const n = Number.parseInt(raw, 10);
    if (Number.isNaN(n) || n < 0 || String(n) !== raw) return null;
    return n;
  };

  const allValid = (): boolean => {
    return ROLE_ORDER.every((role) => validateRow(role) !== null);
  };

  const handleSave = async () => {
    if (!allValid()) {
      setError('Export limit must be an integer ≥ 0 for all roles.');
      return;
    }
    setError(null);
    setSuccess(null);
    setSaveLoading(true);
    try {
      const payload = ROLE_ORDER.map((role) => ({
        role,
        exportLimitRows: validateRow(role) as number,
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
        0 = export disabled for that role. A large number = effectively unlimited.
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
                  <TableCell align="right">Export limit (rows)</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {ROLE_ORDER.map((role) => {
                  const value = localValues[role] ?? '';
                  const parsed = Number.parseInt(value, 10);
                  const invalid =
                    value !== '' &&
                    (Number.isNaN(parsed) || parsed < 0);
                  return (
                    <TableRow key={role}>
                      <TableCell>{role}</TableCell>
                      <TableCell align="right">
                        <TextField
                          type="number"
                          size="small"
                          value={value}
                          onChange={(e) =>
                            handleExportLimitChange(role, e.target.value)
                          }
                          slotProps={{ htmlInput: { min: 0, step: 1 } }}
                          error={invalid}
                          sx={{ width: 140 }}
                        />
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
          <Box>
            <BrandButton
              onClick={handleSave}
              disabled={saveLoading || !allValid()}
            >
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
