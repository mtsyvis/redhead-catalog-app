import { DEFAULT_PAGE_SIZE } from "../constants/pagination.constants";
import { useEffect, useState } from "react";
import { Navigate } from "react-router-dom";
import {
  Alert,
  Box,
  Checkbox,
  FormControlLabel,
  MenuItem,
  Paper,
  Snackbar,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { PageShell } from "../components/layout/PageShell";
import { BrandButton } from "../components/common/BrandButton";
import { QuarantineFilter } from "../components/common/QuarantineFilter";
import { SearchInput } from "../components/common/SearchInput";
import { PricingTable } from "../components/pricing/PricingTable";
import { EditCatalogPricesDialog } from "../components/pricing/EditCatalogPricesDialog";
import { TableViewToolbar } from "../components/common/table-views/TableViewToolbar";
import { pricingColumnGroups } from "../components/pricing/pricingColumns";
import { formatInteger } from "../utils/numberFormat";
import { usePricingTableViews } from "../components/pricing/usePricingTableViews";
import { useUserRoles } from "../hooks/useUserRoles";
import { pricingService } from "../services/pricing.service";
import type { PricingPage, PricingQuery, PricingTerm } from "../types/pricing.types";
import { PRICE_TYPE_LABELS, PRICING_SECTION_ORDER, type PriceTypeValue } from "../utils/pricing";

const defaultQuery: PricingQuery = {
  domain: "",
  termKey: "",
  needsReview: false,
  priceType: 0,
  margin: "all",
  sortBy: "domain",
  sortPriceType: 0,
  descending: false,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
};

export function Pricing() {
  const { canReadPricing, canEditPricing } = useUserRoles();
  const columns = usePricingTableViews(canReadPricing);
  const [query, setQuery] = useState(defaultQuery);
  const [search, setSearch] = useState("");
  const [data, setData] = useState<PricingPage | null>(null);
  const [terms, setTerms] = useState<PricingTerm[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [termsError, setTermsError] = useState<string | null>(null);
  const [termsRefresh, setTermsRefresh] = useState(0);
  const [refresh, setRefresh] = useState(0);
  const [editing, setEditing] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [minText, setMinText] = useState("");
  const [maxText, setMaxText] = useState("");
  const minMargin = minText.trim() === "" ? undefined : Number(minText);
  const maxMargin = maxText.trim() === "" ? undefined : Number(maxText);
  const minError =
    minMargin !== undefined && !Number.isFinite(minMargin)
      ? "Enter a valid USD amount."
      : undefined;
  const maxError =
    maxMargin !== undefined && !Number.isFinite(maxMargin)
      ? "Enter a valid USD amount."
      : !minError && minMargin !== undefined && maxMargin !== undefined && minMargin > maxMargin
        ? "Must be greater than or equal to the lower bound."
        : undefined;
  const invalidRange = Boolean(minError || maxError);
  const hasRange = minMargin !== undefined || maxMargin !== undefined;
  const marginWarning = invalidRange
    ? null
    : query.margin === "missing" && hasRange
      ? "Missing margins cannot match a numeric range. Clear the range or choose another margin filter."
      : query.needsReview && (query.margin === "nonPositive" || hasRange)
        ? "Sites with multiple terms have no calculated margin. These margin filters cannot match them."
        : query.margin === "nonPositive" && minMargin !== undefined && minMargin > 0
          ? "Negative & Zero cannot match a range above $0. Adjust the range or choose another margin filter."
          : null;
  const changeQuery = (next: Partial<PricingQuery>) =>
    setQuery((current) => ({ ...current, ...next }));

  useEffect(() => {
    if (!canReadPricing) return;
    const timer = window.setTimeout(
      () =>
        setQuery((current) =>
          current.domain === search.trim()
            ? current
            : { ...current, domain: search.trim(), page: 1 }
        ),
      300
    );
    return () => window.clearTimeout(timer);
  }, [search, canReadPricing]);
  useEffect(() => {
    if (!canReadPricing || invalidRange) return;
    const timer = window.setTimeout(
      () =>
        setQuery((current) => {
          return current.minMargin === minMargin && current.maxMargin === maxMargin
            ? current
            : { ...current, minMargin, maxMargin, page: 1 };
        }),
      300
    );
    return () => window.clearTimeout(timer);
  }, [minMargin, maxMargin, invalidRange, canReadPricing]);
  useEffect(() => {
    if (!canReadPricing) return;
    let active = true;
    const timer = window.setTimeout(() => {
      setLoading(true);
      pricingService
        .search(query)
        .then((result) => {
          if (!active) return;
          const lastPage = Math.max(1, Math.ceil(result.totalCount / query.pageSize));
          if (query.page > lastPage) {
            setQuery((current) => ({ ...current, page: lastPage }));
            return;
          }
          setData(result);
          setError(null);
        })
        .catch((reason) => {
          if (active)
            setError(reason instanceof Error ? reason.message : "Failed to load catalog pricing.");
        })
        .finally(() => {
          if (active) setLoading(false);
        });
    }, 0);
    return () => {
      active = false;
      window.clearTimeout(timer);
    };
  }, [query, refresh, canReadPricing]);
  useEffect(() => {
    if (!canReadPricing) return;
    let active = true;
    pricingService
      .terms()
      .then((result) => {
        if (active) {
          setTerms(result);
          setTermsError(null);
        }
      })
      .catch((reason) => {
        if (active)
          setTermsError(
            reason instanceof Error ? reason.message : "Failed to load placement terms."
          );
      });
    return () => {
      active = false;
    };
  }, [canReadPricing, refresh, termsRefresh]);

  if (!canReadPricing) return <Navigate to="/sites" replace />;
  return (
    <PageShell title="Pricing" maxWidth={false}>
      <Stack spacing={2}>
        {error && <Alert severity="error">{error}</Alert>}
        {termsError && (
          <Alert
            severity="error"
            action={
              <BrandButton size="small" onClick={() => setTermsRefresh((value) => value + 1)}>
                Retry terms
              </BrandButton>
            }
          >
            {termsError}
          </Alert>
        )}
        {columns.loadError && <Alert severity="warning">{columns.loadError}</Alert>}
        <Paper variant="outlined" sx={{ p: 2 }}>
          <Stack spacing={2}>
            <Box
              sx={{
                display: { xs: "grid", sm: "flex" },
                gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
                flexWrap: "wrap",
                alignItems: "center",
                gap: 1.5,
              }}
            >
              <SearchInput
                searchSize="compact"
                placeholder="Search domains…"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                onClear={() => setSearch("")}
                inputProps={{ "aria-label": "Search domains" }}
                sx={{ gridColumn: "1 / -1", width: { lg: 360 } }}
              />
              <QuarantineFilter
                labelPlacement="outlined"
                value={
                  query.isQuarantined == null ? "all" : query.isQuarantined ? "only" : "exclude"
                }
                onChange={(value) =>
                  changeQuery({
                    isQuarantined: value === "all" ? undefined : value === "only",
                    page: 1,
                  })
                }
              />
              <TextField
                select
                label="Term"
                size="small"
                sx={{ width: { xs: "100%", sm: 160 }, minWidth: 0 }}
                value={query.termKey}
                slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
                onChange={(event) => changeQuery({ termKey: event.target.value, page: 1 })}
              >
                <MenuItem value="">Any term</MenuItem>
                {terms.map((term) => (
                  <MenuItem key={term.termKey} value={term.termKey}>
                    {term.label}
                  </MenuItem>
                ))}
              </TextField>
              <FormControlLabel
                control={
                  <Checkbox
                    checked={query.needsReview}
                    onChange={(_, checked) => changeQuery({ needsReview: checked, page: 1 })}
                  />
                }
                label="Multiple terms"
                sx={{ m: 0, whiteSpace: { sm: "nowrap" } }}
              />
              <BrandButton
                sx={{ ml: { sm: "auto" }, justifySelf: "end" }}
                onClick={() => {
                  setSearch("");
                  setMinText("");
                  setMaxText("");
                  changeQuery({
                    domain: defaultQuery.domain,
                    isQuarantined: undefined,
                    termKey: defaultQuery.termKey,
                    needsReview: defaultQuery.needsReview,
                    priceType: defaultQuery.priceType,
                    margin: defaultQuery.margin,
                    minMargin: undefined,
                    maxMargin: undefined,
                    page: 1,
                  });
                }}
              >
                Clear all filters
              </BrandButton>
            </Box>
            <Box component="fieldset" sx={{ m: 0, p: 0, border: 0, minWidth: 0 }}>
              <Typography
                component="legend"
                variant="caption"
                color="text.secondary"
                sx={{ px: 0, mb: 1, fontWeight: 500 }}
              >
                Margin (USD)
              </Typography>
              <Box
                sx={{
                  display: { xs: "grid", sm: "flex" },
                  gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
                  flexWrap: "wrap",
                  alignItems: "flex-start",
                  gap: 1.5,
                  "@media (max-width: 359px)": { gridTemplateColumns: "minmax(0, 1fr)" },
                }}
              >
                <TextField
                  select
                  label="Price type"
                  size="small"
                  value={query.priceType}
                  sx={{ width: { xs: "100%", sm: 180 }, minWidth: 0 }}
                  onChange={(event) =>
                    changeQuery({
                      priceType: Number(event.target.value) as PriceTypeValue,
                      page: 1,
                    })
                  }
                >
                  {PRICING_SECTION_ORDER.map((type) => (
                    <MenuItem key={type} value={type}>
                      {PRICE_TYPE_LABELS[type]}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField
                  select
                  label="Condition"
                  size="small"
                  value={query.margin}
                  sx={{ width: { xs: "100%", sm: 190 }, minWidth: 0 }}
                  onChange={(event) =>
                    changeQuery({ margin: event.target.value as PricingQuery["margin"], page: 1 })
                  }
                >
                  <MenuItem value="all">Any margin</MenuItem>
                  <MenuItem value="nonPositive">Negative &amp; Zero</MenuItem>
                  <MenuItem value="missing">Missing</MenuItem>
                </TextField>
                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: "minmax(0, 1fr) auto minmax(0, 1fr)",
                    alignItems: "start",
                    gap: 0.75,
                    width: { xs: "100%", sm: 240 },
                    gridColumn: "1 / -1",
                  }}
                >
                  <TextField
                    label="Min"
                    size="small"
                    value={minText}
                    error={Boolean(minError)}
                    helperText={minError}
                    onChange={(event) => setMinText(event.target.value)}
                    slotProps={{
                      inputLabel: { shrink: true },
                      htmlInput: { inputMode: "decimal", "aria-label": "Minimum margin in USD" },
                    }}
                    sx={{ minWidth: 0 }}
                  />
                  <Typography aria-hidden="true" color="text.secondary" sx={{ pt: 1 }}>
                    —
                  </Typography>
                  <TextField
                    label="Max"
                    size="small"
                    value={maxText}
                    error={Boolean(maxError)}
                    helperText={maxError}
                    onChange={(event) => setMaxText(event.target.value)}
                    slotProps={{
                      inputLabel: { shrink: true },
                      htmlInput: { inputMode: "decimal", "aria-label": "Maximum margin in USD" },
                    }}
                    sx={{ minWidth: 0 }}
                  />
                </Box>
              </Box>
            </Box>
            {invalidRange && (
              <Alert severity="error">
                The entered range has not been applied. The table still uses the last valid margin
                range.
              </Alert>
            )}
            {marginWarning && <Alert severity="warning">{marginWarning}</Alert>}
          </Stack>
        </Paper>
        <PricingTable
          data={data}
          query={query}
          loading={loading}
          columnIds={columns.visibleColumnIds}
          canEdit={canEditPricing}
          density={columns.density}
          columnWidths={columns.columnWidths}
          toolbar={
            <TableViewToolbar
              tableViews={columns}
              columnGroups={pricingColumnGroups}
              resultLabel={`${formatInteger(data?.totalCount ?? 0)} ${(data?.totalCount ?? 0) === 1 ? "result" : "results"}`}
              resultLoading={loading}
              onSuccess={setNotice}
              onError={setError}
            />
          }
          onQuery={changeQuery}
          onEdit={setEditing}
        />
      </Stack>
      {editing && (
        <EditCatalogPricesDialog
          key={editing}
          domain={editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            setNotice("Catalog prices saved");
            setRefresh((value) => value + 1);
          }}
        />
      )}
      <Snackbar
        open={notice != null}
        autoHideDuration={3500}
        onClose={() => setNotice(null)}
        message={notice ?? ""}
      />
    </PageShell>
  );
}
