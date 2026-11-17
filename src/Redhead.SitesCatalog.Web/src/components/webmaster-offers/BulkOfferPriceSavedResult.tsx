import { useEffect, useState } from "react";
import { useNavigate, useLocation, useSearchParams } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
  List,
  ListItemButton,
  ListItemText,
  Skeleton,
} from "@mui/material";
import { SitesSnackbar } from "../sites/feedback/SitesSnackbar";
import { BulkOfferPriceHeader } from "./BulkOfferPriceHeader";
import { BrandButton } from "../common/BrandButton";
import { PageShell } from "../layout/PageShell";
import { BulkOfferPricePreviewTable } from "./BulkOfferPricePreviewTable";
import { webmasterOffersService } from "../../services/webmasterOffers.service";
import { BulkOfferPriceReportExports } from "./BulkOfferPriceReportExports";
import type {
  BulkOfferPriceOperation,
  BulkOfferPriceOperationSummary,
} from "../../types/bulkOfferPricePreview.types";

function RecentUpdatesSkeleton() {
  return (
    <Stack
      role="status"
      aria-label="Loading recent updates"
      aria-busy="true"
      spacing={2}
      sx={{
        px: 2,
        py: 1,
        "@media (prefers-reduced-motion: reduce)": {
          "& .MuiSkeleton-root::after": { animation: "none" },
        },
      }}
    >
      {[0, 1, 2].map((row) => (
        <Box key={row} aria-hidden="true">
          <Skeleton animation="wave" width="55%" height={24} />
          <Skeleton animation="wave" width="85%" height={20} />
        </Box>
      ))}
    </Stack>
  );
}

function SavedResultSkeleton() {
  return (
    <Stack
      role="status"
      aria-label="Loading saved result"
      aria-busy="true"
      spacing={2}
      sx={{
        "@media (prefers-reduced-motion: reduce)": {
          "& .MuiSkeleton-root::after": { animation: "none" },
        },
      }}
    >
      <Stack aria-hidden="true" direction="row" justifyContent="space-between" spacing={2}>
        <Skeleton animation="wave" width="35%" height={40} />
        <Skeleton animation="wave" width="20%" height={40} />
      </Stack>
      <Skeleton aria-hidden="true" animation="wave" width="40%" height={42} />
      <Box
        aria-hidden="true"
        sx={{ border: 1, borderColor: "divider", borderRadius: 1, overflow: "hidden" }}
      >
        <Skeleton animation="wave" variant="rectangular" height={44} />
        {[0, 1, 2].map((row) => (
          <Stack
            key={row}
            direction="row"
            spacing={3}
            sx={{ px: 2, py: 2, borderTop: 1, borderColor: "divider" }}
          >
            {[30, 20, 20, 20].map((width, column) => (
              <Skeleton key={column} animation="wave" width={`${width}%`} height={24} />
            ))}
          </Stack>
        ))}
      </Box>
    </Stack>
  );
}

export function RecentBulkOfferOperations({ disabled = false }: { disabled?: boolean }) {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [recent, setRecent] = useState<BulkOfferPriceOperationSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    if (!open) return;
    const controller = new AbortController();
    webmasterOffersService
      .getRecentBulkOperations(controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) {
          setRecent(result);
          setError(null);
        }
      })
      .catch((cause) => {
        if (!controller.signal.aborted)
          setError(cause instanceof Error ? cause.message : "Could not load recent operations.");
      });
    return () => controller.abort();
  }, [open, retry]);
  return (
    <>
      <Button
        variant="text"
        size="small"
        sx={{ color: "text.secondary", textTransform: "none" }}
        disabled={disabled}
        onClick={() => {
          setRecent(null);
          setError(null);
          setOpen(true);
        }}
      >
        Recent updates
      </Button>
      <Dialog open={open} onClose={() => setOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Recent price updates</DialogTitle>
        <DialogContent dividers sx={{ minHeight: 220 }}>
          {error ? (
            <Alert
              severity="error"
              action={
                <BrandButton
                  onClick={() => {
                    setError(null);
                    setRetry((value) => value + 1);
                  }}
                >
                  Retry
                </BrandButton>
              }
            >
              {error}
            </Alert>
          ) : !recent ? (
            <RecentUpdatesSkeleton />
          ) : !recent.length ? (
            <Typography>No saved updates yet.</Typography>
          ) : (
            <List disablePadding>
              {recent.map((operation) => (
                <ListItemButton
                  key={operation.id}
                  onClick={() => {
                    setOpen(false);
                    navigate(
                      `/webmaster-offers/update-prices?operation=${encodeURIComponent(operation.id)}`
                    );
                  }}
                >
                  <ListItemText
                    primary={operation.primaryEmail}
                    secondary={
                      <>
                        <Typography
                          component="span"
                          variant="body2"
                          display="block"
                          color="text.secondary"
                        >
                          {new Date(operation.appliedAtUtc).toLocaleString(undefined, {
                            year: "numeric",
                            month: "short",
                            day: "numeric",
                            hour: "2-digit",
                            minute: "2-digit",
                          })}
                          {" \u00b7 "}
                          {operation.actor}
                        </Typography>
                        <Typography
                          component="span"
                          variant="caption"
                          display="block"
                          color="text.secondary"
                        >
                          {operation.updatedCount} updated{" \u00b7 "}
                          {operation.noChangesCount} unchanged{" \u00b7 "}
                          {operation.exceptionCount} exceptions
                        </Typography>
                      </>
                    }
                  />
                </ListItemButton>
              ))}
            </List>
          )}
        </DialogContent>
        <DialogActions>
          <BrandButton onClick={() => setOpen(false)}>Close</BrandButton>
        </DialogActions>
      </Dialog>
    </>
  );
}

export function BulkOfferPriceSavedResult({ id }: { id: string }) {
  const navigate = useNavigate();
  const location = useLocation();
  const [params] = useSearchParams();
  const isResult = params.get("view") === "result";
  const [showSuccess, setShowSuccess] = useState(
    () => isResult && location.state?.appliedOperationId === id
  );
  useEffect(() => {
    if (location.state?.appliedOperationId === id) {
      navigate(location.pathname + location.search, { replace: true, state: null });
    }
  }, [id, location.pathname, location.search, location.state, navigate]);
  const [operation, setOperation] = useState<BulkOfferPriceOperation | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    webmasterOffersService
      .getBulkOperation(id, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) {
          setOperation(result);
          setError(null);
        }
      })
      .catch((cause) => {
        if (!controller.signal.aborted)
          setError(cause instanceof Error ? cause.message : "Could not load this operation.");
      });
    return () => controller.abort();
  }, [id, retry]);
  return (
    <PageShell maxWidth={false}>
      <Stack spacing={2}>
        <BulkOfferPriceHeader
          title={isResult ? "Update webmaster prices" : "Price update details"}
          step={isResult ? 2 : undefined}
          applied={!!operation}
          actions={
            <>
              <RecentBulkOfferOperations />
              <BrandButton size="small" onClick={() => navigate("/webmaster-offers/update-prices")}>
                New update
              </BrandButton>
            </>
          }
        />
        {error ? (
          <Alert
            severity="error"
            action={
              <BrandButton
                onClick={() => {
                  setError(null);
                  setRetry((value) => value + 1);
                }}
              >
                Retry
              </BrandButton>
            }
          >
            {error}
          </Alert>
        ) : !operation ? (
          <SavedResultSkeleton />
        ) : (
          <>
            <BulkOfferPriceReportExports operation={operation} />
            <BulkOfferPricePreviewTable
              preview={operation.result}
              saved
              operationDetails={
                <Stack sx={{ textAlign: { xs: "left", sm: "right" }, overflowWrap: "anywhere" }}>
                  <Typography variant="caption" color="text.secondary">
                    {new Date(operation.appliedAtUtc).toLocaleString(undefined, {
                      year: "numeric",
                      month: "short",
                      day: "numeric",
                      hour: "2-digit",
                      minute: "2-digit",
                    })}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {operation.actor}
                  </Typography>
                </Stack>
              }
            />
          </>
        )}
      </Stack>
      <SitesSnackbar
        snackbar={{
          open: isResult && showSuccess && !!operation,
          severity: "success",
          message: `${operation?.result.readyCount ?? 0} ${operation?.result.readyCount === 1 ? "offer" : "offers"} updated successfully`,
        }}
        onClose={() => setShowSuccess(false)}
      />
    </PageShell>
  );
}
