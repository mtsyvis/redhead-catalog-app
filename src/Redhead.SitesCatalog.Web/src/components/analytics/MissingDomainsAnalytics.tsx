import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Chip,
  MenuItem,
  Paper,
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
import { BrandButton } from "../common/BrandButton";
import { AnalyticsLoadingSkeleton } from "./AnalyticsLoadingSkeleton";
import { KpiCard } from "./AnalyticsShared";
import { ApiClient } from "../../services/api.client";
import { formatInteger } from "../../utils/numberFormat";
import type { MissingDomainsAnalyticsResponse } from "../../types/missingDomainsAnalytics.types";

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

export function MissingDomainsAnalytics({ active }: { active: boolean }) {
  const [filters, setFilters] = useState<Filters>(() => ({
    preset: "last30",
    ...utcRange(30),
    role: "all",
    status: "all",
    domain: "",
    page: 0,
    pageSize: 25,
  }));
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
      <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr", lg: "1fr 1fr 1fr 1.3fr" },
            gap: 2,
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
            slotProps={{ htmlInput: { maxLength: 253 } }}
            onChange={(event) => updateFilters({ domain: event.target.value })}
          />
        </Box>
        {filters.preset === "custom" && (
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", sm: "240px 240px" },
              gap: 2,
              mt: 2,
            }}
          >
            <TextField
              size="small"
              type="date"
              label="From (UTC)"
              value={filters.from}
              error={!!dateError}
              helperText={dateError}
              slotProps={{ inputLabel: { shrink: true } }}
              onChange={(event) => updateFilters({ from: event.target.value })}
            />
            <TextField
              size="small"
              type="date"
              label="To (UTC)"
              value={filters.to}
              error={!!dateError}
              slotProps={{ inputLabel: { shrink: true } }}
              onChange={(event) => updateFilters({ to: event.target.value })}
            />
          </Box>
        )}
      </Paper>
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
      {loading && <AnalyticsLoadingSkeleton />}
      {!dateError && data && (
        <>
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", sm: "repeat(3, minmax(0, 1fr))" },
              gap: 2,
              mb: 2,
            }}
          >
            <KpiCard
              label="Unique missing domains"
              value={data.uniqueDomains}
              helperText="Not found at the time of search."
            />
            <KpiCard
              label="Searches for missing domains"
              value={data.searches}
              helperText="Each domain counts once per search."
            />
            <KpiCard
              label="Unique users"
              value={data.uniqueUsers}
              helperText={
                filters.role === "all" ? "Client and Lite accounts." : `${filters.role} accounts.`
              }
            />
          </Box>
          <Paper variant="outlined" sx={{ overflow: "hidden" }}>
            <Box
              sx={{
                p: 2,
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                gap: 1,
                flexWrap: "wrap",
              }}
            >
              <Typography variant="h6" sx={{ fontWeight: 600 }}>
                Missing domains
              </Typography>
            </Box>
            <TableContainer>
              <Table size="small" aria-label="Missing domain demand" sx={{ minWidth: 800 }}>
                <TableHead>
                  <TableRow>
                    <TableCell>Domain</TableCell>
                    <TableCell align="right">Searches ↓</TableCell>
                    <TableCell align="right">Unique users</TableCell>
                    <TableCell>First searched</TableCell>
                    <TableCell>Last searched</TableCell>
                    <TableCell>Catalog status</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {data.items.map((row) => (
                    <TableRow key={row.domain} hover>
                      <TableCell sx={{ overflowWrap: "anywhere", maxWidth: 300 }}>
                        {row.domain}
                      </TableCell>
                      <TableCell align="right">{formatInteger(row.searches)}</TableCell>
                      <TableCell align="right">{formatInteger(row.uniqueUsers)}</TableCell>
                      <TableCell sx={{ whiteSpace: "nowrap" }}>
                        {formatUtc(row.firstSearchedAtUtc)}
                      </TableCell>
                      <TableCell sx={{ whiteSpace: "nowrap" }}>
                        {formatUtc(row.lastSearchedAtUtc)}
                      </TableCell>
                      <TableCell>
                        <Chip
                          size="small"
                          variant="outlined"
                          color={row.isInCatalog ? "success" : "default"}
                          label={row.isInCatalog ? "Now in catalog" : "Still missing"}
                        />
                      </TableCell>
                    </TableRow>
                  ))}
                  {data.items.length === 0 && (
                    <TableRow>
                      <TableCell
                        colSpan={6}
                        sx={{ py: 4, textAlign: "center", color: "text.secondary" }}
                      >
                        No missing domains found for these filters. Analytics includes searches
                        recorded since this feature was enabled.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
            <TablePagination
              component="div"
              count={data.uniqueDomains}
              page={data.page - 1}
              rowsPerPage={filters.pageSize}
              rowsPerPageOptions={[10, 25, 50, 100]}
              onPageChange={(_, page) => setFilters((current) => ({ ...current, page }))}
              onRowsPerPageChange={(event) =>
                updateFilters({ pageSize: Number(event.target.value) })
              }
            />
          </Paper>
        </>
      )}
    </>
  );
}
