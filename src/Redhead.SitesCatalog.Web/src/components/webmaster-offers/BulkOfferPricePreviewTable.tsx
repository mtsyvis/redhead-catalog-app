import {
  ALL_EXCEPTION_CATEGORIES,
  BULK_PREVIEW_OUTCOME,
  BULK_PREVIEW_TABS,
} from "../../constants/bulkOfferPricePreview.constants";
import { BULK_PRICE_TYPES, STATUS_ACTIVE } from "../../constants/webmasterOffers.constants";
import { DEFAULT_CURRENCY } from "../../constants/currency.constants";
import { SERVICE_AVAILABILITY_STATUS } from "../../utils/serviceAvailability";
import { useMemo, useState, type ReactNode } from "react";
import {
  Alert,
  Box,
  Chip,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  Tabs,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import { alpha } from "@mui/material/styles";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { DEFAULT_PAGE_SIZE, PAGE_SIZE_OPTIONS } from "../../constants/pagination.constants";
import type {
  BulkOfferPriceInputRow,
  BulkOfferPricePreview,
  BulkOfferPricePreviewRow,
  BulkOfferPriceValue,
} from "../../types/bulkOfferPricePreview.types";
import { formatOfferRateSource } from "../../utils/currencyDisplay";
import { pluralize } from "../../utils/pluralize";
import { BrandButton } from "../common/BrandButton";
import { CopyToClipboardIconButton } from "../common/CopyToClipboardIconButton";
import { quoteAmountKey } from "./editWebmasterOfferForm";
import { BulkOfferPriceFollowUp } from "./BulkOfferPriceFollowUp";
import { WebmasterOffersLink } from "./WebmasterOffersLink";

const MAX_VISIBLE_INPUT_ROWS = 10;

const CATALOG_SERVICES = [
  "Main",
  "Casino",
  "Crypto",
  "Link insertion",
  "Link insertion casino",
  "Dating",
];
function CatalogImpact({ row }: { row: BulkOfferPricePreviewRow }) {
  const [open, setOpen] = useState(false);
  const changed = (row.purchaseMinima ?? []).filter(
    (key) => quoteAmountKey(key.beforeUsd) !== quoteAmountKey(key.afterUsd)
  );
  const amount = (value: string | null) =>
    value === null ? "No price" : `$${formatUsdAmount(value).replace(/\.00$/, "")}`;

  if (!row.catalogRecalculationNeeded || changed.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary">
        —
      </Typography>
    );
  }

  if (changed.length === 1 && row.currentTerm === row.proposedTerm) {
    const change = changed[0];
    return (
      <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
        {CATALOG_SERVICES[change.service]}: {amount(change.beforeUsd)} → {amount(change.afterUsd)}
      </Typography>
    );
  }

  const services = new Set(changed.map((key) => key.service)).size;
  return (
    <>
      <Stack direction="row" spacing={0.75} alignItems="center" useFlexGap flexWrap="wrap">
        <Typography variant="body2">
          {services} {pluralize(services, "service")} changed
        </Typography>
        <Button
          variant="text"
          size="small"
          aria-label={`View catalog changes for ${row.domain}`}
          onClick={() => setOpen(true)}
          sx={{ minWidth: 0, p: 0, textTransform: "none" }}
        >
          Details
        </Button>
      </Stack>
      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        fullWidth
        maxWidth="sm"
        aria-labelledby="catalog-impact-title"
      >
        <DialogTitle id="catalog-impact-title">Catalog impact</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2}>
            <Typography variant="body2" sx={{ fontWeight: 600, overflowWrap: "anywhere" }}>
              {row.domain}
            </Typography>
            {row.currentTerm !== row.proposedTerm && (
              <Typography variant="body2" color="text.secondary">
                Term corrected: {row.currentTerm} → {row.proposedTerm}
              </Typography>
            )}
            <Typography variant="body2" color="text.secondary">
              Changed minimum purchase prices across suppliers.
            </Typography>
            <TableContainer>
              <Table size="small" aria-label={`Changed purchase minima for ${row.domain}`}>
                <TableHead>
                  <TableRow>
                    <TableCell>Service</TableCell>
                    <TableCell>Term</TableCell>
                    <TableCell>Before USD</TableCell>
                    <TableCell>After USD</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {changed.map((key) => (
                    <TableRow key={`${key.service}:${key.term.termKey}`}>
                      <TableCell>{CATALOG_SERVICES[key.service]}</TableCell>
                      <TableCell>
                        {key.term.termKey === "unknown" ? "No term" : key.term.displayLabel}
                      </TableCell>
                      <TableCell sx={{ whiteSpace: "nowrap" }}>{amount(key.beforeUsd)}</TableCell>
                      <TableCell sx={{ whiteSpace: "nowrap" }}>{amount(key.afterUsd)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
            {changed.some((key) => key.isQuarantined) && (
              <Typography variant="caption" color="text.secondary">
                Site remains quarantined.
              </Typography>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <BrandButton onClick={() => setOpen(false)}>Close</BrandButton>
        </DialogActions>
      </Dialog>
    </>
  );
}

function formatUsdAmount(amount: string | null) {
  if (amount === null) return "—";
  const [whole, decimals = ""] = amount.split(".");
  return `${whole}.${decimals.padEnd(2, "0")}`;
}

function PriceValue({
  value,
  proposed = false,
  sharedConversion = false,
  showConversion = true,
}: {
  value: BulkOfferPriceValue | null;
  proposed?: boolean;
  sharedConversion?: boolean;
  showConversion?: boolean;
}) {
  if (!value)
    return (
      <Typography variant="body2" color="text.secondary">
        No price row
      </Typography>
    );
  const label =
    value.availabilityStatus === SERVICE_AVAILABILITY_STATUS.Available
      ? formatUsdAmount(value.webmasterPriceUsd)
      : value.availabilityStatus === SERVICE_AVAILABILITY_STATUS.NotAvailable
        ? "NO"
        : value.availabilityStatus === SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice
          ? "YES"
          : "Unknown";
  return (
    <Stack spacing={0.5}>
      <Typography
        variant="body2"
        color={proposed ? "text.primary" : "text.secondary"}
        sx={{ fontWeight: proposed ? 600 : 400, fontVariantNumeric: "tabular-nums" }}
      >
        {label}
      </Typography>
      {showConversion &&
        value.availabilityStatus === SERVICE_AVAILABILITY_STATUS.Available &&
        value.sourceCurrency !== DEFAULT_CURRENCY && (
          <Typography variant="caption" color="text.secondary">
            {value.sourceAmount} {value.sourceCurrency}
          </Typography>
        )}
      {showConversion &&
        value.availabilityStatus === SERVICE_AVAILABILITY_STATUS.Available &&
        !sharedConversion &&
        (value.sourceCurrency !== DEFAULT_CURRENCY || value.appliedUsdSurcharge > 0) && (
          <Box component="details">
            <Typography
              component="summary"
              variant="caption"
              color="text.secondary"
              sx={{ cursor: "pointer" }}
            >
              Saved conversion
            </Typography>
            <Typography variant="caption" component="div" color="text.secondary">
              {value.appliedUsdRate} USD per {value.sourceCurrency} ·{" "}
              {formatOfferRateSource(value.sourceCurrency, value.rateDate)}
              {value.appliedUsdSurcharge > 0 && (
                <Box component="span" display="block">
                  Includes ${value.appliedUsdSurcharge} company surcharge
                </Box>
              )}
            </Typography>
          </Box>
        )}
    </Stack>
  );
}

function OriginalInputs({
  inputs,
  label,
  reasons,
}: {
  inputs: BulkOfferPriceInputRow[];
  label: string;
  reasons?: string[];
}) {
  return (
    <Box component="details" sx={{ mt: 0.5 }}>
      <Typography
        component="summary"
        variant="caption"
        color="text.secondary"
        sx={{ cursor: "pointer" }}
      >
        {label}
      </Typography>
      {reasons && (
        <Typography variant="caption" component="div" sx={{ whiteSpace: "pre-wrap", mt: 0.5 }}>
          {reasons.join("\n")}
        </Typography>
      )}
      <Typography
        variant="caption"
        component="div"
        sx={{ whiteSpace: "pre-wrap", maxHeight: 240, overflowY: "auto", mt: 0.5 }}
      >
        {inputs
          .map(
            (input) => `Row ${input.rowNumber}: ${JSON.stringify([input.domain, ...input.values])}`
          )
          .join("\n")}
      </Typography>
    </Box>
  );
}

function DomainContext({
  row,
  showState = false,
}: {
  row: BulkOfferPricePreviewRow;
  showState?: boolean;
}) {
  const termChanged = row.currentTerm !== row.proposedTerm;
  const statusChanged = row.currentStatus !== row.proposedStatus;
  const currentStatus = row.currentStatus === STATUS_ACTIVE ? "Active" : "Inactive";
  const proposedStatus = row.proposedStatus === STATUS_ACTIVE ? "Active" : "Inactive";
  return (
    <Stack spacing={0.5}>
      <Stack direction="row" spacing={0.5} alignItems="center">
        <Typography variant="body2" sx={{ fontWeight: 600, overflowWrap: "anywhere", minWidth: 0 }}>
          {row.domain || "(blank domain)"}
        </Typography>
        {!row.invalidDomain && row.domain && <WebmasterOffersLink domain={row.domain} />}
      </Stack>
      {row.invalidDomain && (
        <Typography variant="caption" color="error">
          Invalid domain input
        </Typography>
      )}
      <Typography variant="caption" color="text.secondary">
        {pluralize(row.inputs.length, "Row")}:{" "}
        {row.inputs
          .slice(0, MAX_VISIBLE_INPUT_ROWS)
          .map((input) => input.rowNumber)
          .join(", ")}
        {row.inputs.length > MAX_VISIBLE_INPUT_ROWS
          ? ` … (+${row.inputs.length - MAX_VISIBLE_INPUT_ROWS} more)`
          : ""}
      </Typography>
      {showState && (
        <Box sx={{ pt: 0.5 }}>
          {!termChanged && !statusChanged ? (
            <Typography variant="caption" color="text.secondary">
              {row.currentTerm} · {currentStatus}
            </Typography>
          ) : (
            <>
              <Typography variant="caption" color="text.secondary" component="div">
                {termChanged ? (
                  <>
                    {row.currentTerm} →{" "}
                    <Box component="span" sx={{ color: "text.primary", fontWeight: 600 }}>
                      {row.proposedTerm}
                    </Box>
                  </>
                ) : (
                  row.currentTerm
                )}
              </Typography>
              <Typography variant="caption" color="text.secondary" component="div">
                {statusChanged ? (
                  <>
                    {currentStatus} →{" "}
                    <Box component="span" sx={{ color: "success.main", fontWeight: 600 }}>
                      {proposedStatus}
                    </Box>
                  </>
                ) : (
                  currentStatus
                )}
              </Typography>
            </>
          )}
        </Box>
      )}
      {showState && row.inputs.length > 1 && (
        <OriginalInputs inputs={row.inputs} label={`Original inputs (${row.inputs.length})`} />
      )}
    </Stack>
  );
}

const ISSUE_GUIDANCE: Record<string, string> = {
  "Conflicting input rows":
    "Different values for the same domain. Keep one consistent set of prices.",
  "Invalid domain": "Check the domain spelling or URL.",
  "Invalid selected value":
    "Check every selected cell. Main requires a positive price; other services also accept YES or NO.",
  "Site not found": "This domain is absent from the catalog. Check the spelling.",
  "Offer not found": "No offer from this contact. Check the contact or domain.",
  "Invalid structured term":
    "An existing offer has an invalid term. Review the offer before retrying.",
  "Multiple matching offers":
    "More than one offer matches. Review the offers for this contact and term.",
  "Different term": "No offer matches this term. Check the price-list term.",
  "Inconsistent price terms":
    "Existing service terms differ from the offer term. Review the offer before retrying.",
  "Conversion unavailable":
    "Conversion is unavailable. Check the currency details below and try again.",
  "Invalid converted amount":
    "The converted price is outside supported bounds. Check the input amount.",
};

export function BulkOfferPricePreviewTable({
  preview,
  onEditInput,
  saved = false,
  busy = false,
  operationDetails,
}: {
  preview: BulkOfferPricePreview;
  onEditInput?: () => void;
  saved?: boolean;
  busy?: boolean;
  operationDetails?: ReactNode;
}) {
  const [filter, setFilter] = useState<BulkOfferPricePreviewRow["outcome"]>(
    BULK_PREVIEW_OUTCOME.Ready
  );
  const [category, setCategory] = useState(ALL_EXCEPTION_CATEGORIES);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [copyError, setCopyError] = useState(false);
  const reference = preview.conversionReference;
  const usesReference = (value: BulkOfferPriceValue) =>
    !!reference &&
    value.availabilityStatus === SERVICE_AVAILABILITY_STATUS.Available &&
    value.sourceCurrency === reference.sourceCurrency &&
    value.appliedUsdRate === reference.appliedUsdRate &&
    value.rateDate === reference.rateDate &&
    value.appliedUsdSurcharge === reference.appliedUsdSurcharge;
  const exceptions = useMemo(
    () => preview.rows.filter((row) => row.outcome === BULK_PREVIEW_OUTCOME.Exceptions),
    [preview]
  );
  const categories = useMemo(
    () => [...new Set(exceptions.map((row) => row.category!))],
    [exceptions]
  );
  const visible = useMemo(
    () =>
      preview.rows.filter(
        (row) =>
          row.outcome === filter &&
          (filter !== BULK_PREVIEW_OUTCOME.Exceptions ||
            category === ALL_EXCEPTION_CATEGORIES ||
            row.category === category)
      ),
    [preview, filter, category]
  );
  const paged = visible.slice(page * pageSize, (page + 1) * pageSize);
  const activeTabId = BULK_PREVIEW_TABS.find((tab) => tab.value === filter)!.id;
  const copy = (domains: string[], label: string) => (
    <Stack direction="row" alignItems="center" spacing={1}>
      <Typography variant="body2">
        {label} ({domains.length})
      </Typography>
      <CopyToClipboardIconButton
        value={domains.join("\n")}
        copyLabel={label}
        copiedLabel="Copied"
        onCopyError={() => setCopyError(true)}
        onCopySuccess={() => setCopyError(false)}
      />
    </Stack>
  );

  return (
    <Stack spacing={2}>
      <Stack
        direction="row"
        spacing={1.5}
        useFlexGap
        flexWrap="wrap"
        alignItems="center"
        justifyContent="space-between"
      >
        <Stack
          direction="row"
          spacing={1}
          useFlexGap
          flexWrap="wrap"
          alignItems="center"
          sx={{ minWidth: 0 }}
        >
          <Tooltip title={preview.primaryEmail}>
            <Typography
              variant="body2"
              noWrap
              sx={{ fontWeight: 500, maxWidth: { xs: 220, md: 400 } }}
            >
              {preview.primaryEmail}
            </Typography>
          </Tooltip>
          <Chip size="small" label={preview.term} />
          <Chip size="small" label={preview.sourceCurrency} />
        </Stack>
        <Stack
          direction="row"
          spacing={1.5}
          useFlexGap
          flexWrap="wrap"
          alignItems="center"
          sx={{ maxWidth: "100%" }}
        >
          {operationDetails}
          {onEditInput && (
            <BrandButton
              disabled={busy}
              size="small"
              startIcon={<ArrowBackIcon />}
              onClick={onEditInput}
            >
              Edit input
            </BrandButton>
          )}
        </Stack>
      </Stack>
      <BulkOfferPriceFollowUp preview={preview} saved={saved} />
      <Stack
        direction={{ xs: "column", sm: "row" }}
        alignItems={{ xs: "stretch", sm: "center" }}
        spacing={1}
        sx={{ borderBottom: 1, borderColor: "divider" }}
      >
        <Tabs
          value={filter}
          aria-label={saved ? "Saved result categories" : "Preview categories"}
          variant="scrollable"
          scrollButtons="auto"
          textColor="primary"
          onChange={(_, value: BulkOfferPricePreviewRow["outcome"]) => {
            setFilter(value);
            setPage(0);
            setCopyError(false);
          }}
          sx={{
            minHeight: 42,
            flex: 1,
            minWidth: 0,
            borderBottom: 0,
            borderColor: "divider",
            "& .MuiTab-root": { minHeight: 42, px: 1.5, color: "text.secondary", opacity: 1 },
            "& .MuiTab-root.Mui-selected": { color: "primary.main", fontWeight: 600, opacity: 1 },
          }}
        >
          {BULK_PREVIEW_TABS.map(({ value, label, countKey, id }) => (
            <Tab
              key={value}
              value={value}
              id={id}
              aria-controls="bulk-preview-results"
              aria-label={`${saved && value === BULK_PREVIEW_OUTCOME.Ready ? "Updated" : label}: ${preview[countKey]}`}
              label={
                <Stack component="span" direction="row" spacing={0.75} alignItems="center">
                  <Typography component="span" variant="body2">
                    {saved && value === BULK_PREVIEW_OUTCOME.Ready ? "Updated" : label}
                  </Typography>
                  <Box
                    component="span"
                    sx={{
                      px: 0.75,
                      minWidth: 24,
                      borderRadius: 999,
                      bgcolor: "action.hover",
                      fontSize: "0.75rem",
                      fontVariantNumeric: "tabular-nums",
                    }}
                  >
                    {preview[countKey].toLocaleString("en-US")}
                  </Box>
                </Stack>
              }
            />
          ))}
        </Tabs>
        <Typography
          variant="caption"
          color="text.secondary"
          sx={{ textAlign: "right", flexShrink: 0, pb: { xs: 1, sm: 0 } }}
        >
          {preview.inputCount.toLocaleString("en-US")} input rows
          {preview.duplicateInputCount > 0 &&
            ` · ${preview.duplicateInputCount.toLocaleString("en-US")} identical ${preview.duplicateInputCount === 1 ? "duplicate" : "duplicates"} removed`}
        </Typography>
      </Stack>
      <Stack spacing={2} role="tabpanel" id="bulk-preview-results" aria-labelledby={activeTabId}>
        {filter === BULK_PREVIEW_OUTCOME.Ready &&
          visible.length > 0 &&
          reference &&
          reference.sourceCurrency !== DEFAULT_CURRENCY && (
            <Box component="details">
              <Typography
                component="summary"
                variant="caption"
                color="text.secondary"
                sx={{ cursor: "pointer" }}
              >
                New {reference.sourceCurrency} prices: 1 {reference.sourceCurrency} ={" "}
                {reference.appliedUsdRate} USD
                {reference.appliedUsdSurcharge > 0 && (
                  <> · +${reference.appliedUsdSurcharge} per price</>
                )}
              </Typography>
              <Typography variant="caption" component="div" color="text.secondary" sx={{ mt: 0.5 }}>
                {formatOfferRateSource(reference.sourceCurrency, reference.rateDate)}. Convert and
                round first
                {reference.appliedUsdSurcharge > 0
                  ? `, then add $${reference.appliedUsdSurcharge} to each new numeric quote.`
                  : "."}
              </Typography>
            </Box>
          )}
        {filter === BULK_PREVIEW_OUTCOME.Exceptions && exceptions.length > 0 && (
          <Stack
            direction="row"
            spacing={2}
            useFlexGap
            flexWrap="wrap"
            alignItems="center"
            justifyContent="space-between"
          >
            <TextField
              select
              size="small"
              label="Exception category"
              value={category}
              sx={{ minWidth: 250 }}
              onChange={(event) => {
                setCategory(event.target.value);
                setPage(0);
                setCopyError(false);
              }}
            >
              <MenuItem value={ALL_EXCEPTION_CATEGORIES}>
                All categories ({exceptions.length})
              </MenuItem>
              {categories.map((reason) => (
                <MenuItem key={reason} value={reason}>
                  {reason} ({exceptions.filter((row) => row.category === reason).length})
                </MenuItem>
              ))}
            </TextField>
            <Stack direction="row" spacing={2} useFlexGap flexWrap="wrap" alignItems="center">
              {copy(
                visible.map((row) => row.domain),
                category === ALL_EXCEPTION_CATEGORIES
                  ? "Copy all exception domains"
                  : "Copy filtered domains"
              )}
            </Stack>
          </Stack>
        )}
        {copyError && <Alert severity="error">Could not copy domains. Try again.</Alert>}
        {visible.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ py: 4, textAlign: "center" }}>
            No domains in this category.
          </Typography>
        ) : (
          <>
            <TableContainer
              sx={{
                border: 1,
                borderColor: "divider",
                borderRadius: 1,
                bgcolor: "background.paper",
              }}
            >
              <Table
                size="small"
                aria-label={saved ? "Saved price update result" : "Price update preview"}
              >
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ width: "31%" }}>Domain</TableCell>
                    {filter === BULK_PREVIEW_OUTCOME.Exceptions ? (
                      <TableCell>Issue</TableCell>
                    ) : (
                      <>
                        <TableCell>Service</TableCell>
                        <TableCell>{saved ? "Before · USD" : "Current · USD"}</TableCell>
                        <TableCell>{saved ? "After · USD" : "Proposed · USD"}</TableCell>
                        <TableCell>Catalog impact</TableCell>
                      </>
                    )}
                  </TableRow>
                </TableHead>
                {filter === BULK_PREVIEW_OUTCOME.Exceptions ? (
                  <TableBody>
                    {paged.map((row, index) => (
                      <TableRow key={index} sx={{ verticalAlign: "top" }}>
                        <TableCell sx={{ minWidth: 200, maxWidth: 320 }}>
                          <DomainContext row={row} />
                        </TableCell>
                        <TableCell sx={{ maxWidth: 650, overflowWrap: "anywhere" }}>
                          <Typography variant="body2" color="warning.dark" sx={{ fontWeight: 500 }}>
                            {row.category}
                          </Typography>
                          <Typography variant="body2">
                            {ISSUE_GUIDANCE[row.category ?? ""] ?? row.reasons[0]}
                          </Typography>
                          <OriginalInputs
                            inputs={row.inputs}
                            reasons={row.reasons}
                            label="Details and original values"
                          />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                ) : (
                  paged.map((row, index) => (
                    <TableBody
                      key={index}
                      aria-label={row.domain}
                      sx={{
                        "&:not(:first-of-type) > tr:first-of-type > td": {
                          borderTop: "2px solid",
                          borderTopColor: "divider",
                        },
                      }}
                    >
                      {row.prices.map((price, priceIndex) => (
                        <TableRow key={price.priceType} sx={{ verticalAlign: "top" }}>
                          {priceIndex === 0 && (
                            <TableCell
                              rowSpan={row.prices.length}
                              sx={{
                                minWidth: 220,
                                maxWidth: 320,
                                borderRight: 1,
                                borderRightColor: "divider",
                              }}
                            >
                              <DomainContext row={row} showState />
                            </TableCell>
                          )}
                          <TableCell sx={{ minWidth: 150 }}>
                            <Typography variant="body2">
                              {
                                BULK_PRICE_TYPES.find((type) => type.value === price.priceType)
                                  ?.label
                              }
                            </Typography>
                          </TableCell>
                          <TableCell sx={{ minWidth: 180, maxWidth: 320 }}>
                            <PriceValue
                              value={price.current}
                              showConversion={filter === BULK_PREVIEW_OUTCOME.Ready}
                            />
                          </TableCell>
                          <TableCell
                            sx={(theme) => ({
                              minWidth: 180,
                              maxWidth: 320,
                              bgcolor: price.changed
                                ? alpha(theme.palette.success.main, 0.08)
                                : undefined,
                            })}
                          >
                            <PriceValue
                              value={price.proposed}
                              proposed
                              sharedConversion={usesReference(price.proposed)}
                              showConversion={filter === BULK_PREVIEW_OUTCOME.Ready}
                            />
                            {!price.changed && (
                              <Typography variant="caption" color="text.secondary">
                                Unchanged
                              </Typography>
                            )}
                            {price.changed &&
                              price.current?.availabilityStatus ===
                                price.proposed.availabilityStatus &&
                              quoteAmountKey(price.current?.webmasterPriceUsd) ===
                                quoteAmountKey(price.proposed.webmasterPriceUsd) && (
                                <Typography variant="caption" color="text.secondary">
                                  Source quote changed
                                </Typography>
                              )}
                          </TableCell>
                          {priceIndex === 0 && (
                            <TableCell
                              rowSpan={row.prices.length}
                              sx={{ minWidth: 180, maxWidth: 480 }}
                            >
                              <CatalogImpact row={row} />
                            </TableCell>
                          )}
                        </TableRow>
                      ))}
                    </TableBody>
                  ))
                )}
              </Table>
            </TableContainer>
            <Stack
              direction="row"
              alignItems="center"
              justifyContent="flex-end"
              useFlexGap
              flexWrap="wrap"
              spacing={1}
            >
              <TablePagination
                component="div"
                count={visible.length}
                page={page}
                rowsPerPage={pageSize}
                labelRowsPerPage="Domains per page:"
                rowsPerPageOptions={PAGE_SIZE_OPTIONS}
                onPageChange={(_, next) => setPage(next)}
                onRowsPerPageChange={(event) => {
                  setPageSize(Number(event.target.value));
                  setPage(0);
                }}
              />
            </Stack>
          </>
        )}
      </Stack>
    </Stack>
  );
}
