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
import { NON_SUPER_ADMIN_ROLES, ROLES } from '../types/adminUsers.types';
import type { RoleSettingItem } from '../types/roleSettings.types';
import type { ExportLimitMode } from '../utils/exportLimit';
import { formatExportLimit } from '../utils/exportLimit';
import { dataGridLocaleText } from '../utils/numberFormat';
import { ApiClientError } from '../services/api.client';

type ExportLimitOverrideOption = 'role-default' | ExportLimitMode;

const USER_TYPE_OPTIONS: Array<{ value: UserTypeFilter; label: string; emptyMessage: string }> = [
  { value: 'all', label: 'All users', emptyMessage: 'No users found.' },
  { value: 'internal', label: 'Internal users', emptyMessage: 'No internal users found.' },
  { value: 'clients', label: 'Clients', emptyMessage: 'No clients found.' },
];

const SUPER_ADMIN_NOTE_MAX_LENGTH = 1000;
const SUPER_ADMIN_NOTE_HELPER_TEXT =
  'Visible only to Super Admin. Use it for internal client/account identification.';

function parsePositiveInt(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const n = Number.parseInt(trimmed, 10);
  if (Number.isNaN(n) || n <= 0 || String(n) !== trimmed) return null;
  return n;
}

function normalizeOptionalText(value: string): string | null {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
}

function getProfileName(user: UserListItemType): string | null {
  const firstName = user.firstName?.trim();
  const lastName = user.lastName?.trim();

  if (!firstName || !lastName) return null;

  return `${firstName} ${lastName}`;
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

  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [createEmail, setCreateEmail] = useState('');
  const [createRole, setCreateRole] = useState<string>(ROLES[1]);
  const [createSuperAdminNote, setCreateSuperAdminNote] = useState('');
  const [createLoading, setCreateLoading] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const [tempPasswordDialog, setTempPasswordDialog] = useState<{
    title: string;
    email: string;
    password: string;
  } | null>(null);

  const [disableConfirmUser, setDisableConfirmUser] = useState<UserListItemType | null>(null);
  const [resetPasswordConfirmUser, setResetPasswordConfirmUser] = useState<UserListItemType | null>(null);
  const [changeRoleUser, setChangeRoleUser] = useState<UserListItemType | null>(null);
  const [changeRoleValue, setChangeRoleValue] = useState<string>(NON_SUPER_ADMIN_ROLES[0]);
  const [changeRoleLoading, setChangeRoleLoading] = useState(false);
  const [changeRoleError, setChangeRoleError] = useState<string | null>(null);
  const [reactivateUser, setReactivateUser] = useState<UserListItemType | null>(null);
  const [reactivateRoleValue, setReactivateRoleValue] = useState<string>(NON_SUPER_ADMIN_ROLES[0]);
  const [reactivateLoading, setReactivateLoading] = useState(false);
  const [reactivateError, setReactivateError] = useState<string | null>(null);
  const [actionLoadingId, setActionLoadingId] = useState<string | null>(null);
  const [rowActionsAnchor, setRowActionsAnchor] = useState<null | HTMLElement>(null);
  const [rowActionsUser, setRowActionsUser] = useState<UserListItemType | null>(null);

  const [editExportLimitUser, setEditExportLimitUser] = useState<UserListItemType | null>(null);
  const [exportLimitOption, setExportLimitOption] = useState<ExportLimitOverrideOption>('role-default');
  const [exportLimitRowsInput, setExportLimitRowsInput] = useState('');
  const [exportLimitSaveLoading, setExportLimitSaveLoading] = useState(false);
  const [exportLimitError, setExportLimitError] = useState<string | null>(null);

  const [editNoteUser, setEditNoteUser] = useState<UserListItemType | null>(null);
  const [editNoteInput, setEditNoteInput] = useState('');
  const [editNoteSaveLoading, setEditNoteSaveLoading] = useState(false);
  const [editNoteError, setEditNoteError] = useState<string | null>(null);

  const isAdmin = currentUser?.roles?.some((r) => r === 'Admin' || r === 'SuperAdmin');
  const isSuperAdmin = currentUser?.roles?.includes('SuperAdmin');
  const allowedRoles = isSuperAdmin ? ROLES : ROLES.filter((r) => r !== 'SuperAdmin');
  const normalRoles = NON_SUPER_ADMIN_ROLES;

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

  const resetCreateForm = useCallback(() => {
    setCreateEmail('');
    setCreateRole(ROLES[1]);
    setCreateSuperAdminNote('');
    setCreateError(null);
  }, []);

  const handleOpenCreateDialog = useCallback(() => {
    resetCreateForm();
    setCreateDialogOpen(true);
  }, [resetCreateForm]);

  const handleCloseCreateDialog = useCallback(() => {
    if (createLoading) return;
    setCreateDialogOpen(false);
    resetCreateForm();
  }, [createLoading, resetCreateForm]);

  const handleCreate = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setCreateError(null);
    setCreateLoading(true);
    try {
      const res = await adminUsersService.create({
        email: createEmail,
        role: createRole,
        superAdminNote: normalizeOptionalText(createSuperAdminNote),
      });
      setCreateDialogOpen(false);
      resetCreateForm();
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

  const handleChangeRoleClick = useCallback((u: UserListItemType) => {
    setRowActionsAnchor(null);
    setRowActionsUser(null);
    setChangeRoleUser(u);
    setChangeRoleValue(
      normalRoles.includes(u.role as (typeof normalRoles)[number])
        ? u.role
        : normalRoles[0],
    );
    setChangeRoleError(null);
  }, [normalRoles]);

  const handleCloseChangeRole = () => {
    if (changeRoleLoading) return;
    setChangeRoleUser(null);
    setChangeRoleError(null);
  };

  const handleChangeRoleConfirm = async () => {
    if (!changeRoleUser || changeRoleValue === changeRoleUser.role) return;

    setChangeRoleError(null);
    setChangeRoleLoading(true);
    setActionLoadingId(changeRoleUser.id);
    try {
      await adminUsersService.updateRole(changeRoleUser.id, { role: changeRoleValue });
      setChangeRoleUser(null);
      await loadUsers(true);
    } catch (err) {
      setChangeRoleError(err instanceof ApiClientError ? err.message : 'Failed to change role');
    } finally {
      setActionLoadingId(null);
      setChangeRoleLoading(false);
    }
  };

  const handleReactivateClick = useCallback((u: UserListItemType) => {
    setRowActionsAnchor(null);
    setRowActionsUser(null);
    setReactivateUser(u);
    setReactivateRoleValue(
      u.role === 'SuperAdmin'
        ? 'SuperAdmin'
        : normalRoles.includes(u.role as (typeof normalRoles)[number])
          ? u.role
          : normalRoles[0],
    );
    setReactivateError(null);
  }, [normalRoles]);

  const handleCloseReactivate = () => {
    if (reactivateLoading) return;
    setReactivateUser(null);
    setReactivateError(null);
  };

  const handleReactivateConfirm = async () => {
    if (!reactivateUser) return;

    setReactivateError(null);
    setReactivateLoading(true);
    setActionLoadingId(reactivateUser.id);
    try {
      const res = await adminUsersService.reactivate(reactivateUser.id, { role: reactivateRoleValue });
      setReactivateUser(null);
      setTempPasswordDialog({
        title: 'User reactivated',
        email: reactivateUser.email,
        password: res.temporaryPassword,
      });
      await loadUsers(true);
    } catch (err) {
      setReactivateError(err instanceof ApiClientError ? err.message : 'Failed to reactivate user');
    } finally {
      setActionLoadingId(null);
      setReactivateLoading(false);
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

  const handleOpenEditNote = useCallback((u: UserListItemType) => {
    setRowActionsAnchor(null);
    setRowActionsUser(null);
    setEditNoteUser(u);
    setEditNoteInput(u.superAdminNote ?? '');
    setEditNoteError(null);
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

  const handleCloseEditNote = () => {
    if (editNoteSaveLoading) return;
    setEditNoteUser(null);
    setEditNoteInput('');
    setEditNoteError(null);
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

  const handleSaveEditNote = async () => {
    if (!editNoteUser) return;

    setEditNoteError(null);
    setEditNoteSaveLoading(true);
    try {
      await adminUsersService.updateSuperAdminNote(editNoteUser.id, {
        superAdminNote: normalizeOptionalText(editNoteInput),
      });
      setEditNoteUser(null);
      setEditNoteInput('');
      await loadUsers(true);
    } catch (err) {
      setEditNoteError(
        err instanceof ApiClientError ? err.message : 'Failed to save admin note.',
      );
    } finally {
      setEditNoteSaveLoading(false);
    }
  };

  const canModifyUser = useCallback((targetUser: UserListItemType): boolean => {
    return Boolean(isSuperAdmin && targetUser.id !== currentUser?.id);
  }, [currentUser?.id, isSuperAdmin]);

  const canChangeUserRole = useCallback((targetUser: UserListItemType): boolean => {
    return Boolean(
      isSuperAdmin &&
      targetUser.isActive &&
      targetUser.id !== currentUser?.id &&
      normalRoles.includes(targetUser.role as (typeof normalRoles)[number]),
    );
  }, [currentUser?.id, isSuperAdmin, normalRoles]);

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
        field: 'user',
        headerName: 'User',
        flex: 1.15,
        minWidth: 280,
        sortable: false,
        renderCell: (params) => {
          const profileName = getProfileName(params.row);
          const isCurrentUserRow = params.row.id === currentUser?.id;

          return (
            <Box sx={{ py: 0.75, minWidth: 0 }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0, flexWrap: 'wrap' }}>
                <Typography
                  variant="body2"
                  sx={{
                    fontWeight: 600,
                    color: profileName ? 'text.primary' : 'warning.dark',
                    wordBreak: 'break-word',
                    minWidth: 0,
                  }}
                >
                  {profileName ?? 'Profile incomplete'}
                </Typography>
                {isCurrentUserRow && (
                  <Chip
                    label="You"
                    color="primary"
                    size="small"
                    variant="outlined"
                    sx={{
                      height: 20,
                      fontSize: '0.6875rem',
                      fontWeight: 700,
                    }}
                  />
                )}
              </Box>
              <Typography variant="caption" color="text.secondary" sx={{ wordBreak: 'break-word' }}>
                {params.row.email}
              </Typography>
            </Box>
          );
        },
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
              field: 'superAdminNote',
              headerName: 'Super Admin note',
              minWidth: 220,
              flex: 0.8,
              sortable: false,
              renderCell: (params: { row: UserListItemType }) => {
                const note = params.row.superAdminNote?.trim();

                return (
                  <Box sx={{ py: 0.75, minWidth: 0, width: '100%', overflow: 'hidden' }}>
                    <Typography
                      variant="body2"
                      color={note ? 'text.primary' : 'text.secondary'}
                      sx={{
                        display: '-webkit-box',
                        WebkitLineClamp: 2,
                        WebkitBoxOrient: 'vertical',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        wordBreak: 'break-word',
                        lineHeight: 1.35,
                        maxHeight: '2.7em',
                      }}
                    >
                      {note || '—'}
                    </Typography>
                  </Box>
                );
              },
            } satisfies GridColDef<UserListItemType>,
          ]
        : []),
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
                const hasActions =
                  (u.isActive && (canModifyUser(u) || u.isExportLimitEditable || isSuperAdmin)) ||
                  (!u.isActive && isSuperAdmin);
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
      currentUser?.id,
      handleOpenRowActions,
      isSuperAdmin,
    ],
  );
  const rowActionsCanModify = rowActionsUser ? rowActionsUser.isActive && canModifyUser(rowActionsUser) : false;
  const rowActionsCanChangeRole = rowActionsUser ? canChangeUserRole(rowActionsUser) : false;
  const rowActionsCanReactivate = Boolean(isSuperAdmin && rowActionsUser && !rowActionsUser.isActive);
  const rowActionsCanEditLimit = Boolean(rowActionsUser?.isActive && rowActionsUser.isExportLimitEditable);
  const rowActionsCanEditNote = Boolean(isSuperAdmin && rowActionsUser?.isActive);

  if (!isAdmin) {
    return <Navigate to="/sites" replace />;
  }

  const exportLimitPreview = getExportLimitPreview();

  return (
    <PageShell
      title="Users"
      maxWidth="lg"
      actions={
        isSuperAdmin ? (
          <BrandButton kind="primary" onClick={handleOpenCreateDialog}>
            Add user
          </BrandButton>
        ) : undefined
      }
    >
      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
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
          getRowClassName={(params) => (params.row.id === currentUser?.id ? 'current-user-row' : '')}
          localeText={dataGridLocaleText}
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
            '& .MuiDataGrid-row.current-user-row': {
              backgroundColor: 'rgba(25, 118, 210, 0.04)',
              boxShadow: 'inset 3px 0 0 rgba(25, 118, 210, 0.85)',
            },
            '& .MuiDataGrid-row:hover': {
              backgroundColor: 'action.hover',
            },
            '& .MuiDataGrid-row.current-user-row:hover': {
              backgroundColor: 'rgba(25, 118, 210, 0.08)',
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
        {rowActionsCanEditLimit && rowActionsUser && (
          <MenuItem onClick={() => handleOpenEditExportLimit(rowActionsUser)}>
            Edit export limit
          </MenuItem>
        )}
        {rowActionsCanEditNote && rowActionsUser && (
          <MenuItem onClick={() => handleOpenEditNote(rowActionsUser)}>
            Edit super admin note
          </MenuItem>
        )}
        {rowActionsCanChangeRole && rowActionsUser && (
          <MenuItem onClick={() => handleChangeRoleClick(rowActionsUser)}>
            Change role
          </MenuItem>
        )}
        {rowActionsCanReactivate && rowActionsUser && (
          <MenuItem onClick={() => handleReactivateClick(rowActionsUser)}>
            Reactivate
          </MenuItem>
        )}
        {rowActionsCanModify && rowActionsUser && (
          <MenuItem onClick={() => handleResetPasswordClick(rowActionsUser)}>
            Reset password
          </MenuItem>
        )}
        {rowActionsCanModify && rowActionsUser && (
          <MenuItem onClick={() => handleDisableClick(rowActionsUser)} sx={{ color: 'error.main' }}>
            Disable
          </MenuItem>
        )}
      </Menu>

      <Dialog
        open={createDialogOpen}
        onClose={handleCloseCreateDialog}
        maxWidth="sm"
        fullWidth
      >
        <Box component="form" onSubmit={handleCreate}>
          <DialogTitle>Add user</DialogTitle>
          <DialogContent>
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
              <TextField
                label="Email"
                type="email"
                required
                value={createEmail}
                onChange={(e) => {
                  setCreateEmail(e.target.value);
                  setCreateError(null);
                }}
                disabled={createLoading}
                autoComplete="off"
                fullWidth
              />
              <FormControl fullWidth disabled={createLoading} required>
                <InputLabel>Role</InputLabel>
                <Select
                  value={createRole}
                  label="Role"
                  onChange={(e) => {
                    setCreateRole(e.target.value);
                    setCreateError(null);
                  }}
                >
                  {allowedRoles.map((r) => (
                    <MenuItem key={r} value={r}>
                      {r}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              {isSuperAdmin && (
                <TextField
                  label="Super Admin note"
                  value={createSuperAdminNote}
                  onChange={(e) => {
                    setCreateSuperAdminNote(e.target.value);
                    setCreateError(null);
                  }}
                  disabled={createLoading}
                  helperText={SUPER_ADMIN_NOTE_HELPER_TEXT}
                  multiline
                  rows={4}
                  fullWidth
                  slotProps={{
                    htmlInput: { maxLength: SUPER_ADMIN_NOTE_MAX_LENGTH },
                  }}
                />
              )}
              {createError && <Alert severity="error">{createError}</Alert>}
            </Box>
          </DialogContent>
          <DialogActions>
            <BrandButton kind="outline" onClick={handleCloseCreateDialog} disabled={createLoading}>
              Cancel
            </BrandButton>
            <BrandButton kind="primary" type="submit" disabled={createLoading}>
              {createLoading ? <CircularProgress size={24} color="inherit" /> : 'Create'}
            </BrandButton>
          </DialogActions>
        </Box>
      </Dialog>

      <Dialog
        open={!!changeRoleUser}
        onClose={handleCloseChangeRole}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle>Change role</DialogTitle>
        <DialogContent>
          {changeRoleUser && (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
              <Box>
                <Typography variant="body2">
                  <strong>{changeRoleUser.email}</strong>
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Current role: {changeRoleUser.role}
                </Typography>
              </Box>
              <FormControl fullWidth disabled={changeRoleLoading} required>
                <InputLabel>New role</InputLabel>
                <Select
                  value={changeRoleValue}
                  label="New role"
                  onChange={(e) => {
                    setChangeRoleValue(e.target.value);
                    setChangeRoleError(null);
                  }}
                >
                  {normalRoles.map((r) => (
                    <MenuItem key={r} value={r}>
                      {r}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              {changeRoleError && <Alert severity="error">{changeRoleError}</Alert>}
            </Box>
          )}
        </DialogContent>
        <DialogActions>
          <BrandButton kind="outline" onClick={handleCloseChangeRole} disabled={changeRoleLoading}>
            Cancel
          </BrandButton>
          <BrandButton
            kind="primary"
            onClick={handleChangeRoleConfirm}
            disabled={changeRoleLoading || !changeRoleUser || changeRoleValue === changeRoleUser.role}
          >
            {changeRoleLoading ? <CircularProgress size={24} color="inherit" /> : 'Save'}
          </BrandButton>
        </DialogActions>
      </Dialog>

      <Dialog
        open={!!reactivateUser}
        onClose={handleCloseReactivate}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle>Reactivate user</DialogTitle>
        <DialogContent>
          {reactivateUser && (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
              <Box>
                <Typography variant="body2">
                  <strong>{reactivateUser.email}</strong>
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Current role: {reactivateUser.role}
                </Typography>
              </Box>

              {reactivateUser.role === 'SuperAdmin' ? (
                <TextField label="Role" value="SuperAdmin" disabled fullWidth />
              ) : (
                <FormControl fullWidth disabled={reactivateLoading} required>
                  <InputLabel>Role</InputLabel>
                  <Select
                    value={reactivateRoleValue}
                    label="Role"
                    onChange={(e) => {
                      setReactivateRoleValue(e.target.value);
                      setReactivateError(null);
                    }}
                  >
                    {normalRoles.map((r) => (
                      <MenuItem key={r} value={r}>
                        {r}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}

              <Typography variant="body2" color="text.secondary">
                A new temporary password will be generated and shown only once.
              </Typography>
              {reactivateError && <Alert severity="error">{reactivateError}</Alert>}
            </Box>
          )}
        </DialogContent>
        <DialogActions>
          <BrandButton kind="outline" onClick={handleCloseReactivate} disabled={reactivateLoading}>
            Cancel
          </BrandButton>
          <BrandButton
            kind="primary"
            onClick={handleReactivateConfirm}
            disabled={reactivateLoading || !reactivateUser}
          >
            {reactivateLoading ? <CircularProgress size={24} color="inherit" /> : 'Reactivate'}
          </BrandButton>
        </DialogActions>
      </Dialog>

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
                This user will no longer be able to sign in or access the application.
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

      <Dialog
        open={!!editNoteUser}
        onClose={handleCloseEditNote}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle>Edit admin note</DialogTitle>
        <DialogContent>
          {editNoteUser && (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
              <Box>
                <Typography variant="body2">
                  <strong>{editNoteUser.email}</strong> · {editNoteUser.role}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {getProfileName(editNoteUser) ?? 'Profile incomplete'}
                </Typography>
              </Box>

              <TextField
                label="Super Admin note"
                value={editNoteInput}
                onChange={(e) => {
                  setEditNoteInput(e.target.value);
                  setEditNoteError(null);
                }}
                disabled={editNoteSaveLoading}
                helperText={SUPER_ADMIN_NOTE_HELPER_TEXT}
                multiline
                rows={4}
                fullWidth
                slotProps={{
                  htmlInput: { maxLength: SUPER_ADMIN_NOTE_MAX_LENGTH },
                }}
              />

              {editNoteError && <Alert severity="error">{editNoteError}</Alert>}
            </Box>
          )}
        </DialogContent>
        <DialogActions>
          <BrandButton kind="outline" onClick={handleCloseEditNote} disabled={editNoteSaveLoading}>
            Cancel
          </BrandButton>
          <BrandButton
            kind="primary"
            onClick={handleSaveEditNote}
            disabled={editNoteSaveLoading || !editNoteUser}
          >
            {editNoteSaveLoading ? <CircularProgress size={24} color="inherit" /> : 'Save'}
          </BrandButton>
        </DialogActions>
      </Dialog>
    </PageShell>
  );
};
