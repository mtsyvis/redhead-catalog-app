import type { ExportLimitMode } from '../utils/exportLimit';

export interface RoleSettingItem {
  role: string;
  exportLimitMode: ExportLimitMode;
  exportLimitRows: number | null;
  isEditable: boolean;
  dailyUniqueExportedDomainsLimit: number | null;
  weeklyUniqueExportedDomainsLimit: number | null;
  dailyExportOperationsLimit: number | null;
  weeklyExportOperationsLimit: number | null;
}

export interface RoleSettingUpdateItem {
  role: string;
  exportLimitMode: ExportLimitMode;
  exportLimitRows: number | null;
  dailyUniqueExportedDomainsLimit?: number | null;
  weeklyUniqueExportedDomainsLimit?: number | null;
  dailyExportOperationsLimit?: number | null;
  weeklyExportOperationsLimit?: number | null;
}
