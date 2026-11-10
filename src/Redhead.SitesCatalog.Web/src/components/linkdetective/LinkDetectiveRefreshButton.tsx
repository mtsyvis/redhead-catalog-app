import { useEffect, useState } from "react";
import { Box, CircularProgress, IconButton, Tooltip } from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import { BrandButton } from "../common/BrandButton";

export function LinkDetectiveRefreshButton({
  loading,
  retryAfterUtc,
  onRefresh,
  compact = false,
  label = "Refresh prices",
}: {
  loading: boolean;
  retryAfterUtc: string | null;
  onRefresh: () => void;
  compact?: boolean;
  label?: string;
}) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (!retryAfterUtc) return;
    const timer = window.setTimeout(
      () => setNow(Date.now()),
      Math.min(2_147_483_647, Math.max(0, Date.parse(retryAfterUtc) - Date.now()))
    );
    return () => window.clearTimeout(timer);
  }, [retryAfterUtc]);

  const coolingDown = Boolean(retryAfterUtc && Date.parse(retryAfterUtc) > now);
  const disabled = loading || coolingDown;
  const tooltip = [
    "Refresh LinkDetective prices. Service type and placement term are not specified by the source.",
    coolingDown && retryAfterUtc
      ? `Refresh available after ${new Date(retryAfterUtc).toLocaleString()}.`
      : null,
  ]
    .filter(Boolean)
    .join(" ");
  const icon = loading ? <CircularProgress size={16} /> : <RefreshIcon fontSize="small" />;

  return (
    <Tooltip title={tooltip} describeChild>
      <Box component="span" tabIndex={disabled ? 0 : undefined} sx={{ display: "inline-flex" }}>
        {compact ? (
          <IconButton
            aria-label="Refresh LinkDetective prices"
            disabled={disabled}
            onClick={onRefresh}
            size="small"
          >
            {icon}
          </IconButton>
        ) : (
          <BrandButton size="small" disabled={disabled} startIcon={icon} onClick={onRefresh}>
            {loading ? "Loading…" : label}
          </BrandButton>
        )}
      </Box>
    </Tooltip>
  );
}
