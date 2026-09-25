import { useCallback, useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ApiClientError } from '../services/api.client';
import { authService } from '../services/auth.service';
import { googleDriveService } from '../services/googleDrive.service';
import { sitesService } from '../services/sites.service';
import type { GoogleDriveDialogState } from '../components/sites/dialogs/GoogleDriveConnectionDialog';
import type { SitesSnackbarState } from '../components/sites/feedback/SitesSnackbar';
import type {
  GoogleDriveExportPayload,
  ExportPreview,
  MultiSearchResponse,
  SitesQueryParams,
} from '../types/sites.types';
import type { GoogleDriveStatus } from '../types/googleDrive.types';
import type { CurrentUserProfileLimits } from '../types/auth.types';
import {
  getExportUsageLimitMessage,
  getExportUsageLimitUsageLine,
  isExportUsageLimitReason,
} from '../utils/exportUsageLimits';

const GOOGLE_DRIVE_NOT_CONNECTED = 'GoogleDriveNotConnected';
const GOOGLE_DRIVE_RECONNECT_REQUIRED = 'GoogleDriveReconnectRequired';
const GOOGLE_DRIVE_UPLOAD_FAILED = 'GoogleDriveUploadFailed';
const GOOGLE_DRIVE_CONFIGURATION_MISSING = 'GoogleDriveConfigurationMissing';

interface UseSitesExportOptions {
  buildSitesQueryParams: (page: number, pageSize: number) => SitesQueryParams;
  isClient: boolean;
  enabled?: boolean;
  multiSearchResult: MultiSearchResponse | null;
  quarantineFilter: NonNullable<SitesQueryParams['quarantine']>;
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

function getUsageLimitErrorMessage(error: unknown): string | null {
  if (!(error instanceof ApiClientError)) {
    return null;
  }

  return getExportUsageLimitMessage(error.message);
}

function buildPartialExportDetail(
  exportedRows: number,
  reason: string | null | undefined,
  limits: CurrentUserProfileLimits | null
): string {
  const exportedText = `Exported ${exportedRows.toLocaleString()} domains.`;
  const usageLine = getExportUsageLimitUsageLine(reason, limits);

  return usageLine ? `${exportedText} ${usageLine}` : exportedText;
}

export function useSitesExport({
  buildSitesQueryParams,
  isClient,
  enabled = true,
  multiSearchResult,
  quarantineFilter,
  searchText,
  visibleColumnKeys,
  showSnackbar,
}: UseSitesExportOptions) {
  const location = useLocation();
  const navigate = useNavigate();
  const [exporting, setExporting] = useState(false);
  const [includeQuarantinedByChoice, setIncludeQuarantinedByChoice] = useState(false);
  const [pendingExport, setPendingExport] = useState<{ preview: ExportPreview; run: () => Promise<void> } | null>(null);
  const [googleDriveStatus, setGoogleDriveStatus] = useState<GoogleDriveStatus | null>(null);
  const [googleDriveDialog, setGoogleDriveDialog] = useState<GoogleDriveDialogState>({
    open: false,
    reconnect: false,
  });
  const [connectingGoogleDrive, setConnectingGoogleDrive] = useState(false);
  const [exportUsageLimits, setExportUsageLimits] = useState<CurrentUserProfileLimits | null>(null);
  const includeQuarantined =
    quarantineFilter === 'only' ||
    (quarantineFilter === 'all' && includeQuarantinedByChoice);
  const excludeQuarantined = !includeQuarantined;

  const loadGoogleDriveStatus = useCallback(async () => {
    if (!enabled) {
      setGoogleDriveStatus(null);
      return;
    }

    try {
      const status = await googleDriveService.getStatus();
      setGoogleDriveStatus(status);
    } catch (error) {
      console.error('Failed to load Google Drive status:', error);
      setGoogleDriveStatus(null);
    }
  }, [enabled]);

  const loadExportUsageLimits = useCallback(async (): Promise<CurrentUserProfileLimits | null> => {
    if (!enabled || !isClient) {
      setExportUsageLimits(null);
      return null;
    }

    try {
      const profile = await authService.getCurrentProfile();
      setExportUsageLimits(profile.limits);
      return profile.limits;
    } catch (error) {
      console.error('Failed to load export usage limits:', error);
      setExportUsageLimits(null);
      return null;
    }
  }, [enabled, isClient]);

  useEffect(() => {
    void loadGoogleDriveStatus();
  }, [loadGoogleDriveStatus]);

  useEffect(() => {
    void loadExportUsageLimits();
  }, [loadExportUsageLimits]);

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
        excludeQuarantined,
      };
    }

    return { filters: params, visibleColumnKeys, excludeQuarantined };
  }, [buildSitesQueryParams, excludeQuarantined, multiSearchResult, searchText, visibleColumnKeys]);

  const runWithExportState = useCallback(async (run: () => Promise<void>) => {
    setExporting(true);
    try {
      await run();
    } finally {
      setExporting(false);
    }
  }, []);

  const requestExport = useCallback((run: () => Promise<void>) => runWithExportState(async () => {
    if (!isClient) {
      await run();
      return;
    }
    try {
      const preview = await sitesService.previewExport(buildGoogleDriveExportPayload());
      if (preview.isBlocked) {
        showSnackbar({ open: true, severity: 'warning',
          message: getExportUsageLimitMessage(preview.reason) ?? 'Your export limit has been reached. Please try again later.' });
      } else if (preview.exportableRows < preview.selectionRows) {
        setPendingExport({ preview, run });
      } else {
        await run();
      }
    } catch (error) {
      showSnackbar({ open: true, severity: 'error', message: error instanceof Error ? error.message : 'Could not check export availability.' });
    }
  }), [isClient, buildGoogleDriveExportPayload, showSnackbar, runWithExportState]);

  const handleDownloadExport = useCallback(async () => {
    try {
      const params = buildSitesQueryParams(1, 1000000);

      let metadata;
      if (multiSearchResult !== null) {
        metadata = await sitesService.exportSitesMultiSearch({
          searchText: searchText.trim(),
          filters: params,
          visibleColumnKeys,
          excludeQuarantined,
        });
      } else {
        metadata = await sitesService.exportSites({
          filters: params,
          visibleColumnKeys,
          excludeQuarantined,
        });
      }

      const updatedUsageLimits = await loadExportUsageLimits();

      if (metadata.truncated && isExportUsageLimitReason(metadata.truncationReason)) {
        showSnackbar({
          open: true,
          message: 'Export completed partially because your export limit was reached.',
          detail: buildPartialExportDetail(
            metadata.exportedRows,
            metadata.truncationReason,
            updatedUsageLimits
          ),
          severity: 'success',
        });
      } else if (metadata.truncated) {
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
    }
  }, [
    buildSitesQueryParams,
    multiSearchResult,
    searchText,
    visibleColumnKeys,
    excludeQuarantined,
    loadExportUsageLimits,
    showSnackbar,
  ]);

  const handleSaveToGoogleDrive = useCallback(async () => {
    if (googleDriveStatus?.needsReconnect) {
      setGoogleDriveDialog({ open: true, reconnect: true });
      return;
    }

    if (googleDriveStatus && !googleDriveStatus.connected) {
      setGoogleDriveDialog({ open: true, reconnect: false });
      return;
    }

    try {
      const result = await sitesService.exportSitesToGoogleDrive(buildGoogleDriveExportPayload());
      openGoogleDriveFile(result.webViewLink);
      const updatedUsageLimits = await loadExportUsageLimits();
      const wasUsageLimitPartial =
        result.wasTruncated && isExportUsageLimitReason(result.truncationReason);

      const details = wasUsageLimitPartial
        ? [
            result.fileName,
            buildPartialExportDetail(
              result.rowsExported,
              result.truncationReason,
              updatedUsageLimits
            ),
          ].filter(Boolean)
        : [
            result.fileName,
            result.destinationLabel,
            result.wasTruncated
              ? `Limit applied: ${result.rowsExported} rows saved.`
              : `${result.rowsExported} rows saved.`,
          ].filter(Boolean);

      showSnackbar({
        open: true,
        message: wasUsageLimitPartial
          ? 'Export completed partially because your export limit was reached.'
          : 'Export saved to Google Drive',
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
      const usageLimitMessage = getUsageLimitErrorMessage(error);

      if (usageLimitMessage) {
        showSnackbar({
          open: true,
          message: usageLimitMessage,
          severity: 'error',
        });
        void loadExportUsageLimits();
      } else if (errorCode === GOOGLE_DRIVE_NOT_CONNECTED) {
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
            void requestExport(handleDownloadExport);
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
            void requestExport(handleDownloadExport);
          },
        });
      } else if (error instanceof ApiClientError && error.statusCode === 403) {
        showSnackbar({
          open: true,
          message: error.message,
          severity: 'error',
        });
      } else if (error instanceof ApiClientError && (error.statusCode === 400 || error.statusCode === 429)) {
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
            void requestExport(handleDownloadExport);
          },
        });
      }
    }
  }, [
    googleDriveStatus,
    buildGoogleDriveExportPayload,
    loadGoogleDriveStatus,
    loadExportUsageLimits,
    showSnackbar,
    handleDownloadExport,
    requestExport,
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
    includeQuarantined,
    setIncludeQuarantined: setIncludeQuarantinedByChoice,
    googleDriveStatus,
    exportUsageLimits,
    googleDriveDialog,
    connectingGoogleDrive,
    handleDownloadExport: () => requestExport(handleDownloadExport),
    handleSaveToGoogleDrive: () => requestExport(handleSaveToGoogleDrive),
    pendingExport,
    cancelPartialExport: () => setPendingExport(null),
    confirmPartialExport: async () => {
      const pending = pendingExport;
      setPendingExport(null);
      if (pending) await runWithExportState(pending.run);
    },
    handleConnectGoogleDrive,
    closeGoogleDriveDialog,
  };
}
