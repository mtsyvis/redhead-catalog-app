import type { ExportLimitMode } from '../utils/exportLimit';

export interface RoleSettingItem {
  role: string;
  exportLimitMode: ExportLimitMode;
  exportLimitRows: number | null;
  isEditable: boolean;
}

export interface RoleSettingUpdateItem {
  role: string;
  exportLimitMode: ExportLimitMode;
  exportLimitRows: number | null;
}
