export interface GoogleDriveStatus {
  connected: boolean;
  googleEmail: string | null;
  connectedAtUtc: string | null;
  exportFolderName: string | null;
  hasExportFolderId: boolean;
  revoked: boolean;
  needsReconnect: boolean;
}

export interface GoogleDriveConnectStartResponse {
  authorizationUrl: string;
}
