import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  InputAdornment,
  MenuItem,
  Paper,
  TextField,
  Typography,
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import { BrandButton } from "../common/BrandButton";
import { AnalyticsLoadingSkeleton } from "./AnalyticsLoadingSkeleton";
import { AnalyticsInfo, AnalyticsMetrics } from "./AnalyticsShared";
import { analyticsGridSx } from "./analyticsGridStyles";
import { ApiClient } from "../../services/api.client";
import { dataGridLocaleText, formatInteger } from "../../utils/numberFormat";
import type {
  MissingDomainAnalyticsRow,
  MissingDomainsAnalyticsResponse,
} from "../../types/missingDomainsAnalytics.types";

type Preset = "last7" | "last30" | "last90" | "all" | "custom";
interface Filters {
  preset: Preset;
  from: string;
  to: string;
  role: "all" | "Client" | "Lite";
  status: "all" | "missing" | "added";
  domain: string;
  page: number;
  pageSize: number;
}

function utcRange(days: number) {
  const today = new Date();
  const from = new Date(today);
  from.setUTCDate(today.getUTCDate() - days + 1);
  return { from: from.toISOString().slice(0, 10), to: today.toISOString().slice(0, 10) };
}

const formatUtc = (value: string) =>
  new Date(value).toLocaleString("en-GB", {
    timeZone: "UTC",
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });

const columns: GridColDef<MissingDomainAnalyticsRow>[] = [
  {
    field: "domain",
    headerName: "Domain",
    minWidth: 200,
    flex: 1.3,
    renderCell: ({ row }) => (
      <Typography variant="body2" sx={{ overflowWrap: "anywhere", whiteSpace: "normal" }}>
        {row.domain}
      </Typography>
    ),
  },
  {
    field: "searches",
    headerName: "Searches",
    minWidth: 100,
    flex: 0.5,
    align: "right",
    headerAlign: "right",
    valueFormatter: (value: number) => formatInteger(value),
  },
  {
    field: "uniqueUsers",
    headerName: "Unique users",
    minWidth: 120,
    flex: 0.5,
    align: "right",
    headerAlign: "right",
    valueFormatter: (value: number) => formatInteger(value),
  },
  {
    field: "lastSearchedAtUtc",
    headerName: "Last searched",
    minWidth: 195,
    flex: 1,
    valueFormatter: (value: string) => formatUtc(value),
  },
  {
    field: "firstSearchedAtUtc",
    headerName: "First searched",
    minWidth: 195,
    flex: 1,
    valueFormatter: (value: string) => formatUtc(value),
  },
  {
    field: "isInCatalog",
    headerName: "Catalog status",
    minWidth: 150,
    flex: 0.75,
    renderCell: ({ row }) => (
      <Chip
        size="small"
        variant="outlined"
        color={row.isInCatalog ? "success" : "default"}
        label={row.isInCatalog ? "Now in catalog" : "Still missing"}
      />
    ),
  },
];

const defaultFilters = (): Filters => ({
  preset: "last30",
  ...utcRange(30),
  role: "all",
  status: "all",
  domain: "",
  page: 0,
  pageSize: 25,
});

export function MissingDomainsAnalytics({ active }: { active: boolean }) {
  const [filters, setFilters] = useState<Filters>(defaultFilters);
  const hasActiveFilters =
    filters.preset !== "last30" ||
    filters.role !== "all" ||
    filters.status !== "all" ||
    filters.domain !== "";
  const [result, setResult] = useState<{
    key: string;
    data?: MissingDomainsAnalyticsResponse;
    error?: string;
  } | null>(null);
  const [retry, setRetry] = useState(0);
  const updateFilters = (changes: Partial<Filters>) =>
    setFilters((current) => ({ ...current, ...changes, page: 0 }));
  const dateError =
    filters.preset === "custom" && (!filters.from || !filters.to || filters.from > filters.to)
      ? "Choose both dates, with From earlier than or equal to To."
      : null;
  const queryString = useMemo(() => {
    const query = new URLSearchParams({
      page: String(filters.page + 1),
      pageSize: String(filters.pageSize),
    });
    if (filters.preset === "all") query.set("allTime", "true");
    else {
      const range =
        filters.preset === "custom"
          ? filters
          : utcRange(filters.preset === "last7" ? 7 : filters.preset === "last90" ? 90 : 30);
      query.set("from", range.from);
      query.set("to", range.to);
    }
    if (filters.role !== "all") query.set("role", filters.role);
    if (filters.status !== "all") query.set("catalogStatus", filters.status);
    if (filters.domain.trim()) query.set("domain", filters.domain.trim());
    return query.toString();
  }, [filters]);
  const requestKey = `${queryString}:${retry}`;

  useEffect(() => {
    if (!active || dateError) return;
    let current = true;
    const timer = window.setTimeout(() => {
      ApiClient.get<MissingDomainsAnalyticsResponse>(
        `/api/admin/analytics/missing-domains?${queryString}`
      )
        .then((data) => {
          if (current) setResult({ key: requestKey, data });
        })
        .catch((error) => {
          if (current)
            setResult({
              key: requestKey,
              error: error instanceof Error ? error.message : "Failed to load missing domains.",
            });
        });
    }, 250);
    return () => {
      current = false;
      window.clearTimeout(timer);
    };
  }, [active, dateError, queryString, requestKey]);

  const data = result?.key === requestKey ? result.data : undefined;
  const error = result?.key === requestKey ? result.error : undefined;
  const loading = !dateError && !data && !error;

  return (
    <>
      <Box sx={{ mb: 2 }}>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr", lg: "1fr 1fr 1fr 1.4fr auto" },
            gap: 1.5,
          }}
        >
          <TextField
            select
            size="small"
            label="Date range"
            value={filters.preset}
            onChange={(event) => updateFilters({ preset: event.target.value as Preset })}
          >
            <MenuItem value="last7">Last 7 days</MenuItem>
            <MenuItem value="last30">Last 30 days</MenuItem>
            <MenuItem value="last90">Last 90 days</MenuItem>
            <MenuItem value="all">All time</MenuItem>
            <MenuItem value="custom">Custom</MenuItem>
          </TextField>
          <TextField
            select
            size="small"
            label="Role"
            value={filters.role}
            onChange={(event) => updateFilters({ role: event.target.value as Filters["role"] })}
          >
            <MenuItem value="all">Client &amp; Lite</MenuItem>
            <MenuItem value="Client">Client</MenuItem>
            <MenuItem value="Lite">Lite</MenuItem>
          </TextField>
          <TextField
            select
            size="small"
            label="Catalog status"
            value={filters.status}
            onChange={(event) => updateFilters({ status: event.target.value as Filters["status"] })}
          >
            <MenuItem value="all">All domains</MenuItem>
            <MenuItem value="missing">Still missing</MenuItem>
            <MenuItem value="added">Now in catalog</MenuItem>
          </TextField>
          <TextField
            size="small"
            type="search"
            label="Domain"
            placeholder="Search domain…"
            value={filters.domain}
            slotProps={{
              htmlInput: { maxLength: 253 },
              inputLabel: { shrink: true },
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchIcon fontSize="small" />
                  </InputAdornment>
                ),
              },
            }}
            onChange={(event) => updateFilters({ domain: event.target.value })}
          />
          {hasActiveFilters && (
            <Button
              size="small"
              color="inherit"
              sx={{
                whiteSpace: "nowrap",
                alignSelf: "center",
                justifySelf: "end",
                gridColumn: { xs: "1 / -1", lg: "auto" },
              }}
              onClick={() =>
                setFilters((current) => ({ ...defaultFilters(), pageSize: current.pageSize }))
              }
            >
              Reset filters
            </Button>
          )}
        </Box>
        {filters.preset === "custom" && (
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", sm: "240px 240px" },
              gap: 1.5,
              mt: 2,
            }}
          >
            <TextField
              size="small"
              type="date"
              label="From"
              value={filters.from}
              error={!!dateError}
              helperText={dateError}
              slotProps={{ inputLabel: { shrink: true } }}
              onChange={(event) => updateFilters({ from: event.target.value })}
            />
            <TextField
              size="small"
              type="date"
              label="To"
              value={filters.to}
              error={!!dateError}
              slotProps={{ inputLabel: { shrink: true } }}
              onChange={(event) => updateFilters({ to: event.target.value })}
            />
          </Box>
        )}
      </Box>
      {error && (
        <Alert
          severity="error"
          action={
            <BrandButton kind="outline" size="small" onClick={() => setRetry((value) => value + 1)}>
              Retry
            </BrandButton>
          }
        >
          {error}
        </Alert>
      )}
      {loading && <AnalyticsLoadingSkeleton tableOnly />}
      {!dateError && data && (
        <>
          <Paper variant="outlined" sx={{ overflow: "hidden" }}>
            <Box
              sx={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                flexWrap: "wrap",
                gap: 1,
                px: 2,
                py: 1.25,
              }}
            >
              <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
                <Typography variant="h6" sx={{ fontWeight: 700 }}>
                  Missing domain demand
                </Typography>
                <AnalyticsInfo
                  label="Missing domain demand"
                  text="Domains not found at the time of Client and Lite multi-searches. First and last searches refer to the selected period."
                />
              </Box>
              <AnalyticsMetrics
                inline
                metrics={[
                  {
                    label: data.uniqueDomains === 1 ? "domain" : "domains",
                    value: data.uniqueDomains,
                  },
                  {
                    label: "domain searches",
                    value: data.searches,
                    helperText:
                      "Each missing domain counts once per multi-search. This is not the number of multi-search requests.",
                  },
                  { label: data.uniqueUsers === 1 ? "user" : "users", value: data.uniqueUsers },
                ]}
              />
            </Box>
            <DataGrid
              aria-label="Missing domain demand"
              rows={data.items}
              columns={columns}
              getRowId={(row) => row.domain}
              rowCount={data.uniqueDomains}
              paginationModel={{ page: data.page - 1, pageSize: filters.pageSize }}
              paginationMode="server"
              pageSizeOptions={[10, 25, 50, 100]}
              onPaginationModelChange={(model) =>
                setFilters((current) => {
                  const page = model.pageSize === current.pageSize ? model.page : 0;
                  return page === current.page && model.pageSize === current.pageSize
                    ? current
                    : { ...current, page, pageSize: model.pageSize };
                })
              }
              localeText={{
                ...dataGridLocaleText,
                noRowsLabel:
                  "No domains match these filters. Analytics includes searches recorded since this feature was enabled.",
              }}
              disableColumnSorting
              disableColumnMenu
              disableRowSelectionOnClick
              autoHeight
              getRowHeight={() => "auto"}
              columnHeaderHeight={44}
              sx={{ ...analyticsGridSx, border: 0, borderRadius: 0 }}
            />
          </Paper>
        </>
      )}
    </>
  );
}
