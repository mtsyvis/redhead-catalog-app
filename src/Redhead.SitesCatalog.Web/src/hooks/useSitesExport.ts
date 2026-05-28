import { useCallback, useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ApiClientError } from '../services/api.client';
import { googleDriveService } from '../services/googleDrive.service';
import { sitesService } from '../services/sites.service';
import type { GoogleDriveDialogState } from '../components/sites/dialogs/GoogleDriveConnectionDialog';
import type { SitesSnackbarState } from '../components/sites/feedback/SitesSnackbar';
import type {
  GoogleDriveExportPayload,
  MultiSearchResponse,
  SitesQueryParams,
} from '../types/sites.types';
import type { GoogleDriveStatus } from '../types/googleDrive.types';

const GOOGLE_DRIVE_NOT_CONNECTED = 'GoogleDriveNotConnected';
const GOOGLE_DRIVE_RECONNECT_REQUIRED = 'GoogleDriveReconnectRequired';
const GOOGLE_DRIVE_UPLOAD_FAILED = 'GoogleDriveUploadFailed';
const GOOGLE_DRIVE_CONFIGURATION_MISSING = 'GoogleDriveConfigurationMissing';

interface UseSitesExportOptions {
  buildSitesQueryParams: (page: number, pageSize: number) => SitesQueryParams;
  multiSearchResult: MultiSearchResponse | null;
  searchText: string;
  visibleColumnKeys: string[];
  showSnackbar: (snackbar: SitesSnackbarState) => void;
}

function getApiErrorCode(error: unknown): string | undefined {
  return error instanceof ApiClientError ? error.message : undefined;
}

function openGoogleDriveFile(webViewLink: string | null): boolean {
  if (!webViewLink) {
    return false;
  }

  try {
    const opened = window.open(webViewLink, '_blank', 'noopener,noreferrer');
    return Boolean(opened);
  } catch {
    return false;
  }
}

export function useSitesExport({
  buildSitesQueryParams,
  multiSearchResult,
  searchText,
  visibleColumnKeys,
  showSnackbar,
}: UseSitesExportOptions) {
  const location = useLocation();
  const navigate = useNavigate();
  const [exporting, setExporting] = useState(false);
  const [googleDriveStatus, setGoogleDriveStatus] = useState<GoogleDriveStatus | null>(null);
  const [googleDriveDialog, setGoogleDriveDialog] = useState<GoogleDriveDialogState>({
    open: false,
    reconnect: false,
  });
  const [connectingGoogleDrive, setConnectingGoogleDrive] = useState(false);

  const loadGoogleDriveStatus = useCallback(async () => {
    try {
      const status = await googleDriveService.getStatus();
      setGoogleDriveStatus(status);
    } catch (error) {
      console.error('Failed to load Google Drive status:', error);
      setGoogleDriveStatus(null);
    }
  }, []);

  useEffect(() => {
    void loadGoogleDriveStatus();
  }, [loadGoogleDriveStatus]);

  useEffect(() => {
    const params = new URLSearchParams(location.search);
    const callbackStatus = params.get('googleDrive');
    if (!callbackStatus) {
      return;
    }

    params.delete('googleDrive');
    navigate(
      {
        pathname: location.pathname,
        search: params.toString() ? `?${params.toString()}` : '',
      },
      { replace: true }
    );

    if (callbackStatus === 'connected') {
      showSnackbar({
        open: true,
        message: 'Google Drive connected',
        severity: 'success',
      });
      void loadGoogleDriveStatus();
      return;
    }

    showSnackbar({
      open: true,
      message: 'Could not connect Google Drive. Try again when you are ready.',
      severity: 'error',
    });
  }, [location.pathname, location.search, navigate, loadGoogleDriveStatus, showSnackbar]);

  const buildGoogleDriveExportPayload = useCallback((): GoogleDriveExportPayload => {
    const params = buildSitesQueryParams(1, 1000000);

    if (multiSearchResult !== null) {
      return {
        searchText: searchText.trim(),
        filters: params,
        visibleColumnKeys,
      };
    }

    return { filters: params, visibleColumnKeys };
  }, [buildSitesQueryParams, multiSearchResult, searchText, visibleColumnKeys]);

  const handleDownloadExport = useCallback(async () => {
    setExporting(true);
    try {
      const params = buildSitesQueryParams(1, 1000000);

      let metadata;
      if (multiSearchResult !== null) {
        metadata = await sitesService.exportSitesMultiSearch({
          searchText: searchText.trim(),
          filters: params,
          visibleColumnKeys,
        });
      } else {
        metadata = await sitesService.exportSites({ filters: params, visibleColumnKeys });
      }

      if (metadata.truncated) {
        showSnackbar({
          open: true,
          message: `Export completed with limit applied: ${metadata.exportedRows} of ${metadata.requestedRows} rows downloaded.`,
          severity: 'success',
        });
      } else {
        showSnackbar({
          open: true,
          message: 'Export completed successfully',
          severity: 'success',
        });
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Export failed';
      showSnackbar({ open: true, message, severity: 'error' });
    } finally {
      setExporting(false);
    }
  }, [buildSitesQueryParams, multiSearchResult, searchText, visibleColumnKeys, showSnackbar]);

  const handleSaveToGoogleDrive = useCallback(async () => {
    if (googleDriveStatus?.needsReconnect) {
      setGoogleDriveDialog({ open: true, reconnect: true });
      return;
    }

    if (googleDriveStatus && !googleDriveStatus.connected) {
      setGoogleDriveDialog({ open: true, reconnect: false });
      return;
    }

    setExporting(true);

    try {
      const result = await sitesService.exportSitesToGoogleDrive(buildGoogleDriveExportPayload());
      openGoogleDriveFile(result.webViewLink);

      const details = [
        result.fileName,
        result.destinationLabel,
        result.wasTruncated
          ? `Limit applied: ${result.rowsExported} rows saved.`
          : `${result.rowsExported} rows saved.`,
      ].filter(Boolean);

      showSnackbar({
        open: true,
        message: 'Export saved to Google Drive',
        detail: details.join(' - '),
        severity: 'success',
        actionLabel: 'Open file',
        onAction: () => {
          openGoogleDriveFile(result.webViewLink);
        },
      });

      void loadGoogleDriveStatus();
    } catch (error) {
      const errorCode = getApiErrorCode(error);

      if (errorCode === GOOGLE_DRIVE_NOT_CONNECTED) {
        setGoogleDriveDialog({ open: true, reconnect: false });
      } else if (errorCode === GOOGLE_DRIVE_RECONNECT_REQUIRED) {
        setGoogleDriveDialog({ open: true, reconnect: true });
        void loadGoogleDriveStatus();
      } else if (errorCode === GOOGLE_DRIVE_UPLOAD_FAILED) {
        showSnackbar({
          open: true,
          message: 'Could not save to Google Drive',
          detail: 'You can still download the Excel export.',
          severity: 'error',
          actionLabel: 'Download Excel',
          onAction: () => {
            void handleDownloadExport();
          },
        });
      } else if (errorCode === GOOGLE_DRIVE_CONFIGURATION_MISSING) {
        showSnackbar({
          open: true,
          message: 'Google Drive export is not available right now.',
          detail: 'You can still download the Excel export.',
          severity: 'error',
          actionLabel: 'Download Excel',
          onAction: () => {
            void handleDownloadExport();
          },
        });
      } else if (error instanceof ApiClientError && error.statusCode === 403) {
        showSnackbar({
          open: true,
          message: error.message,
          severity: 'error',
        });
      } else if (error instanceof ApiClientError && error.statusCode === 400) {
        showSnackbar({
          open: true,
          message: error.message,
          severity: 'error',
        });
      } else {
        showSnackbar({
          open: true,
          message: 'Could not save to Google Drive. You can still download Excel.',
          severity: 'error',
          actionLabel: 'Download Excel',
          onAction: () => {
            void handleDownloadExport();
          },
        });
      }
    } finally {
      setExporting(false);
    }
  }, [
    googleDriveStatus,
    buildGoogleDriveExportPayload,
    loadGoogleDriveStatus,
    showSnackbar,
    handleDownloadExport,
  ]);

  const handleConnectGoogleDrive = useCallback(async () => {
    setConnectingGoogleDrive(true);
    try {
      const response = await googleDriveService.startConnect();
      window.location.assign(response.authorizationUrl);
    } catch (error) {
      console.error('Failed to start Google Drive connection:', error);
      showSnackbar({
        open: true,
        message: 'Could not start Google Drive connection. Try again later.',
        severity: 'error',
      });
    } finally {
      setConnectingGoogleDrive(false);
    }
  }, [showSnackbar]);

  const closeGoogleDriveDialog = useCallback(() => {
    setGoogleDriveDialog((state) => ({ ...state, open: false }));
  }, []);

  return {
    exporting,
    googleDriveStatus,
    googleDriveDialog,
    connectingGoogleDrive,
    handleDownloadExport,
    handleSaveToGoogleDrive,
    handleConnectGoogleDrive,
    closeGoogleDriveDialog,
  };
}
