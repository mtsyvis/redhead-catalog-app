import { useState } from "react";
import { Navigate } from "react-router-dom";
import { Box, Tab, Tabs, Typography } from "@mui/material";
import { PageShell } from "../components/layout/PageShell";
import { ExportAnalytics } from "../components/analytics/ExportAnalytics";
import { MissingDomainsAnalytics } from "../components/analytics/MissingDomainsAnalytics";
import { useUserRoles } from "../hooks/useUserRoles";

export function Analytics() {
  const { canReadAnalytics, canReadMissingDomainsAnalytics } = useUserRoles();
  const [section, setSection] = useState<"exports" | "multi-search">(
    canReadAnalytics ? "exports" : "multi-search"
  );
  if (!canReadAnalytics && !canReadMissingDomainsAnalytics) return <Navigate to="/sites" replace />;
  const activeSection =
    section === "exports" && !canReadAnalytics
      ? "multi-search"
      : section === "multi-search" && !canReadMissingDomainsAnalytics
        ? "exports"
        : section;

  return (
    <PageShell maxWidth="xl" compact>
      <Box sx={{ display: "flex", alignItems: "center", gap: 3, flexWrap: "wrap", mb: 2 }}>
        <Typography component="h1" sx={{ fontSize: { xs: 28, sm: 30 }, fontWeight: 600 }}>
          Analytics
        </Typography>
        <Tabs
          value={activeSection}
          onChange={(_, value) => setSection(value)}
          aria-label="Analytics sections"
          sx={{
            bgcolor: "action.hover",
            p: 0.5,
            borderRadius: 8,
            minHeight: 40,
            "& .MuiTabs-indicator": { display: "none" },
            "& .MuiTab-root": { minHeight: 36, py: 0.75, px: 2.5, borderRadius: 8 },
            "& .Mui-selected": { bgcolor: "background.paper", boxShadow: 1 },
          }}
        >
          {canReadAnalytics && (
            <Tab id="exports-tab" aria-controls="exports-panel" label="Exports" value="exports" />
          )}
          {canReadMissingDomainsAnalytics && (
            <Tab
              id="multi-search-tab"
              aria-controls="multi-search-panel"
              label="Multi-search"
              value="multi-search"
            />
          )}
        </Tabs>
      </Box>
      {canReadAnalytics && (
        <Box
          id="exports-panel"
          role="tabpanel"
          aria-labelledby="exports-tab"
          hidden={activeSection !== "exports"}
        >
          <ExportAnalytics active={activeSection === "exports"} />
        </Box>
      )}
      {canReadMissingDomainsAnalytics && (
        <Box
          id="multi-search-panel"
          role="tabpanel"
          aria-labelledby="multi-search-tab"
          hidden={activeSection !== "multi-search"}
        >
          <MissingDomainsAnalytics active={activeSection === "multi-search"} />
        </Box>
      )}
    </PageShell>
  );
}
