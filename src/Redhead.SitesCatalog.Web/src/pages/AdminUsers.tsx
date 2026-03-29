import React, { useState, useCallback } from 'react';
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
  MenuItem,
  Select,
  FormControl,
  InputLabel,
  CircularProgress,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Chip,
} from '@mui/material';
import { PageShell } from '../components/layout/PageShell';
import { BrandButton } from '../components/common/BrandButton';
import { useAuth } from '../contexts/AuthContext';
import { adminUsersService } from '../services/adminUsers.service';
import { roleSettingsService } from '../services/roleSettings.service';
import type { UserListItem as UserListItemType } from '../types/adminUsers.types';
import { ROLES } from '../types/adminUsers.types';
import type { RoleSettingItem } from '../types/roleSettings.types';
import type { ExportLimitMode } from '../utils/exportLimit';
import { formatExportLimit } from '../utils/exportLimit';
import { ApiClientError } from '../services/api.client';

type ExportLimitOverrideOption = 'role-default' | ExportLimitMode;

function parsePositiveInt(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const n = Number.parseInt(trimmed, 10);
  if (Number.isNaN(n) || n <= 0 || String(n) !== trimmed) return null;
  return n;
}

export const AdminUsers: React.FC = () => {
  const { user: currentUser } = useAuth();
  const [users, setUsers] = useState<UserListItemType[]>([]);
  const [roleSettings, setRoleSettings] = useState<RoleSettingItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [createEmail, setCreateEmail] = useState('');
  const [createRole, setCreateRole] = useState<string>(ROLES[1]);
  const [createLoading, setCreateLoading] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const [tempPasswordDialog, setTempPasswordDialog] = useState<{
    title: string;
    email: string;
    password: string;
  } | null>(null);

  const [disableConfirmUser, setDisableConfirmUser] = useState<UserListItemType | null>(null);
  const [resetPasswordConfirmUser, setResetPasswordConfirmUser] = useState<UserListItemType | null>(null);
  const [actionLoadingId, setActionLoadingId] = useState<string | null>(null);

  const [editExportLimitUser, setEditExportLimitUser] = useState<UserListItemType | null>(null);
  const [exportLimitOption, setExportLimitOption] = useState<ExportLimitOverrideOption>('role-default');
  const [exportLimitRowsInput, setExportLimitRowsInput] = useState('');
  const [exportLimitSaveLoading, setExportLimitSaveLoading] = useState(false);
  const [exportLimitError, setExportLimitError] = useState<string | null>(null);

  const isAdmin = currentUser?.roles?.some((r) => r === 'Admin' || r === 'SuperAdmin');
  const isSuperAdmin = currentUser?.roles?.includes('SuperAdmin');
  const allowedRoles = isSuperAdmin ? ROLES : ROLES.filter((r) => r !== 'SuperAdmin');

  const loadUsers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [list, settings] = await Promise.all([
        adminUsersService.list(),
        roleSettingsService.list(),
      ]);
      setUsers(list);
      setRoleSettings(settings);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load users');
    } finally {
      setLoading(false);
    }
  }, []);

  React.useEffect(() => {
    loadUsers();
  }, [loadUsers]);

  const handleCreate = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setCreateError(null);
    setCreateLoading(true);
    try {
      const res = await adminUsersService.create({ email: createEmail, role: createRole });
      setCreateEmail('');
      setCreateRole(allowedRoles[0]);
      setTempPasswordDialog({
        title: 'User created',
        email: res.email,
        password: res.temporaryPassword,
      });
      await loadUsers();
    } catch (err) {
      setCreateError(err instanceof ApiClientError ? err.message : 'Failed to create user');
    } finally {
      setCreateLoading(false);
    }
  };

  const handleResetPasswordClick = (u: UserListItemType) => {
    setResetPasswordConfirmUser(u);
  };

  const handleResetPasswordConfirm = async () => {
    if (!resetPasswordConfirmUser) return;
    const user = resetPasswordConfirmUser;
    setActionLoadingId(user.id);
    try {
      const res = await adminUsersService.resetPassword(user.id);
      setResetPasswordConfirmUser(null);
      setTempPasswordDialog({
        title: 'Password reset',
        email: user.email,
        password: res.temporaryPassword,
      });
    } catch {
      setError('Failed to reset password');
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleDisableClick = (u: UserListItemType) => {
    setDisableConfirmUser(u);
  };

  const handleDisableConfirm = async () => {
    if (!disableConfirmUser) return;
    const userId = disableConfirmUser.id;
    setActionLoadingId(userId);
    try {
      await adminUsersService.disable(userId);
      setDisableConfirmUser(null);
      await loadUsers();
    } catch {
      setError('Failed to disable user');
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleOpenEditExportLimit = (u: UserListItemType) => {
    setEditExportLimitUser(u);
    if (u.exportLimitOverrideMode === null) {
      setExportLimitOption('role-default');
      setExportLimitRowsInput('');
    } else {
      setExportLimitOption(u.exportLimitOverrideMode);
      setExportLimitRowsInput(
        u.exportLimitOverrideMode === 'Limited' && u.exportLimitRowsOverride !== null
          ? String(u.exportLimitRowsOverride)
          : '',
      );
    }
    setExportLimitError(null);
  };

  const handleExportLimitOptionChange = (option: ExportLimitOverrideOption) => {
    setExportLimitOption(option);
    if (option !== 'Limited') setExportLimitRowsInput('');
    setExportLimitError(null);
  };

  const handleCloseExportLimit = () => {
    if (exportLimitSaveLoading) return;
    setEditExportLimitUser(null);
  };

  const handleSaveExportLimit = async () => {
    if (!editExportLimitUser) return;

    let overrideMode: ExportLimitMode | null = null;
    let overrideRows: number | null = null;

    if (exportLimitOption !== 'role-default') {
      overrideMode = exportLimitOption;
      if (exportLimitOption === 'Limited') {
        const parsed = parsePositiveInt(exportLimitRowsInput);
        if (parsed === null) {
          setExportLimitError('Enter a positive integer (no decimals, no zero).');
          return;
        }
        overrideRows = parsed;
      }
    }

    setExportLimitError(null);
    setExportLimitSaveLoading(true);
    try {
      await adminUsersService.updateExportLimit(editExportLimitUser.id, { overrideMode, overrideRows });
      setEditExportLimitUser(null);
      await loadUsers();
    } catch (err) {
      setExportLimitError(
        err instanceof ApiClientError ? err.message : 'Failed to save export limit.',
      );
    } finally {
      setExportLimitSaveLoading(false);
    }
  };

  const canModifyUser = (targetRole: string): boolean => {
    if (isSuperAdmin) return true;
    return targetRole !== 'SuperAdmin' && targetRole !== 'Admin';
  };

  const copyPassword = () => {
    if (tempPasswordDialog) {
      void navigator.clipboard.writeText(tempPasswordDialog.password);
    }
  };

  const getExportLimitPreview = (): string | null => {
    if (!editExportLimitUser) return null;
    if (exportLimitOption === 'role-default') {
      const roleDefault = roleSettings.find((r) => r.role === editExportLimitUser.role);
      if (!roleDefault) return null;
      return `Effective export limit: ${formatExportLimit(roleDefault.exportLimitMode, roleDefault.exportLimitRows)}`;
    }
    if (exportLimitOption === 'Disabled') return 'Effective export limit: Disabled';
    if (exportLimitOption === 'Unlimited') return 'Effective export limit: Unlimited';
    if (exportLimitOption === 'Limited') {
      const parsed = parsePositiveInt(exportLimitRowsInput);
      if (parsed === null) return null;
      return `Effective export limit: ${parsed} rows`;
    }
    return null;
  };

  const getRoleDefaultText = (role: string): string => {
    const item = roleSettings.find((r) => r.role === role);
    if (!item) return '—';
    return `Role default: ${formatExportLimit(item.exportLimitMode, item.exportLimitRows)}`;
  };

  if (!isAdmin) {
    return <Navigate to="/sites" replace />;
  }

  const exportLimitPreview = getExportLimitPreview();

  return (
    <PageShell title="Users" maxWidth="lg">
      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <Paper sx={{ p: 3, mb: 3 }}>
        <Typography variant="h6" gutterBottom>
          Create user
        </Typography>
        <form onSubmit={handleCreate}>
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2, alignItems: 'flex-start' }}>
            <TextField
              label="Email"
              type="email"
              required
              value={createEmail}
              onChange={(e) => setCreateEmail(e.target.value)}
              disabled={createLoading}
              sx={{ minWidth: 260 }}
              autoComplete="off"
            />
            <FormControl sx={{ minWidth: 160 }} disabled={createLoading}>
              <InputLabel>Role</InputLabel>
              <Select
                value={createRole}
                label="Role"
                onChange={(e) => setCreateRole(e.target.value)}
              >
                {allowedRoles.map((r) => (
                  <MenuItem key={r} value={r}>
                    {r}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <BrandButton type="submit" disabled={createLoading}>
              {createLoading ? <CircularProgress size={24} color="inherit" /> : 'Create'}
            </BrandButton>
          </Box>
          {createError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {createError}
            </Alert>
          )}
        </form>
      </Paper>

      <Typography variant="h6" sx={{ mb: 2 }}>
        All users
      </Typography>
      {loading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Email</TableCell>
                <TableCell>Role</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Export limit</TableCell>
                {isSuperAdmin && <TableCell align="right">Actions</TableCell>}
              </TableRow>
            </TableHead>
            <TableBody>
              {users.map((u) => (
                <TableRow key={u.id}>
                  <TableCell>{u.email}</TableCell>
                  <TableCell>{u.role}</TableCell>
                  <TableCell>
                    <Chip
                      label={u.isActive ? 'Active' : 'Disabled'}
                      color={u.isActive ? 'success' : 'default'}
                      size="small"
                    />
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">
                      {formatExportLimit(u.effectiveExportLimitMode, u.effectiveExportLimitRows)}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {!u.isExportLimitEditable
                        ? 'Fixed for SuperAdmin'
                        : u.isExportLimitOverridden
                          ? 'Personal override'
                          : 'Inherited from role'}
                    </Typography>
                  </TableCell>
                  {isSuperAdmin && (
                    <TableCell align="right">
                      <Box sx={{ display: 'inline-flex', gap: 1, justifyContent: 'flex-end' }}>
                        {u.isActive && canModifyUser(u.role) && (
                          <>
                            <BrandButton
                              kind="outline"
                              size="small"
                              onClick={() => handleResetPasswordClick(u)}
                              disabled={actionLoadingId === u.id}
                            >
                              {actionLoadingId === u.id ? (
                                <CircularProgress size={18} />
                              ) : (
                                'Reset password'
                              )}
                            </BrandButton>
                            <BrandButton
                              kind="outline"
                              size="small"
                              onClick={() => handleDisableClick(u)}
                              disabled={actionLoadingId === u.id}
                            >
                              Disable
                            </BrandButton>
                          </>
                        )}
                        {u.isActive && u.isExportLimitEditable && (
                          <BrandButton
                            kind="outline"
                            size="small"
                            onClick={() => handleOpenEditExportLimit(u)}
                            disabled={actionLoadingId === u.id}
                          >
                            Edit export limit
                          </BrandButton>
                        )}
                      </Box>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog
        open={!!resetPasswordConfirmUser}
        onClose={() => !actionLoadingId && setResetPasswordConfirmUser(null)}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle>Reset password</DialogTitle>
        <DialogContent>
          {resetPasswordConfirmUser && (
            <>
              <Typography sx={{ mb: 1 }}>
                Reset password for <strong>{resetPasswordConfirmUser.email}</strong>?
              </Typography>
              <Typography variant="body2" color="text.secondary">
                A new temporary password will be generated and shown only once. Copy it and share it
                securely with the user. They will be required to change it on their next login.
              </Typography>
            </>
          )}
        </DialogContent>
        <DialogActions>
          <BrandButton
            kind="outline"
            onClick={() => setResetPasswordConfirmUser(null)}
            disabled={!!actionLoadingId}
          >
            Cancel
          </BrandButton>
          <BrandButton
            kind="primary"
            onClick={handleResetPasswordConfirm}
            disabled={!!actionLoadingId || !resetPasswordConfirmUser}
          >
            {actionLoadingId && resetPasswordConfirmUser ? (
              <CircularProgress size={24} color="inherit" />
            ) : (
              'Reset password'
            )}
          </BrandButton>
        </DialogActions>
      </Dialog>

      <Dialog
        open={!!disableConfirmUser}
        onClose={() => !actionLoadingId && setDisableConfirmUser(null)}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle>Disable user</DialogTitle>
        <DialogContent>
          {disableConfirmUser && (
            <>
              <Typography sx={{ mb: 1 }}>
                Disable user <strong>{disableConfirmUser.email}</strong>?
              </Typography>
              <Typography variant="body2" color="text.secondary">
                This user will no longer be able to sign in or access the application. This action
                cannot be reverted.
              </Typography>
            </>
          )}
        </DialogContent>
        <DialogActions>
          <BrandButton kind="outline" onClick={() => setDisableConfirmUser(null)} disabled={!!actionLoadingId}>
            Cancel
          </BrandButton>
          <BrandButton
            kind="primary"
            onClick={handleDisableConfirm}
            disabled={!!actionLoadingId || !disableConfirmUser}
          >
            {actionLoadingId && disableConfirmUser ? (
              <CircularProgress size={24} color="inherit" />
            ) : (
              'Disable'
            )}
          </BrandButton>
        </DialogActions>
      </Dialog>

      <Dialog open={!!tempPasswordDialog} onClose={() => setTempPasswordDialog(null)} maxWidth="sm" fullWidth>
        <DialogTitle>{tempPasswordDialog?.title}</DialogTitle>
        <DialogContent>
          {tempPasswordDialog && (
            <>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                {tempPasswordDialog.email}
              </Typography>
              <Alert severity="warning" sx={{ mt: 1 }}>
                This password is shown only once. Copy it now and share it securely with the user.
              </Alert>
              <Box
                sx={{
                  mt: 2,
                  p: 2,
                  bgcolor: 'grey.100',
                  borderRadius: 1,
                  fontFamily: 'monospace',
                  wordBreak: 'break-all',
                }}
              >
                {tempPasswordDialog.password}
              </Box>
            </>
          )}
        </DialogContent>
        <DialogActions>
          <BrandButton kind="outline" onClick={copyPassword}>
            Copy password
          </BrandButton>
          <BrandButton onClick={() => setTempPasswordDialog(null)}>Done</BrandButton>
        </DialogActions>
      </Dialog>

      <Dialog
        open={!!editExportLimitUser}
        onClose={handleCloseExportLimit}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle>Edit export limit</DialogTitle>
        <DialogContent>
          {editExportLimitUser && (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
              <Box>
                <Typography variant="body2">
                  <strong>{editExportLimitUser.email}</strong> · {editExportLimitUser.role}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {getRoleDefaultText(editExportLimitUser.role)}
                </Typography>
              </Box>

              <FormControl fullWidth>
                <InputLabel>Override</InputLabel>
                <Select
                  value={exportLimitOption}
                  label="Override"
                  onChange={(e) =>
                    handleExportLimitOptionChange(e.target.value as ExportLimitOverrideOption)
                  }
                  disabled={exportLimitSaveLoading}
                >
                  <MenuItem value="role-default">Use role default</MenuItem>
                  <MenuItem value="Disabled">Disabled</MenuItem>
                  <MenuItem value="Limited">Limited</MenuItem>
                  <MenuItem value="Unlimited">Unlimited</MenuItem>
                </Select>
              </FormControl>

              {exportLimitOption === 'Limited' && (
                <TextField
                  label="Rows"
                  type="number"
                  value={exportLimitRowsInput}
                  onChange={(e) => {
                    setExportLimitRowsInput(e.target.value);
                    setExportLimitError(null);
                  }}
                  slotProps={{ htmlInput: { min: 1, step: 1 } }}
                  disabled={exportLimitSaveLoading}
                  required
                  fullWidth
                />
              )}

              {exportLimitPreview && (
                <Typography variant="body2" color="text.secondary">
                  {exportLimitPreview}
                </Typography>
              )}

              {exportLimitError && (
                <Alert severity="error">{exportLimitError}</Alert>
              )}
            </Box>
          )}
        </DialogContent>
        <DialogActions>
          <BrandButton kind="outline" onClick={handleCloseExportLimit} disabled={exportLimitSaveLoading}>
            Cancel
          </BrandButton>
          <BrandButton
            onClick={handleSaveExportLimit}
            disabled={exportLimitSaveLoading || !editExportLimitUser}
          >
            {exportLimitSaveLoading ? <CircularProgress size={24} color="inherit" /> : 'Save'}
          </BrandButton>
        </DialogActions>
      </Dialog>
    </PageShell>
  );
};
