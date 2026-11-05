import {
  Box,
  IconButton,
  MenuItem,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import { MarginAmount } from "../common/MarginAmount";
import type { PricingSite } from "../../types/pricing.types";
import type { ServiceAvailabilityStatusValue } from "../../types/sites.types";
import { parseCatalogPriceAmount } from "../../utils/catalogPriceAmount";
import {
  createPricingDraft,
  getPricingTerms,
  priceCellKey,
  type CatalogPricingDraft,
} from "./EditCatalogPricesDialog.helpers";
import {
  PRICE_TYPE,
  PRICE_TYPE_LABELS,
  PRICING_SECTION_ORDER,
  type PriceTypeValue,
} from "../../utils/pricing";
import { SERVICE_AVAILABILITY_STATUS_OPTIONS } from "../../utils/serviceAvailability";
import { formatUsd } from "../../utils/numberFormat";

const offerExplanation =
  "Lowest Active Webmaster Offer price for the same service and term.";
export function CatalogPricingFields({
  site,
  termKey,
  amounts,
  statuses,
  disabled,
  onAmountChange,
  onStatusChange,
}: {
  site: PricingSite;
  termKey: string;
  amounts: Record<string, string>;
  statuses: CatalogPricingDraft["statuses"];
  disabled: boolean;
  onAmountChange: (type: PriceTypeValue, value: string) => void;
  onStatusChange: (type: PriceTypeValue, status: ServiceAvailabilityStatusValue) => void;
}) {
  const terms = getPricingTerms(site);
  const originalAmounts = createPricingDraft(site).amounts;
  return (
    <Box sx={{ overflowX: "auto" }}>
      <Table size="small" sx={{ minWidth: 650 }} aria-label="Catalog price editor">
        <TableHead>
          <TableRow>
            <TableCell>Price type</TableCell>
            <TableCell>Catalog price, USD</TableCell>
            <TableCell align="right">
              <Stack direction="row" justifyContent="flex-end" alignItems="center">
                Offer price
                <Tooltip title={offerExplanation}>
                  <IconButton size="small" aria-label="About Offer price">
                    <InfoOutlinedIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </Stack>
            </TableCell>
            <TableCell align="right">Margin after save</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {PRICING_SECTION_ORDER.map((type) => {
            const key = priceCellKey(type, termKey);
            const text = amounts[key] ?? "";
            const comparison = site.prices.find(
              (price) => price.priceType === type && price.termKey === termKey
            );
            const { amount, error } = parseCatalogPriceAmount(text);
            const margin =
              amount != null && comparison?.offerPrice != null
                ? amount - comparison.offerPrice
                : null;
            const changed = text !== (originalAmounts[key] ?? "");
            return (
              <TableRow key={type}>
                <TableCell>
                  <Typography variant="body2">{PRICE_TYPE_LABELS[type]}</Typography>
                </TableCell>
                <TableCell>
                  <Stack direction="row" spacing={1} alignItems="flex-start">
                    {type !== PRICE_TYPE.Main && (
                      <TextField
                        select
                        label="Availability"
                        size="small"
                        value={statuses[type] ?? 0}
                        disabled={disabled}
                        sx={{ width: 125, flexShrink: 0 }}
                        onChange={(event) =>
                          onStatusChange(
                            type,
                            Number(event.target.value) as ServiceAvailabilityStatusValue
                          )
                        }
                      >
                        {SERVICE_AVAILABILITY_STATUS_OPTIONS.map((option) => (
                          <MenuItem key={option.value} value={option.value}>
                            {option.label}
                          </MenuItem>
                        ))}
                      </TextField>
                    )}
                    <TextField
                      size="small"
                      label={`${PRICE_TYPE_LABELS[type]} price`}
                      value={text}
                      error={Boolean(error)}
                      disabled={disabled}
                      sx={{ width: 145 }}
                      slotProps={{ htmlInput: { inputMode: "decimal" } }}
                      helperText={
                        error
                          ? error
                          : changed
                            ? `Was ${formatUsd(originalAmounts[key] ? Number(originalAmounts[key]) : null)}`
                            : " "
                      }
                      onChange={(event) => onAmountChange(type, event.target.value)}
                    />
                  </Stack>
                </TableCell>
                <TableCell align="right">
                  <Tooltip
                    title={
                      comparison?.offerId
                        ? `Active offer · ${comparison.offerId} · ${terms.find((term) => term.termKey === termKey)?.label}`
                        : "No matching Active numeric offer"
                    }
                  >
                    <span>{formatUsd(comparison?.offerPrice)}</span>
                  </Tooltip>
                </TableCell>
                <TableCell align="right">
                  <MarginAmount amount={margin} label={false} />
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </Box>
  );
}
