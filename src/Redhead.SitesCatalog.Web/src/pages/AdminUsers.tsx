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
import type { UserListItem as UserListItemType } from '../types/adminUsers.types';
import { ROLES } from '../types/adminUsers.types';
import { ApiClientError } from '../services/api.client';

export const AdminUsers: React.FC = () => {
  const { user: currentUser } = useAuth();
  const [users, setUsers] = useState<UserListItemType[]>([]);
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

  const isAdmin = currentUser?.roles?.some((r) => r === 'Admin' || r === 'SuperAdmin');
  const isSuperAdmin = currentUser?.roles?.includes('SuperAdmin');
  const allowedRoles = isSuperAdmin ? ROLES : ROLES.filter((r) => r !== 'SuperAdmin');

  const loadUsers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await adminUsersService.list();
      setUsers(list);
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

  const canModifyUser = (targetRole: string): boolean => {
    if (isSuperAdmin) return true;
    return targetRole !== 'SuperAdmin' && targetRole !== 'Admin';
  };

  const copyPassword = () => {
    if (tempPasswordDialog) {
      void navigator.clipboard.writeText(tempPasswordDialog.password);
    }
  };

  if (!isAdmin) {
    return <Navigate to="/sites" replace />;
  }

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
                {isSuperAdmin && (
                  <TableCell align="right">Actions</TableCell>
                )}
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
                  {isSuperAdmin && (
                    <TableCell align="right">
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
    </PageShell>
  );
};
