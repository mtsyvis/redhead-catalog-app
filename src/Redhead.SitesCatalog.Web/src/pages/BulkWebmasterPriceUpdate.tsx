import { useEffect, useMemo, useRef, useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import {
  Alert,
  Box,
  Checkbox,
  FormControlLabel,
  IconButton,
  MenuItem,
  Stack,
  Step,
  StepLabel,
  Stepper,
  Tooltip,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import { useAuth } from "../contexts/AuthContext";
import { useUserRoles } from "../hooks/useUserRoles";
import { BrandButton } from "../components/common/BrandButton";
import { PageShell } from "../components/layout/PageShell";
import { BulkOfferPricePreviewTable } from "../components/webmaster-offers/BulkOfferPricePreviewTable";
import {
  BULK_PRICE_TYPES,
  MAIN_PRICE_TYPE,
  PRIMARY_EMAIL_MAX_LENGTH,
  MAX_TERM_YEARS,
  TERM_PERMANENT,
  TERM_FINITE,
  TERM_YEAR,
} from "../constants/webmasterOffers.constants";
import { DEFAULT_CURRENCY, FALLBACK_CURRENCIES } from "../constants/currency.constants";
import type { TermKind } from "../components/webmaster-offers/editWebmasterOfferForm";
import { DEFAULT_PAGE_SIZE, PAGE_SIZE_OPTIONS } from "../constants/pagination.constants";
import { WEBMASTER_OFFERS_MULTI_SEARCH_LIMIT } from "../constants/search.constants";
import { webmasterOffersService } from "../services/webmasterOffers.service";
import type { BulkOfferPricePreview } from "../types/bulkOfferPricePreview.types";

interface InputRow {
  domain: string;
  values: Record<number, string>;
}

const TOO_MANY_ROWS_MESSAGE =
  "Lists this large are not supported. Split the list into smaller batches.";

export function BulkWebmasterPriceUpdate() {
  const { user } = useAuth();
  const { canReadWebmasterOffers, canManageWebmasterOffers } = useUserRoles();
  if (!user || !canReadWebmasterOffers || !canManageWebmasterOffers)
    return <Navigate to="/webmaster-offers" replace />;
  return <BulkPriceUpdatePage key={user.id} />;
}

function BulkPriceUpdatePage() {
  const navigate = useNavigate();
  const [primaryEmail, setPrimaryEmail] = useState("");
  const [termKind, setTermKind] = useState<TermKind>("none");
  const [years, setYears] = useState("1");
  const [currency, setCurrency] = useState(DEFAULT_CURRENCY);
  const [currencies, setCurrencies] = useState(FALLBACK_CURRENCIES);
  const [currencyError, setCurrencyError] = useState(false);
  const [currencyRetry, setCurrencyRetry] = useState(0);
  const [selected, setSelected] = useState<number[]>([MAIN_PRICE_TYPE]);
  const [pasteText, setPasteText] = useState("");
  const [pasteError, setPasteError] = useState<string | null>(null);
  const [rows, setRows] = useState<InputRow[]>([]);
  const [preview, setPreview] = useState<BulkOfferPricePreview | null>(null);
  const step = preview ? 1 : 0;
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [rowLimitError, setRowLimitError] = useState<"paste" | "add" | null>(null);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const request = useRef<AbortController | null>(null);
  const lastRowDomainInput = useRef<HTMLInputElement | null>(null);
  const focusAddedRow = useRef(false);
  const tableHeading = useRef<HTMLDivElement | null>(null);
  const scrollLoadedRows = useRef(false);
  const types = useMemo(
    () => BULK_PRICE_TYPES.filter((type) => selected.includes(type.value)),
    [selected]
  );
  const headers = ["Domain", ...types.map((type) => type.label)];
  const hasHeader =
    rows.length > 0 &&
    rows[0].domain.trim().toLowerCase() === "domain" &&
    types.every(
      (type) => rows[0].values[type.value]?.trim().toLowerCase() === type.label.toLowerCase()
    );
  const termInvalid =
    termKind === "finite" &&
    (!/^\d+$/.test(years) || Number(years) < 1 || Number(years) > MAX_TERM_YEARS);

  useEffect(() => {
    let active = true;
    webmasterOffersService
      .getCurrencies()
      .then((values) => {
        if (active) {
          setCurrencies(values);
          setCurrencyError(false);
        }
      })
      .catch(() => {
        if (active) setCurrencyError(true);
      });
    return () => {
      active = false;
    };
  }, [currencyRetry]);

  useEffect(() => () => request.current?.abort(), []);
  useEffect(() => {
    if (!scrollLoadedRows.current || !tableHeading.current) return;
    scrollLoadedRows.current = false;
    tableHeading.current.scrollIntoView({ block: "start", behavior: "smooth" });
  }, [rows]);
  useEffect(() => {
    if (!focusAddedRow.current || !lastRowDomainInput.current) return;
    focusAddedRow.current = false;
    lastRowDomainInput.current.focus({ preventScroll: true });
    lastRowDomainInput.current.scrollIntoView({
      block: "center",
      inline: "nearest",
      behavior: "smooth",
    });
  }, [rows.length, page]);
  useEffect(() => {
    const beforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
    };
    if (rows.length || pasteText || primaryEmail)
      window.addEventListener("beforeunload", beforeUnload);
    return () => window.removeEventListener("beforeunload", beforeUnload);
  }, [rows.length, pasteText, primaryEmail]);

  const invalidate = () => {
    request.current?.abort();
    request.current = null;
    setLoading(false);
    setPreview(null);
    setError(null);
    setRowLimitError(null);
    setPasteError(null);
  };
  const loadPaste = () => {
    invalidate();
    const lines = pasteText.replace(/\r\n?/g, "\n").replace(/\n+$/, "").split("\n");
    if (lines.length > WEBMASTER_OFFERS_MULTI_SEARCH_LIMIT) {
      setRowLimitError("paste");
      return;
    }
    const columns = lines.map((line) => line.split("\t"));
    const invalidRow = columns.findIndex((cells) => cells.length !== headers.length);
    if (invalidRow !== -1) {
      setPasteError(
        `Row ${invalidRow + 1}: expected ${headers.length} columns, found ${columns[invalidRow].length}. Paste only ${headers.join(", ")} in this order.`
      );
      return;
    }
    scrollLoadedRows.current = true;
    setRows(
      columns.map(([domain, ...cells]) => ({
        domain,
        values: Object.fromEntries(types.map((type, cell) => [type.value, cells[cell]])),
      }))
    );
    setPasteText("");
    setPage(0);
  };
  const updateRow = (index: number, patch: Partial<InputRow>) => {
    invalidate();
    setRows((current) => current.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  };
  const addRow = () => {
    invalidate();
    if (rows.length >= WEBMASTER_OFFERS_MULTI_SEARCH_LIMIT) {
      setRowLimitError("add");
      return;
    }
    focusAddedRow.current = true;
    setRows((current) => [...current, { domain: "", values: {} }]);
    setPage(Math.floor(rows.length / pageSize));
  };
  const removeRow = (index: number) => {
    invalidate();
    setRows((current) => current.filter((_, i) => i !== index));
    setPage((current) =>
      Math.min(current, Math.max(0, Math.ceil((rows.length - 1) / pageSize) - 1))
    );
  };
  const checkChanges = async () => {
    if (loading || request.current) return;
    const controller = new AbortController();
    request.current = controller;
    setLoading(true);
    setError(null);
    setPreview(null);
    try {
      const result = await webmasterOffersService.previewBulkPrices(
        {
          primaryEmail,
          termType:
            termKind === "none" ? null : termKind === "permanent" ? TERM_PERMANENT : TERM_FINITE,
          termValue: termKind === "finite" ? Number(years) : null,
          termUnit: termKind === "finite" ? TERM_YEAR : null,
          sourceCurrency: currency,
          priceTypes: types.map((type) => type.value),
          rows: rows.map((row, index) => ({
            rowNumber: index + 1,
            domain: row.domain,
            values: types.map((type) => row.values[type.value] ?? ""),
          })),
        },
        controller.signal
      );
      if (!controller.signal.aborted) {
        setPreview(result);
      }
    } catch (cause) {
      if (!controller.signal.aborted)
        setError(cause instanceof Error ? cause.message : "Could not check changes. Try again.");
    } finally {
      if (request.current === controller) {
        request.current = null;
        setLoading(false);
      }
    }
  };
  const pagination = (
    <TablePagination
      component="div"
      count={rows.length}
      page={page}
      rowsPerPage={pageSize}
      rowsPerPageOptions={PAGE_SIZE_OPTIONS}
      onPageChange={(_, value) => setPage(value)}
      onRowsPerPageChange={(event) => {
        setPageSize(Number(event.target.value));
        setPage(0);
      }}
    />
  );

  return (
    <PageShell maxWidth={false}>
      <Stack spacing={2}>
        <Box
          sx={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            gap: 2,
            flexWrap: "wrap",
          }}
        >
          <Typography variant="h4" component="h1" sx={{ fontWeight: 600 }}>
            Update webmaster prices
          </Typography>
          <BrandButton onClick={() => navigate("/webmaster-offers")}>Back to offers</BrandButton>
        </Box>
        <Stepper activeStep={step} sx={{ maxWidth: 650 }}>
          <Step>
            <StepLabel>Enter price list</StepLabel>
          </Step>
          <Step>
            <StepLabel>Preview changes</StepLabel>
          </Step>
        </Stepper>
        {error && <Alert severity="error">{error}</Alert>}
        {step === 0 ? (
          <>
            <Stack direction={{ xs: "column", md: "row" }} spacing={2} alignItems="flex-start">
              <TextField
                label="Primary email"
                multiline
                maxRows={3}
                value={primaryEmail}
                required
                sx={{ flex: 1, minWidth: 260 }}
                slotProps={{ htmlInput: { maxLength: PRIMARY_EMAIL_MAX_LENGTH } }}
                helperText="Use the primary email or contact instructions shown in Webmaster Offers."
                onChange={(event) => {
                  invalidate();
                  setPrimaryEmail(event.target.value);
                }}
              />
              <TextField
                select
                label="Price-list term"
                value={termKind}
                sx={{ minWidth: 170 }}
                onChange={(event) => {
                  invalidate();
                  setTermKind(event.target.value as TermKind);
                }}
              >
                <MenuItem value="none">No term</MenuItem>
                <MenuItem value="permanent">Permanent</MenuItem>
                <MenuItem value="finite">Finite years</MenuItem>
              </TextField>
              {termKind === "finite" && (
                <TextField
                  label="Years"
                  value={years}
                  error={termInvalid}
                  sx={{ width: 140 }}
                  helperText={termInvalid ? "Enter positive whole years." : undefined}
                  onChange={(event) => {
                    invalidate();
                    setYears(event.target.value);
                  }}
                />
              )}
              <TextField
                select
                label="Price-list currency"
                value={currency}
                sx={{ minWidth: 170 }}
                onChange={(event) => {
                  invalidate();
                  setCurrency(event.target.value);
                }}
              >
                {currencies.map((value) => (
                  <MenuItem key={value} value={value}>
                    {value}
                  </MenuItem>
                ))}
              </TextField>
            </Stack>
            {currencyError && (
              <Alert
                severity="warning"
                action={
                  <BrandButton size="small" onClick={() => setCurrencyRetry((value) => value + 1)}>
                    Retry
                  </BrandButton>
                }
              >
                Could not load additional currencies. USD, EUR and GBP remain available.
              </Alert>
            )}
            <Box>
              <Typography variant="subtitle2">Price types to update</Typography>
              <Stack direction="row" useFlexGap flexWrap="wrap">
                {BULK_PRICE_TYPES.map((type) => (
                  <FormControlLabel
                    key={type.value}
                    label={type.label}
                    control={
                      <Checkbox
                        checked={selected.includes(type.value)}
                        onChange={(_, checked) => {
                          invalidate();
                          setSelected((current) =>
                            checked
                              ? [...current, type.value]
                              : current.filter((value) => value !== type.value)
                          );
                        }}
                      />
                    }
                  />
                ))}
              </Stack>
              {!types.length && (
                <Typography color="error" variant="body2">
                  Select at least one price type.
                </Typography>
              )}
            </Box>
            <TextField
              label="Paste price list"
              multiline
              minRows={3}
              maxRows={8}
              value={pasteText}
              placeholder={["example.com", ...types.map(() => "100")].join("\t")}
              error={!!pasteError}
              helperText={
                pasteError ??
                (types.length === 1 && types[0].value === MAIN_PRICE_TYPE
                  ? "Enter a positive price for each domain."
                  : selected.includes(MAIN_PRICE_TYPE)
                    ? "Main requires a positive price. Other selected services also accept YES or NO."
                    : "Enter a positive price, YES or NO for each selected service.")
              }
              onChange={(event) => {
                invalidate();
                setPasteText(event.target.value);
              }}
            />
            <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
              <BrandButton disabled={!pasteText.trim() || !types.length} onClick={loadPaste}>
                {rows.length > 0 ? "Replace pasted rows" : "Load pasted rows"}
              </BrandButton>
            </Stack>
            {rowLimitError === "paste" && <Alert severity="error">{TOO_MANY_ROWS_MESSAGE}</Alert>}
            {rows.length > 0 && (
              <Stack
                ref={tableHeading}
                direction="row"
                spacing={1}
                useFlexGap
                flexWrap="wrap"
                alignItems="center"
                justifyContent="space-between"
                sx={{ scrollMarginTop: 2 }}
              >
                <Typography variant="subtitle2">
                  Price list · {rows.length.toLocaleString("en-US")} input rows
                </Typography>
                {hasHeader && (
                  <BrandButton
                    onClick={() => {
                      invalidate();
                      setRows((current) => current.slice(1));
                      setPage(0);
                    }}
                  >
                    Remove matching header row
                  </BrandButton>
                )}
              </Stack>
            )}
            {rows.length > 0 && (
              <>
                <TableContainer sx={{ border: 1, borderColor: "divider", borderRadius: 1 }}>
                  <Table size="small" aria-label="Price list corrections">
                    <TableHead>
                      <TableRow>
                        <TableCell>Row</TableCell>
                        <TableCell>Domain</TableCell>
                        {types.map((type) => (
                          <TableCell key={type.value}>
                            {type.label} ({currency})
                          </TableCell>
                        ))}
                        <TableCell>Actions</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {rows.slice(page * pageSize, (page + 1) * pageSize).map((row, index) => {
                        const rowIndex = page * pageSize + index;
                        const rowNumber = rowIndex + 1;
                        return (
                          <TableRow key={rowIndex}>
                            <TableCell>{rowNumber}</TableCell>
                            <TableCell sx={{ minWidth: 240 }}>
                              <TextField
                                fullWidth
                                size="small"
                                value={row.domain}
                                inputRef={
                                  rowIndex === rows.length - 1 ? lastRowDomainInput : undefined
                                }
                                slotProps={{
                                  htmlInput: { "aria-label": `Row ${rowNumber} Domain` },
                                }}
                                onChange={(event) =>
                                  updateRow(rowIndex, { domain: event.target.value })
                                }
                              />
                            </TableCell>
                            {types.map((type) => (
                              <TableCell key={type.value} sx={{ minWidth: 145 }}>
                                <TextField
                                  size="small"
                                  value={row.values[type.value] ?? ""}
                                  slotProps={{
                                    htmlInput: { "aria-label": `Row ${rowNumber} ${type.label}` },
                                  }}
                                  onChange={(event) =>
                                    updateRow(rowIndex, {
                                      values: { ...row.values, [type.value]: event.target.value },
                                    })
                                  }
                                />
                              </TableCell>
                            ))}
                            <TableCell sx={{ width: 64 }}>
                              <Tooltip title="Remove row">
                                <IconButton
                                  aria-label={`Remove row ${rowNumber}`}
                                  onClick={() => removeRow(rowIndex)}
                                >
                                  <DeleteOutlineIcon fontSize="small" />
                                </IconButton>
                              </Tooltip>
                            </TableCell>
                          </TableRow>
                        );
                      })}
                    </TableBody>
                  </Table>
                </TableContainer>
              </>
            )}
            <Stack
              direction="row"
              alignItems="center"
              justifyContent="space-between"
              useFlexGap
              flexWrap="wrap"
              spacing={1}
            >
              <BrandButton aria-label="Add row at end of table" onClick={addRow}>
                Add row
              </BrandButton>
              {rows.length > 0 && pagination}
              {rowLimitError === "add" && (
                <Alert severity="error" sx={{ width: "100%" }}>
                  {TOO_MANY_ROWS_MESSAGE}
                </Alert>
              )}
            </Stack>
            <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
              <BrandButton
                kind="primary"
                disabled={
                  loading ||
                  !primaryEmail.trim() ||
                  termInvalid ||
                  !types.length ||
                  !rows.length ||
                  !!pasteText.trim()
                }
                onClick={() => void checkChanges()}
              >
                {loading ? "Checking changes…" : "Preview changes"}
              </BrandButton>
            </Box>
          </>
        ) : (
          preview && (
            <BulkOfferPricePreviewTable
              preview={preview}
              onEditInput={() => {
                setPreview(null);
              }}
            />
          )
        )}
      </Stack>
    </PageShell>
  );
}
