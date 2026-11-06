import { marketDescription } from "../../utils/pricingDisplay";
import { Stack, Tooltip, Typography } from "@mui/material";
import type { PricingMarket } from "../../types/pricing.types";
import { formatUsd } from "../../utils/numberFormat";

export function PricingMarketReference({ market }: { market: PricingMarket | null }) {
  return (
    <Stack
      direction="row"
      flexWrap="wrap"
      columnGap={2}
      rowGap={0.5}
      alignItems="baseline"
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
            Retrieved: {new Date(market.checkedAtUtc).toLocaleDateString()}
            {market.isStale ? " · Outdated" : ""}
          </Typography>
        </Tooltip>
      ) : (
        <Typography variant="caption" color="text.secondary">
          {market ? "No successful check yet" : "Not checked yet"}
        </Typography>
      )}
      {market?.checkedAtUtc && market.minimumUsd == null && (
        <Typography variant="caption" color="text.secondary">
          No market prices found
        </Typography>
      )}
      {market?.lastError && (
        <Typography variant="caption" color="warning.dark">
          Last check failed: {market.lastError}
        </Typography>
      )}
    </Stack>
  );
}
