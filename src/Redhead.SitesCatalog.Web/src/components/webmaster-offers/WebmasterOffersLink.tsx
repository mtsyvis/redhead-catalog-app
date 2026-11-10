import { IconButton, Tooltip } from "@mui/material";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";

export function WebmasterOffersLink({ domain }: { domain: string }) {
  return (
    <Tooltip title="Open webmaster offers">
      <IconButton
        href={`/webmaster-offers?domain=${encodeURIComponent(domain)}`}
        target="_blank"
        rel="noopener noreferrer"
        aria-label={`Open webmaster offers for ${domain} in a new tab`}
        sx={{ width: 36, height: 36, flexShrink: 0 }}
      >
        <OpenInNewIcon fontSize="small" />
      </IconButton>
    </Tooltip>
  );
}
