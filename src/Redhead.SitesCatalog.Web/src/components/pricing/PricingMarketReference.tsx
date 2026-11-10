import { marketDescription } from "../../utils/pricingDisplay";
import { useEffect, useRef, useState } from "react";
import { IconButton, Stack, Tooltip, Typography } from "@mui/material";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import type { PricingMarket } from "../../types/pricing.types";
import { formatUsd } from "../../utils/numberFormat";
import { webmasterOffersService } from "../../services/webmasterOffers.service";
import { LinkDetectiveRefreshButton } from "../linkdetective/LinkDetectiveRefreshButton";

export function PricingMarketReference({
  domain,
  market,
  onUpdated,
}: {
  domain: string;
  market: PricingMarket | null;
  onUpdated: (market: PricingMarket) => void;
}) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const mounted = useRef(false);
  const busy = useRef(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  const refresh = async () => {
    if (busy.current || !market?.canRefresh) return;
    busy.current = true;
    setLoading(true);
    setError(null);
    try {
      const result = await webmasterOffersService.getLinkDetectivePrices(domain, true);
      onUpdated({
        minimumUsd: result.minimumUsd,
        averageUsd: result.averageUsd,
        medianUsd: result.medianUsd,
        checkedAtUtc: result.checkedAtUtc,
        isStale: result.isStale,
        lastError: result.lastError ?? null,
        retryAfterUtc: result.retryAfterUtc,
        canRefresh: market.canRefresh,
      });
    } catch {
      if (mounted.current) setError("Market prices could not be updated. Try again.");
    } finally {
      busy.current = false;
      if (mounted.current) setLoading(false);
    }
  };

  return (
    <Stack
      direction="row"
      flexWrap="wrap"
      columnGap={1.5}
      rowGap={0.5}
      alignItems="center"
      sx={{ p: 1, border: 1, borderColor: "divider", borderRadius: 1 }}
    >
      <Typography variant="body2" fontWeight={600}>
        LinkDetective
      </Typography>
      {market?.checkedAtUtc &&
        (
          [
            ["Minimum", market.minimumUsd],
            ["Average", market.averageUsd],
            ["Median", market.medianUsd],
          ] as const
        ).map(([label, amount]) => (
          <Typography key={label} variant="body2">
            <Typography component="span" variant="caption" color="text.secondary">
              {label}{" "}
            </Typography>
            {formatUsd(amount)}
          </Typography>
        ))}
      {market?.checkedAtUtc ? (
        <Tooltip title={marketDescription(market)}>
          <Typography variant="caption" color="text.secondary" tabIndex={0} sx={{ ml: "auto" }}>
            Retrieved:{" "}
            {new Date(market.checkedAtUtc).toLocaleDateString("en-GB", {
              day: "numeric",
              month: "short",
              year: "numeric",
            })}
            {market.isStale ? " · Outdated" : ""}
          </Typography>
        </Tooltip>
      ) : (
        <Typography variant="caption" color="text.secondary">
          {market ? "No successful check yet" : "Not checked yet"}
        </Typography>
      )}
      {market?.canRefresh && (
        <LinkDetectiveRefreshButton
          compact
          loading={loading}
          retryAfterUtc={market.retryAfterUtc}
          onRefresh={() => void refresh()}
        />
      )}
      {market?.checkedAtUtc && market.minimumUsd == null && (
        <Typography variant="caption" color="text.secondary">
          No market prices found
        </Typography>
      )}
      {(error || market?.lastError) && (
        <Typography variant="caption" color="warning.dark" role="status">
          {error || `Last check failed: ${market?.lastError}`}
        </Typography>
      )}
      {!market?.canRefresh && (
        <Tooltip
          title="Refresh unavailable: LinkDetective is not configured. Saved market prices remain available."
          describeChild
        >
          <IconButton
            size="small"
            aria-label="LinkDetective refresh unavailable"
            sx={{ color: "text.secondary" }}
          >
            <InfoOutlinedIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      )}
    </Stack>
  );
}
