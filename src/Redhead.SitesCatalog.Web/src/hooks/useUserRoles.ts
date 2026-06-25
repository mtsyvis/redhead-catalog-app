import { useAuth } from '../contexts/AuthContext';
import {
  type AppPermission,
  userHasPermission,
} from '../constants/rbac.constants';

export function useUserRoles() {
  const { user } = useAuth();
  const roles = user?.roles ?? [];

  const hasRole = (role: string) => roles.includes(role);
  const hasAnyRole = (candidates: string[]) => candidates.some((role) => roles.includes(role));
  const hasPermission = (permission: AppPermission) => userHasPermission(roles, permission);

  const isSuperAdmin = hasRole('SuperAdmin');
  const isAdmin = hasAnyRole(['Admin', 'SuperAdmin']);
  const isInternal = hasRole('Internal');
  const isClient = hasRole('Client');
  const isLite = hasRole('Lite');
  const canBrowseSites = hasPermission('SitesBrowse');
  const canMultiSearchSites = hasPermission('SitesMultiSearch');
  const canEditSites = hasPermission('SitesEdit');
  const canExportSites = hasPermission('SitesExport');
  const canManageTableViews = hasPermission('TableViewsManage');
  const canRunImports = hasPermission('ImportsRun');
  const canReadUsers = hasPermission('UsersRead');
  const canManageUsers = hasPermission('UsersManage');
  const canReadRoleSettings = hasPermission('RoleSettingsRead');
  const canManageRoleSettings = hasPermission('RoleSettingsManage');
  const canReadAnalytics = hasPermission('AnalyticsRead');
  const canManageAhrefsSync = hasPermission('AhrefsSyncManage');

  return {
    roles,
    isSuperAdmin,
    isAdmin,
    isInternal,
    isClient,
    isLite,
    hasRole,
    hasAnyRole,
    hasPermission,
    canBrowseSites,
    canMultiSearchSites,
    canEditSites,
    canExportSites,
    canManageTableViews,
    canRunImports,
    canReadUsers,
    canManageUsers,
    canReadRoleSettings,
    canManageRoleSettings,
    canReadAnalytics,
    canManageAhrefsSync,
  };
}
