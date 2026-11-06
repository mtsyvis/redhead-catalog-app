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

const offerExplanation = "Lowest Active Webmaster Offer price for the same service and term.";
const alignedValueSx = { minHeight: 40, display: "flex", alignItems: "center" };

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
  const originalAmounts = createPricingDraft(site).amounts;
  return (
    <Box sx={{ overflowX: "auto" }}>
      <Table size="small" sx={{ minWidth: 720 }} aria-label="Catalog price editor">
        <TableHead>
          <TableRow>
            <TableCell>Price type</TableCell>
            <TableCell>Availability</TableCell>
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
              <TableRow key={type} sx={{ "& > td": { verticalAlign: "top" } }}>
                <TableCell>
                  <Typography variant="body2" sx={alignedValueSx}>
                    {PRICE_TYPE_LABELS[type]}
                  </Typography>
                </TableCell>
                <TableCell>
                  {type !== PRICE_TYPE.Main ? (
                    <TextField
                      select
                      size="small"
                      value={statuses[type] ?? 0}
                      disabled={disabled}
                      sx={{ width: 125, flexShrink: 0 }}
                      slotProps={{
                        select: {
                          inputProps: { "aria-label": `${PRICE_TYPE_LABELS[type]} availability` },
                        },
                      }}
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
                  ) : (
                    <Box sx={{ ...alignedValueSx, color: "text.secondary" }}>—</Box>
                  )}
                </TableCell>
                <TableCell>
                  <TextField
                    size="small"
                    value={text}
                    error={Boolean(error)}
                    disabled={disabled}
                    sx={{ width: 145 }}
                    slotProps={{
                      htmlInput: {
                        inputMode: "decimal",
                        "aria-label": `${PRICE_TYPE_LABELS[type]} price`,
                      },
                    }}
                    helperText={
                      error
                        ? error
                        : changed
                          ? `Was ${formatUsd(originalAmounts[key] ? Number(originalAmounts[key]) : null)}`
                          : undefined
                    }
                    onChange={(event) => onAmountChange(type, event.target.value)}
                  />
                </TableCell>
                <TableCell align="right">
                  <Box sx={{ ...alignedValueSx, justifyContent: "flex-end" }}>
                    {formatUsd(comparison?.offerPrice)}
                  </Box>
                </TableCell>
                <TableCell align="right">
                  <Box sx={{ ...alignedValueSx, justifyContent: "flex-end" }}>
                    <MarginAmount amount={margin} label={false} />
                  </Box>
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </Box>
  );
}
