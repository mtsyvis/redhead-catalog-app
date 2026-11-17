import { useEffect, useRef, useState } from "react";
import { Alert, Link, ListItemIcon, Menu, MenuItem, Stack, Typography } from "@mui/material";
import AddToDriveIcon from "@mui/icons-material/AddToDrive";
import ArrowDropDownIcon from "@mui/icons-material/ArrowDropDown";
import DownloadOutlinedIcon from "@mui/icons-material/DownloadOutlined";
import { BrandButton } from "../common/BrandButton";
import {
  GoogleDriveConnectionDialog,
  type GoogleDriveDialogState,
} from "../sites/dialogs/GoogleDriveConnectionDialog";
import { useUserRoles } from "../../hooks/useUserRoles";
import { googleDriveService } from "../../services/googleDrive.service";
import { webmasterOffersService } from "../../services/webmasterOffers.service";
import type { BulkOfferPriceOperation } from "../../types/bulkOfferPricePreview.types";
import type { GoogleDriveStatus } from "../../types/googleDrive.types";
import type { GoogleDriveExportResponse } from "../../types/sites.types";

export function BulkOfferPriceReportExports({ operation }: { operation: BulkOfferPriceOperation }) {
  const { canExportWebmasterOffers } = useUserRoles();
  return (
    <OperationReportExports
      key={`${operation.id}:${canExportWebmasterOffers}`}
      operation={operation}
      canExport={canExportWebmasterOffers}
    />
  );
}

function OperationReportExports({
  operation,
  canExport,
}: {
  operation: BulkOfferPriceOperation;
  canExport: boolean;
}) {
  const [menu, setMenu] = useState<{ anchor: HTMLElement; calculation: boolean } | null>(null);
  const [exporting, setExporting] = useState<{ calculation: boolean; googleDrive: boolean } | null>(
    null
  );
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState<GoogleDriveExportResponse | null>(null);
  const [driveStatus, setDriveStatus] = useState<GoogleDriveStatus | null>(null);
  const [driveDialog, setDriveDialog] = useState<GoogleDriveDialogState>({
    open: false,
    reconnect: false,
  });
  const [connecting, setConnecting] = useState(false);
  const pending = useRef<AbortController | null>(null);
  useEffect(() => () => pending.current?.abort(), []);

  const hasCalculation = operation.result.rows.some((row) => row.catalogRecalculationNeeded);
  const explanation = !canExport
    ? "Report exports require Webmaster Offers export permission (Admin or SuperAdmin)."
    : !hasCalculation
      ? "No purchase minima changed. There is no catalog calculation data to export."
      : null;

  const exportReport = async (calculation: boolean, googleDrive: boolean) => {
    setMenu(null);
    if (!canExport || (calculation && !hasCalculation) || pending.current) return;
    const controller = new AbortController();
    pending.current = controller;
    setExporting({ calculation, googleDrive });
    setError(null);
    setSaved(null);
    try {
      if (googleDrive) {
        const status = await googleDriveService.getStatus();
        controller.signal.throwIfAborted();
        setDriveStatus(status);
        if (!status.connected || status.needsReconnect) {
          setDriveDialog({ open: true, reconnect: status.needsReconnect });
          return;
        }
        const result = await webmasterOffersService.saveBulkReportToGoogleDrive(
          operation.id,
          calculation,
          controller.signal
        );
        controller.signal.throwIfAborted();
        setSaved(result);
        if (result.webViewLink) {
          try {
            window.open(result.webViewLink, "_blank", "noopener,noreferrer");
          } catch {
            // The success alert keeps an Open file link if automatic opening is blocked.
          }
        }
      } else {
        await webmasterOffersService.downloadBulkReport(
          operation.id,
          calculation,
          controller.signal
        );
      }
    } catch (cause) {
      if (!controller.signal.aborted) {
        const message = cause instanceof Error ? cause.message : "Could not export this report.";
        if (
          googleDrive &&
          (message === "GoogleDriveNotConnected" || message === "GoogleDriveReconnectRequired")
        ) {
          setDriveDialog({ open: true, reconnect: message === "GoogleDriveReconnectRequired" });
        } else {
          setError(
            message === "GoogleDriveConfigurationMissing"
              ? "Google Drive integration is not configured. You can download Excel instead."
              : message === "GoogleDriveUploadFailed"
                ? "Could not save to Google Drive. Try again or download Excel."
                : message
          );
        }
      }
    } finally {
      if (pending.current === controller) {
        pending.current = null;
        if (!controller.signal.aborted) setExporting(null);
      }
    }
  };

  const connectDrive = async () => {
    if (pending.current) return;
    const controller = new AbortController();
    pending.current = controller;
    setConnecting(true);
    setError(null);
    try {
      const response = await googleDriveService.startConnect();
      controller.signal.throwIfAborted();
      window.location.assign(response.authorizationUrl);
    } catch {
      if (!controller.signal.aborted)
        setError("Could not start Google Drive connection. Try again later.");
    } finally {
      if (pending.current === controller) {
        pending.current = null;
        if (!controller.signal.aborted) setConnecting(false);
      }
    }
  };

  return (
    <Stack spacing={0.75}>
      <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
        {[false, true].map((calculation) => (
          <BrandButton
            key={String(calculation)}
            size="small"
            startIcon={<DownloadOutlinedIcon />}
            endIcon={<ArrowDropDownIcon />}
            disabled={!canExport || (calculation && !hasCalculation) || !!exporting || connecting}
            onClick={(event) => setMenu({ anchor: event.currentTarget, calculation })}
            aria-controls={
              menu?.calculation === calculation ? "bulk-price-report-export-menu" : undefined
            }
            aria-haspopup="menu"
            aria-expanded={menu?.calculation === calculation ? "true" : undefined}
          >
            {exporting?.calculation === calculation
              ? exporting.googleDrive
                ? "Saving…"
                : "Downloading…"
              : calculation
                ? "Catalog calculation data"
                : "Update report"}
          </BrandButton>
        ))}
      </Stack>
      <Menu
        id="bulk-price-report-export-menu"
        anchorEl={menu?.anchor}
        open={!!menu}
        onClose={() => setMenu(null)}
      >
        <MenuItem onClick={() => menu && void exportReport(menu.calculation, false)}>
          <ListItemIcon>
            <DownloadOutlinedIcon fontSize="small" />
          </ListItemIcon>
          Download Excel
        </MenuItem>
        <MenuItem onClick={() => menu && void exportReport(menu.calculation, true)}>
          <ListItemIcon>
            <AddToDriveIcon fontSize="small" />
          </ListItemIcon>
          Save to Google Drive
        </MenuItem>
      </Menu>
      {explanation && (
        <Typography variant="caption" color="text.secondary">
          {explanation}
        </Typography>
      )}
      {error && <Alert severity="error">{error}</Alert>}
      {saved && (
        <Alert
          severity="success"
          action={
            saved.webViewLink ? (
              <Link
                href={saved.webViewLink}
                target="_blank"
                rel="noopener noreferrer"
                color="inherit"
              >
                Open file
              </Link>
            ) : undefined
          }
        >
          Report saved to Google Drive · {saved.fileName}
        </Alert>
      )}
      <GoogleDriveConnectionDialog
        dialog={driveDialog}
        status={driveStatus}
        connecting={connecting}
        onClose={() => setDriveDialog({ open: false, reconnect: false })}
        onConnect={() => void connectDrive()}
      />
    </Stack>
  );
}
