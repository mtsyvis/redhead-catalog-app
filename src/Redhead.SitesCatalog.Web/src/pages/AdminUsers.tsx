import React, { useState, useCallback, useMemo } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import {
  Box,
  Paper,
  Typography,
  TextField,
  Alert,
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
  IconButton,
  Menu,
  Tooltip,
  ToggleButton,
  ToggleButtonGroup,
} from '@mui/material';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import { DataGrid } from '@mui/x-data-grid';
import type { GridColDef, GridPaginationModel, GridRowParams } from '@mui/x-data-grid';
import { PageShell } from '../components/layout/PageShell';
import { BrandButton } from '../components/common/BrandButton';
import { useAuth } from '../contexts/AuthContext';
import { adminUsersService } from '../services/adminUsers.service';
import { roleSettingsService } from '../services/roleSettings.service';
import type { UserListItem as UserListItemType, UserTypeFilter } from '../types/adminUsers.types';
import { ROLES } from '../types/adminUsers.types';
import type { RoleSettingItem } from '../types/roleSettings.types';
import type { ExportLimitMode } from '../utils/exportLimit';
import { formatExportLimit } from '../utils/exportLimit';
import { ApiClientError } from '../services/api.client';

type ExportLimitOverrideOption = 'role-default' | ExportLimitMode;

const USER_TYPE_OPTIONS: Array<{ value: UserTypeFilter; label: string; emptyMessage: string }> = [
  { value: 'all', label: 'All users', emptyMessage: 'No users found.' },
  { value: 'internal', label: 'Internal users', emptyMessage: 'No internal users found.' },
  { value: 'clients', label: 'Clients', emptyMessage: 'No clients found.' },
];

function parsePositiveInt(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const n = Number.parseInt(trimmed, 10);
  if (Number.isNaN(n) || n <= 0 || String(n) !== trimmed) return null;
  return n;
}

export const AdminUsers: React.FC = () => {
  const { user: currentUser } = useAuth();
  const navigate = useNavigate();
  const [users, setUsers] = useState<UserListItemType[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [userType, setUserType] = useState<UserTypeFilter>('all');
  const [paginationModel, setPaginationModel] = useState<GridPaginationModel>({
    page: 0,
    pageSize: 25,
  });
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
  const [rowActionsAnchor, setRowActionsAnchor] = useState<null | HTMLElement>(null);
  const [rowActionsUser, setRowActionsUser] = useState<UserListItemType | null>(null);

  const [editExportLimitUser, setEditExportLimitUser] = useState<UserListItemType | null>(null);
  const [exportLimitOption, setExportLimitOption] = useState<ExportLimitOverrideOption>('role-default');
  const [exportLimitRowsInput, setExportLimitRowsInput] = useState('');
  const [exportLimitSaveLoading, setExportLimitSaveLoading] = useState(false);
  const [exportLimitError, setExportLimitError] = useState<string | null>(null);

  const isAdmin = currentUser?.roles?.some((r) => r === 'Admin' || r === 'SuperAdmin');
  const isSuperAdmin = currentUser?.roles?.includes('SuperAdmin');
  const allowedRoles = isSuperAdmin ? ROLES : ROLES.filter((r) => r !== 'SuperAdmin');

  const loadUsers = useCallback(async (fallbackToPreviousPage = false) => {
    setLoading(true);
    setError(null);
    try {
      const response = await adminUsersService.list({
        userType,
        page: paginationModel.page + 1,
        pageSize: paginationModel.pageSize,
      });

      if (fallbackToPreviousPage && response.items.length === 0 && response.page > 1) {
        setPaginationModel((prev) => ({ ...prev, page: response.page - 2 }));
        return;
      }

      setUsers(response.items);
      setTotalCount(response.totalCount);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load users');
    } finally {
      setLoading(false);
    }
  }, [paginationModel.page, paginationModel.pageSize, userType]);

  const loadRoleSettings = useCallback(async () => {
    try {
      const settings = await roleSettingsService.list();
      setRoleSettings(settings);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load role settings');
    }
  }, []);

  React.useEffect(() => {
    loadUsers();
  }, [loadUsers]);

  React.useEffect(() => {
    loadRoleSettings();
  }, [loadRoleSettings]);

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

  const handleResetPasswordClick = useCallback((u: UserListItemType) => {
    setRowActionsAnchor(null);
    setRowActionsUser(null);
    setResetPasswordConfirmUser(u);
  }, []);

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
      await loadUsers(true);
    } catch {
      setError('Failed to reset password');
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleDisableClick = useCallback((u: UserListItemType) => {
    setRowActionsAnchor(null);
    setRowActionsUser(null);
    setDisableConfirmUser(u);
  }, []);

  const handleDisableConfirm = async () => {
    if (!disableConfirmUser) return;
    const userId = disableConfirmUser.id;
    setActionLoadingId(userId);
    try {
      await adminUsersService.disable(userId);
      setDisableConfirmUser(null);
      await loadUsers(true);
    } catch {
      setError('Failed to disable user');
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleOpenEditExportLimit = useCallback((u: UserListItemType) => {
    setRowActionsAnchor(null);
    setRowActionsUser(null);
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
  }, []);

  const handleOpenRowActions = useCallback((
    event: React.MouseEvent<HTMLElement>,
    user: UserListItemType,
  ) => {
    event.stopPropagation();
    setRowActionsAnchor(event.currentTarget);
    setRowActionsUser(user);
  }, []);

  const navigateToDetails = useCallback((user: UserListItemType) => {
    navigate(`/admin/users/${encodeURIComponent(user.id)}`);
  }, [navigate]);

  const handleViewDetails = useCallback((user: UserListItemType) => {
    setRowActionsAnchor(null);
    setRowActionsUser(null);
    navigateToDetails(user);
  }, [navigateToDetails]);

  const handleRowClick = useCallback((params: GridRowParams<UserListItemType>) => {
    navigateToDetails(params.row);
  }, [navigateToDetails]);

  const handleCloseRowActions = useCallback(() => {
    setRowActionsAnchor(null);
    setRowActionsUser(null);
  }, []);

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
      await loadUsers(true);
    } catch (err) {
      setExportLimitError(
        err instanceof ApiClientError ? err.message : 'Failed to save export limit.',
      );
    } finally {
      setExportLimitSaveLoading(false);
    }
  };

  const canModifyUser = useCallback((targetRole: string): boolean => {
    if (isSuperAdmin) return true;
    return targetRole !== 'SuperAdmin' && targetRole !== 'Admin';
  }, [isSuperAdmin]);

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

  const handleUserTypeChange = (_event: React.MouseEvent<HTMLElement>, nextUserType: UserTypeFilter | null) => {
    if (nextUserType === null) return;
    setUserType(nextUserType);
    setPaginationModel((prev) => ({ ...prev, page: 0 }));
  };

  const handleShowAllUsers = () => {
    setUserType('all');
    setPaginationModel((prev) => ({ ...prev, page: 0 }));
  };

  const handlePaginationModelChange = (model: GridPaginationModel) => {
    setPaginationModel((prev) => ({
      page: model.pageSize !== prev.pageSize ? 0 : model.page,
      pageSize: model.pageSize,
    }));
  };

  const selectedUserTypeOption = USER_TYPE_OPTIONS.find((option) => option.value === userType) ?? USER_TYPE_OPTIONS[0];

  const NoRowsOverlay = () => (
    <Box
      sx={{
        height: '100%',
        minHeight: 120,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 1.5,
        p: 2,
      }}
    >
      <Typography variant="body2" color="text.secondary">
        {selectedUserTypeOption.emptyMessage}
      </Typography>
      {userType !== 'all' && (
        <BrandButton kind="outline" size="small" onClick={handleShowAllUsers}>
          Show all users
        </BrandButton>
      )}
    </Box>
  );

  const columns = useMemo<GridColDef<UserListItemType>[]>(
    () => [
      {
        field: 'email',
        headerName: 'Email',
        flex: 1,
        minWidth: 240,
        sortable: false,
      },
      {
        field: 'displayName',
        headerName: 'Name',
        flex: 0.8,
        minWidth: 220,
        sortable: false,
        renderCell: (params) => (
          <Box sx={{ py: 0.75 }}>
            {params.row.mustCompleteProfile ? (
              <Chip
                label="Profile incomplete"
                size="small"
                color="warning"
                variant="outlined"
                sx={{ height: 22 }}
              />
            ) : (
              <Typography variant="body2">
                {params.row.displayName || '-'}
              </Typography>
            )}
          </Box>
        ),
      },
      {
        field: 'role',
        headerName: 'Role',
        width: 130,
        sortable: false,
      },
      {
        field: 'isActive',
        headerName: 'Status',
        width: 130,
        sortable: false,
        renderCell: (params) => (
          <Chip
            label={params.row.isActive ? 'Active' : 'Disabled'}
            color={params.row.isActive ? 'success' : 'default'}
            size="small"
            variant={params.row.isActive ? 'filled' : 'outlined'}
          />
        ),
      },
      {
        field: 'effectiveExportLimitMode',
        headerName: 'Export limit',
        minWidth: 230,
        flex: 0.7,
        sortable: false,
        renderCell: (params) => {
          const u = params.row;
          return (
            <Box sx={{ py: 0.75 }}>
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
            </Box>
          );
        },
      },
      ...(isSuperAdmin
        ? [
            {
              field: 'actions',
              headerName: 'Actions',
              width: 72,
              sortable: false,
              align: 'center' as const,
              headerAlign: 'center' as const,
              renderCell: (params: { row: UserListItemType }) => {
                const u = params.row;
                const hasActions = u.isActive && (canModifyUser(u.role) || u.isExportLimitEditable);
                return (
                  <Box sx={{ width: '100%', display: 'flex', justifyContent: 'center' }}>
                    <Tooltip title="Actions">
                      <span>
                        <IconButton
                          size="small"
                          aria-label={`Actions for ${u.email}`}
                          onClick={(event) => handleOpenRowActions(event, u)}
                          disabled={!hasActions || actionLoadingId === u.id}
                          sx={{
                            color: 'text.secondary',
                            '&:hover': {
                              bgcolor: 'action.hover',
                              color: 'text.primary',
                            },
                          }}
                        >
                          {actionLoadingId === u.id ? (
                            <CircularProgress size={18} />
                          ) : (
                            <MoreVertIcon fontSize="small" />
                          )}
                        </IconButton>
                      </span>
                    </Tooltip>
                  </Box>
                );
              },
            } satisfies GridColDef<UserListItemType>,
          ]
        : []),
    ],
    [
      actionLoadingId,
      canModifyUser,
      handleOpenRowActions,
      isSuperAdmin,
    ],
  );

  const rowActionsCanModify = rowActionsUser ? rowActionsUser.isActive && canModifyUser(rowActionsUser.role) : false;
  const rowActionsCanEditLimit = Boolean(rowActionsUser?.isActive && rowActionsUser.isExportLimitEditable);

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

      {isSuperAdmin && (
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
      )}

      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, gap: 2 }}>
        <Typography variant="h6">
          {selectedUserTypeOption.label}
        </Typography>
        <ToggleButtonGroup
          value={userType}
          exclusive
          size="small"
          onChange={handleUserTypeChange}
          aria-label="User type"
          sx={{
            flexWrap: 'wrap',
            '& .MuiToggleButton-root': {
              px: 2,
              py: 0.75,
              textTransform: 'none',
              borderColor: 'divider',
            },
          }}
        >
          {USER_TYPE_OPTIONS.map((option) => (
            <ToggleButton key={option.value} value={option.value}>
              {option.label}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>
      </Box>

      <Paper>
        <DataGrid
          rows={users}
          columns={columns}
          getRowId={(row) => row.id}
          rowCount={totalCount}
          loading={loading}
          pageSizeOptions={[10, 25, 50, 100]}
          paginationModel={paginationModel}
          paginationMode="server"
          onPaginationModelChange={handlePaginationModelChange}
          onRowClick={handleRowClick}
          disableRowSelectionOnClick
          disableColumnMenu
          autoHeight
          getRowHeight={() => 'auto'}
          slots={{ noRowsOverlay: NoRowsOverlay }}
          sx={{
            '& .MuiDataGrid-cell': {
              display: 'flex',
              alignItems: 'center',
              py: 0.75,
            },
            '& .MuiDataGrid-row': {
              cursor: 'pointer',
            },
            '& .MuiDataGrid-row:hover': {
              backgroundColor: 'action.hover',
            },
            '& .MuiDataGrid-cell:focus': {
              outline: 'none',
            },
            '& .MuiDataGrid-cell:focus-within': {
              outline: 'none',
            },
            '& .MuiDataGrid-columnHeader': {
              backgroundColor: 'action.hover',
            },
            '& .MuiDataGrid-columnHeader:focus': {
              outline: 'none',
            },
            '& .MuiDataGrid-columnHeader:focus-within': {
              outline: 'none',
            },
          }}
        />
      </Paper>

      <Menu
        anchorEl={rowActionsAnchor}
        open={Boolean(rowActionsAnchor)}
        onClose={handleCloseRowActions}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        {rowActionsUser && (
          <MenuItem onClick={() => handleViewDetails(rowActionsUser)}>
            View details
          </MenuItem>
        )}
        {rowActionsCanModify && rowActionsUser && (
          <MenuItem onClick={() => handleResetPasswordClick(rowActionsUser)}>
            Reset password
          </MenuItem>
        )}
        {rowActionsCanModify && rowActionsUser && (
          <MenuItem onClick={() => handleDisableClick(rowActionsUser)}>
            Disable
          </MenuItem>
        )}
        {rowActionsCanEditLimit && rowActionsUser && (
          <MenuItem onClick={() => handleOpenEditExportLimit(rowActionsUser)}>
            Edit export limit
          </MenuItem>
        )}
      </Menu>

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
