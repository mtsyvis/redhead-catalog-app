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
  Divider,
  Chip,
  IconButton,
  Menu,
  Snackbar,
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
import type {
  ClientExportUsageLimitOverridesRequest,
  UserListItem as UserListItemType,
  UserTypeFilter,
} from '../types/adminUsers.types';
import { NON_SUPER_ADMIN_ROLES, ROLES } from '../types/adminUsers.types';
import type { RoleSettingItem } from '../types/roleSettings.types';
import type { ExportLimitMode } from '../utils/exportLimit';
import { formatExportLimit } from '../utils/exportLimit';
import { dataGridLocaleText } from '../utils/numberFormat';
import { ApiClientError } from '../services/api.client';
import { ROLE_METADATA, isAppRole } from '../constants/rbac.constants';
import { useUserRoles } from '../hooks/useUserRoles';

type ExportLimitOverrideOption = 'role-default' | ExportLimitMode;
type ClientUsageLimitInputName = keyof ClientExportUsageLimitOverridesRequest;
type ClientUsageLimitInputs = Record<ClientUsageLimitInputName, string>;

const USER_TYPE_OPTIONS: Array<{ value: UserTypeFilter; label: string; emptyMessage: string }> = [
  { value: 'all', label: 'All users', emptyMessage: 'No users found.' },
  { value: 'internal', label: 'Internal users', emptyMessage: 'No internal users found.' },
  { value: 'clients', label: 'Clients / Lite', emptyMessage: 'No client or Lite users found.' },
];

const SUPER_ADMIN_NOTE_MAX_LENGTH = 1000;
const SUPER_ADMIN_NOTE_HELPER_TEXT =
  'Visible only to Super Admin. Use it for internal client/account identification.';

const CLIENT_USAGE_LIMIT_FIELDS: Array<{
  name: ClientUsageLimitInputName;
  label: string;
  helperText: string;
  getOverride: (user: UserListItemType) => number | null;
  getEffective: (user: UserListItemType) => number | null;
  getRoleDefault: (setting: RoleSettingItem) => number | null;
}> = [
  {
    name: 'dailyUniqueExportedDomainsLimit',
    label: 'Daily unique exported domains',
    helperText: '24h window',
    getOverride: (user) => user.dailyUniqueExportedDomainsLimitOverride,
    getEffective: (user) => user.effectiveDailyUniqueExportedDomainsLimit,
    getRoleDefault: (setting) => setting.dailyUniqueExportedDomainsLimit,
  },
  {
    name: 'weeklyUniqueExportedDomainsLimit',
    label: 'Weekly unique exported domains',
    helperText: '7d window',
    getOverride: (user) => user.weeklyUniqueExportedDomainsLimitOverride,
    getEffective: (user) => user.effectiveWeeklyUniqueExportedDomainsLimit,
    getRoleDefault: (setting) => setting.weeklyUniqueExportedDomainsLimit,
  },
  {
    name: 'dailyExportOperationsLimit',
    label: 'Daily export operations',
    helperText: '24h window',
    getOverride: (user) => user.dailyExportOperationsLimitOverride,
    getEffective: (user) => user.effectiveDailyExportOperationsLimit,
    getRoleDefault: (setting) => setting.dailyExportOperationsLimit,
  },
  {
    name: 'weeklyExportOperationsLimit',
    label: 'Weekly export operations',
    helperText: '7d window',
    getOverride: (user) => user.weeklyExportOperationsLimitOverride,
    getEffective: (user) => user.effectiveWeeklyExportOperationsLimit,
    getRoleDefault: (setting) => setting.weeklyExportOperationsLimit,
  },
];

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

function getExportLimitCaption(user: UserListItemType): string {
  if (!user.isExportLimitEditable) {
    if (user.role === 'SuperAdmin') return 'Fixed for SuperAdmin';
    if (user.role === 'Lite') return 'Fixed for Lite';
    return 'Fixed setting';
  }

  return user.isExportLimitOverridden ? 'Personal override' : 'Inherited from role';
}

function createEmptyClientUsageLimitInputs(): ClientUsageLimitInputs {
  return {
    dailyUniqueExportedDomainsLimit: '',
    weeklyUniqueExportedDomainsLimit: '',
    dailyExportOperationsLimit: '',
    weeklyExportOperationsLimit: '',
  };
}

function formatEffectiveNumber(value: number | null | undefined): string {
  return value == null ? 'Not available' : value.toLocaleString();
}

function getProfileName(user: UserListItemType): string | null {
  const displayName = user.displayName?.trim();
  return user.mustCompleteProfile ? null : displayName || null;
}

function getRoleCapabilityHelp(role: string): string {
  if (!isAppRole(role)) {
    return '';
  }

  const metadata = ROLE_METADATA[role];
  return `${metadata.description} ${metadata.capabilities}`;
}

export const AdminUsers: React.FC = () => {
  const { user: currentUser } = useAuth();
  const { canReadUsers, canManageUsers, isSuperAdmin } = useUserRoles();
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

  const [secretDialog, setSecretDialog] = useState<{
    title: string;
    email: string;
    value: string;
    kind: 'invitation' | 'password';
  } | null>(null);
  const [copyNotification, setCopyNotification] = useState<{
    open: boolean;
    message: string;
    severity: 'success' | 'error';
  }>({
    open: false,
    message: '',
    severity: 'success',
  });

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
  const [clientUsageLimitInputs, setClientUsageLimitInputs] = useState<ClientUsageLimitInputs>(
    createEmptyClientUsageLimitInputs
  );
  const [exportLimitSaveLoading, setExportLimitSaveLoading] = useState(false);
  const [exportLimitError, setExportLimitError] = useState<string | null>(null);

  const [editNoteUser, setEditNoteUser] = useState<UserListItemType | null>(null);
  const [editNoteInput, setEditNoteInput] = useState('');
  const [editNoteSaveLoading, setEditNoteSaveLoading] = useState(false);
  const [editNoteError, setEditNoteError] = useState<string | null>(null);

  const allowedRoles = canManageUsers ? ROLES : ROLES.filter((r) => r !== 'SuperAdmin');
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
      setSecretDialog({
        title: 'User created',
        email: res.email,
        value: `${window.location.origin}${res.activationPath}`,
        kind: 'invitation',
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
      setSecretDialog({
        title: 'Password reset',
        email: user.email,
        value: res.temporaryPassword,
        kind: 'password',
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
      if (res.activationPath) {
        setSecretDialog({
          title: 'User reactivated',
          email: reactivateUser.email,
          value: `${window.location.origin}${res.activationPath}`,
          kind: 'invitation',
        });
      } else if (res.temporaryPassword) {
        setSecretDialog({
          title: 'User reactivated',
          email: reactivateUser.email,
          value: res.temporaryPassword,
          kind: 'password',
        });
      }
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
    setClientUsageLimitInputs({
      dailyUniqueExportedDomainsLimit:
        u.dailyUniqueExportedDomainsLimitOverride != null
          ? String(u.dailyUniqueExportedDomainsLimitOverride)
          : '',
      weeklyUniqueExportedDomainsLimit:
        u.weeklyUniqueExportedDomainsLimitOverride != null
          ? String(u.weeklyUniqueExportedDomainsLimitOverride)
          : '',
      dailyExportOperationsLimit:
        u.dailyExportOperationsLimitOverride != null
          ? String(u.dailyExportOperationsLimitOverride)
          : '',
      weeklyExportOperationsLimit:
        u.weeklyExportOperationsLimitOverride != null
          ? String(u.weeklyExportOperationsLimitOverride)
          : '',
    });
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

  const handleClientUsageLimitInputChange = (
    field: ClientUsageLimitInputName,
    value: string
  ) => {
    setClientUsageLimitInputs((prev) => ({ ...prev, [field]: value }));
    setExportLimitError(null);
  };

  const handleCloseExportLimit = () => {
    if (exportLimitSaveLoading) return;
    setEditExportLimitUser(null);
    setClientUsageLimitInputs(createEmptyClientUsageLimitInputs());
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
    let clientUsageLimitOverrides: ClientExportUsageLimitOverridesRequest | undefined;

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

    if (editExportLimitUser.role === 'Client') {
      const nextClientUsageLimitOverrides: ClientExportUsageLimitOverridesRequest = {
        dailyUniqueExportedDomainsLimit: null,
        weeklyUniqueExportedDomainsLimit: null,
        dailyExportOperationsLimit: null,
        weeklyExportOperationsLimit: null,
      };

      for (const field of CLIENT_USAGE_LIMIT_FIELDS) {
        const rawValue = clientUsageLimitInputs[field.name].trim();
        if (!rawValue) {
          nextClientUsageLimitOverrides[field.name] = null;
          continue;
        }

        const parsed = parsePositiveInt(rawValue);
        if (parsed === null) {
          setExportLimitError(`${field.label}: enter a positive integer (no decimals, no zero).`);
          return;
        }

        nextClientUsageLimitOverrides[field.name] = parsed;
      }

      clientUsageLimitOverrides = nextClientUsageLimitOverrides;
    }

    setExportLimitError(null);
    setExportLimitSaveLoading(true);
    try {
      await adminUsersService.updateExportLimit(editExportLimitUser.id, {
        overrideMode,
        overrideRows,
        clientUsageLimitOverrides,
      });
      setEditExportLimitUser(null);
      setClientUsageLimitInputs(createEmptyClientUsageLimitInputs());
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
    return Boolean(canManageUsers && targetUser.id !== currentUser?.id);
  }, [canManageUsers, currentUser?.id]);

  const canChangeUserRole = useCallback((targetUser: UserListItemType): boolean => {
    return Boolean(
      canManageUsers &&
      targetUser.isActive &&
      targetUser.id !== currentUser?.id &&
      normalRoles.includes(targetUser.role as (typeof normalRoles)[number]),
    );
  }, [canManageUsers, currentUser?.id, normalRoles]);

  const copySecret = async () => {
    if (!secretDialog) return;

    try {
      await navigator.clipboard.writeText(secretDialog.value);
      setCopyNotification({ open: true, message: 'Copied', severity: 'success' });
    } catch {
      setCopyNotification({
        open: true,
        message: 'Could not copy. Please copy the value manually.',
        severity: 'error',
      });
    }
  };

  const handleReissueInvitation = useCallback(async (user: UserListItemType) => {
    setRowActionsAnchor(null);
    setRowActionsUser(null);
    setActionLoadingId(user.id);
    try {
      const response = await adminUsersService.reissueInvitation(user.id);
      setSecretDialog({
        title: 'Invitation reissued',
        email: user.email,
        value: `${window.location.origin}${response.activationPath}`,
        kind: 'invitation',
      });
      await loadUsers(true);
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Failed to reissue invitation');
    } finally {
      setActionLoadingId(null);
    }
  }, [loadUsers]);

  const getExportLimitPreview = (): string | null => {
    if (!editExportLimitUser) return null;
    if (exportLimitOption === 'role-default') {
      const roleDefault = roleSettings.find((r) => r.role === editExportLimitUser.role);
      if (!roleDefault) return null;
      return formatExportLimit(roleDefault.exportLimitMode, roleDefault.exportLimitRows);
    }
    if (exportLimitOption === 'Disabled') return 'Disabled';
    if (exportLimitOption === 'Unlimited') return 'Unlimited';
    if (exportLimitOption === 'Limited') {
      const parsed = parsePositiveInt(exportLimitRowsInput);
      if (parsed === null) return null;
      return `${parsed.toLocaleString()} rows`;
    }
    return null;
  };

  const getClientUsageLimitPreviewRows = () => {
    if (!editExportLimitUser || editExportLimitUser.role !== 'Client') return [];

    const roleDefault = roleSettings.find((r) => r.role === editExportLimitUser.role);

    return CLIENT_USAGE_LIMIT_FIELDS.map((field) => {
      const rawValue = clientUsageLimitInputs[field.name].trim();
      const parsedOverride = rawValue ? parsePositiveInt(rawValue) : null;
      const invalidOverride = Boolean(rawValue && parsedOverride === null);
      const roleDefaultValue = roleDefault ? field.getRoleDefault(roleDefault) : null;
      const effectiveValue = parsedOverride ?? roleDefaultValue;

      return {
        label: field.label,
        effective: invalidOverride ? 'Invalid' : formatEffectiveNumber(effectiveValue),
      };
    });
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
          const missingNameLabel = params.row.accountStatus === 'InvitationExpired'
            ? 'Invitation expired'
            : params.row.accountStatus === 'PendingActivation'
              ? null
              : 'Profile incomplete';

          return (
            <Box sx={{ py: 0.75, minWidth: 0 }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0, flexWrap: 'wrap' }}>
                {(profileName || missingNameLabel) && (
                  <Typography
                    variant="body2"
                    sx={{
                      fontWeight: 600,
                      color: profileName ? 'text.primary' : 'warning.dark',
                      wordBreak: 'break-word',
                      minWidth: 0,
                    }}
                  >
                    {profileName ?? missingNameLabel}
                  </Typography>
                )}
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
        field: 'accountStatus',
        headerName: 'Status',
        width: 170,
        sortable: false,
        renderCell: (params) => {
          const status = params.row.accountStatus;
          const label = status === 'PendingActivation'
            ? 'Pending activation'
            : status === 'InvitationExpired'
              ? 'Invitation expired'
              : status;
          const color = status === 'Active'
            ? 'success'
            : status === 'PendingActivation'
              ? 'info'
            : status === 'InvitationExpired'
              ? 'warning'
              : 'default';
          return <Chip label={label} color={color} size="small" variant={status === 'Active' ? 'filled' : 'outlined'} />;
        },
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
                {getExportLimitCaption(u)}
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
      ...(canManageUsers
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
                  (u.isActive && (canModifyUser(u) || u.isExportLimitEditable || canManageUsers)) ||
                  (!u.isActive && canManageUsers);
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
      canManageUsers,
      currentUser?.id,
      handleOpenRowActions,
      isSuperAdmin,
    ],
  );
  const rowActionsCanModify = rowActionsUser ? rowActionsUser.isActive && canModifyUser(rowActionsUser) : false;
  const rowActionsCanChangeRole = rowActionsUser ? canChangeUserRole(rowActionsUser) : false;
  const rowActionsCanReactivate = Boolean(canManageUsers && rowActionsUser && !rowActionsUser.isActive);
  const rowActionsCanEditLimit = Boolean(rowActionsUser?.isActive && rowActionsUser.isExportLimitEditable);
  const rowActionsCanEditNote = Boolean(canManageUsers && isSuperAdmin && rowActionsUser?.isActive);
  const rowActionsCanReissueInvitation = Boolean(
    canManageUsers
      && rowActionsUser?.isActive
      && (rowActionsUser.accountStatus === 'PendingActivation'
        || rowActionsUser.accountStatus === 'InvitationExpired')
  );
  const rowActionsCanResetPassword = Boolean(
    rowActionsCanModify && rowActionsUser?.accountStatus === 'Active'
  );

  if (!canReadUsers) {
    return <Navigate to="/sites" replace />;
  }

  const exportLimitPreview = getExportLimitPreview();
  const clientUsageLimitPreviewRows = getClientUsageLimitPreviewRows();

  return (
    <PageShell
      title="Users"
      maxWidth="lg"
      actions={
        canManageUsers ? (
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
        {rowActionsCanReissueInvitation && rowActionsUser && (
          <MenuItem onClick={() => void handleReissueInvitation(rowActionsUser)}>
            Reissue invitation
          </MenuItem>
        )}
        {rowActionsCanResetPassword && rowActionsUser && (
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
              <Typography variant="body2" color="text.secondary">
                {getRoleCapabilityHelp(createRole)}
              </Typography>
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

      <Snackbar
        open={copyNotification.open}
        autoHideDuration={2000}
        onClose={() => setCopyNotification((current) => ({ ...current, open: false }))}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          severity={copyNotification.severity}
          onClose={() => setCopyNotification((current) => ({ ...current, open: false }))}
          sx={{ width: '100%' }}
        >
          {copyNotification.message}
        </Alert>
      </Snackbar>

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
              <Typography variant="body2" color="text.secondary">
                {getRoleCapabilityHelp(changeRoleValue)}
              </Typography>
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
                {getRoleCapabilityHelp(reactivateRoleValue)}
              </Typography>

              <Typography variant="body2" color="text.secondary">
                The new activation link or temporary password will be shown only once.
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

      <Dialog open={!!secretDialog} onClose={() => setSecretDialog(null)} maxWidth="sm" fullWidth>
        <DialogTitle>{secretDialog?.title}</DialogTitle>
        <DialogContent>
          {secretDialog && (
            <>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                {secretDialog.email}
              </Typography>
              <Alert severity="warning" sx={{ mt: 1 }}>
                This {secretDialog.kind === 'invitation' ? 'activation link' : 'password'} is shown only once.
                Copy it now and share it securely with the user.
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
                {secretDialog.value}
              </Box>
            </>
          )}
        </DialogContent>
        <DialogActions>
          <BrandButton kind="outline" onClick={copySecret}>
            Copy {secretDialog?.kind === 'invitation' ? 'link' : 'password'}
          </BrandButton>
          <BrandButton onClick={() => setSecretDialog(null)}>Done</BrandButton>
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
              </Box>

              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
                <Box>
                  <Typography variant="subtitle2">Per-export row limit</Typography>
                  <Typography variant="body2" color="text.secondary">
                    Controls how many site rows this user can export in one file.
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
                    label="Rows per export"
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
                  <Box
                    sx={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      gap: 2,
                      px: 1.5,
                      py: 1,
                      borderRadius: 1,
                      bgcolor: 'background.default',
                    }}
                  >
                    <Typography variant="body2" color="text.secondary">
                      Effective per-export limit
                    </Typography>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {exportLimitPreview}
                    </Typography>
                  </Box>
                )}
              </Box>

              {editExportLimitUser.role === 'Client' && (
                <>
                  <Divider />
                  <Box>
                    <Typography variant="subtitle2">Client usage limits</Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
                      Daily and weekly quotas. Blank fields use the Client role default.
                    </Typography>
                  </Box>
                  <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2 }}>
                    {CLIENT_USAGE_LIMIT_FIELDS.map((field) => {
                      const value = clientUsageLimitInputs[field.name];
                      const invalid = value.trim() !== '' && parsePositiveInt(value) === null;

                      return (
                        <TextField
                          key={field.name}
                          label={field.label}
                          type="number"
                          value={value}
                          onChange={(e) => handleClientUsageLimitInputChange(field.name, e.target.value)}
                          slotProps={{ htmlInput: { min: 1, step: 1 } }}
                          disabled={exportLimitSaveLoading}
                          error={invalid}
                          helperText={invalid ? 'Positive integer required' : field.helperText}
                          fullWidth
                        />
                      );
                    })}
                  </Box>

                  <Box
                    sx={{
                      border: 1,
                      borderColor: 'divider',
                      borderRadius: 1,
                      p: 1.5,
                      bgcolor: 'background.default',
                    }}
                  >
                    <Typography variant="subtitle2" sx={{ mb: 1 }}>
                      Effective usage limits
                    </Typography>
                    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 1 }}>
                      {clientUsageLimitPreviewRows.map((row) => (
                        <Box
                          key={row.label}
                          sx={{ display: 'flex', justifyContent: 'space-between', gap: 1 }}
                        >
                          <Typography variant="body2" color="text.secondary">
                            {row.label}
                          </Typography>
                          <Typography variant="body2" sx={{ fontWeight: 600 }}>
                            {row.effective}
                          </Typography>
                        </Box>
                      ))}
                    </Box>
                  </Box>
                </>
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
