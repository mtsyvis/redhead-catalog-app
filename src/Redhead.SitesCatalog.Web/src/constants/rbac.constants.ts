export const APP_ROLES = ['SuperAdmin', 'Admin', 'Internal', 'Client', 'Lite'] as const;
export type AppRole = (typeof APP_ROLES)[number];

export const NON_SUPER_ADMIN_ROLES = ['Admin', 'Internal', 'Client', 'Lite'] as const;
export type NonSuperAdminRole = (typeof NON_SUPER_ADMIN_ROLES)[number];

export const APP_PERMISSIONS = [
  'SitesBrowse',
  'SitesMultiSearch',
  'SitesEdit',
  'SitesExport',
  'TableViewsManage',
  'ImportsRun',
  'UsersRead',
  'UsersManage',
  'RoleSettingsRead',
  'RoleSettingsManage',
  'AnalyticsRead',
  'AhrefsSyncManage',
] as const;
export type AppPermission = (typeof APP_PERMISSIONS)[number];

export const ROLE_PERMISSION_MATRIX: Record<AppRole, readonly AppPermission[]> = {
  SuperAdmin: APP_PERMISSIONS,
  Admin: [
    'SitesBrowse',
    'SitesMultiSearch',
    'SitesEdit',
    'SitesExport',
    'TableViewsManage',
    'ImportsRun',
    'UsersRead',
    'RoleSettingsRead',
    'AnalyticsRead',
    'AhrefsSyncManage',
  ],
  Internal: ['SitesBrowse', 'SitesMultiSearch', 'SitesExport', 'TableViewsManage'],
  Client: ['SitesBrowse', 'SitesMultiSearch', 'SitesExport', 'TableViewsManage'],
  Lite: ['SitesMultiSearch', 'TableViewsManage'],
};

export const ROLE_METADATA: Record<
  AppRole,
  {
    label: string;
    description: string;
    capabilities: string;
  }
> = {
  SuperAdmin: {
    label: 'Super Admin',
    description: 'System owner with full access.',
    capabilities: 'All catalog, export, import, analytics, user, role settings, and sync controls.',
  },
  Admin: {
    label: 'Admin',
    description: 'Internal manager without user-management ownership.',
    capabilities: 'Browse, edit, export, import, read users and role settings, read analytics, and manage Ahrefs sync.',
  },
  Internal: {
    label: 'Internal',
    description: 'Internal read-oriented team member.',
    capabilities: 'Browse, multi-search, export, and manage table views.',
  },
  Client: {
    label: 'Client',
    description: 'External read-oriented client account.',
    capabilities: 'Browse, multi-search, export with client limits, and manage client-safe custom views.',
  },
  Lite: {
    label: 'Lite',
    description: 'Restricted client account for Multi-search checks.',
    capabilities: 'No filters, no sites browse, no exports.',
  },
};

export function isAppRole(role: string): role is AppRole {
  return APP_ROLES.includes(role as AppRole);
}

export function userHasPermission(roles: readonly string[], permission: AppPermission): boolean {
  return roles.some((role) =>
    isAppRole(role) && ROLE_PERMISSION_MATRIX[role].includes(permission)
  );
}
