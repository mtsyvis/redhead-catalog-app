import { useState } from "react";
import { Navigate } from "react-router-dom";
import { Box, Tab, Tabs, Typography } from "@mui/material";
import { alpha } from "@mui/material/styles";
import { PageShell } from "../components/layout/PageShell";
import { ExportAnalytics } from "../components/analytics/ExportAnalytics";
import { MissingDomainsAnalytics } from "../components/analytics/MissingDomainsAnalytics";
import { useUserRoles } from "../hooks/useUserRoles";

const ANALYTICS_SECTIONS = [
  { id: "exports", label: "Exports", permission: "AnalyticsRead", component: ExportAnalytics },
  {
    id: "multi-search",
    label: "Multi-search",
    permission: "MissingDomainsAnalyticsRead",
    component: MissingDomainsAnalytics,
  },
] as const;

type AnalyticsSectionId = (typeof ANALYTICS_SECTIONS)[number]["id"];

export function Analytics() {
  const { hasPermission } = useUserRoles();
  const availableSections = ANALYTICS_SECTIONS.filter(({ permission }) =>
    hasPermission(permission)
  );
  const [section, setSection] = useState<AnalyticsSectionId>(availableSections[0]?.id ?? "exports");
  if (availableSections.length === 0) return <Navigate to="/sites" replace />;
  const activeSection =
    availableSections.find(({ id }) => id === section)?.id ?? availableSections[0].id;

  return (
    <PageShell maxWidth="xl" compact>
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          columnGap: 3,
          rowGap: 1,
          flexWrap: "wrap",
          mb: 2,
        }}
      >
        <Typography
          component="h1"
          sx={{ fontSize: { xs: 28, sm: 30 }, fontWeight: 600, flexShrink: 0 }}
        >
          Analytics
        </Typography>
        <Tabs
          value={activeSection}
          onChange={(_, value) => setSection(value)}
          aria-label="Analytics sections"
          variant="scrollable"
          scrollButtons="auto"
          allowScrollButtonsMobile
          sx={(theme) => ({
            flex: { xs: "1 1 100%", sm: "1 1 0" },
            minWidth: 0,
            minHeight: 38,
            "& .MuiTabs-indicator": { display: "none" },
            "& .MuiTabs-list": { gap: 0.5 },
            "& .MuiTabs-scrollButtons": { width: 28 },
            "& .MuiTab-root": {
              minHeight: 38,
              minWidth: "auto",
              py: 0.75,
              px: 1.5,
              borderRadius: "8px",
              color: "text.secondary",
              "&:hover": { bgcolor: "action.hover" },
              "&.Mui-selected": {
                color: "primary.main",
                fontWeight: 600,
                bgcolor: alpha(theme.palette.primary.main, 0.08),
              },
              "&.Mui-focusVisible": {
                outline: `2px solid ${theme.palette.primary.main}`,
                outlineOffset: -2,
              },
            },
          })}
        >
          {availableSections.map(({ id, label }) => (
            <Tab key={id} id={`${id}-tab`} aria-controls={`${id}-panel`} label={label} value={id} />
          ))}
        </Tabs>
      </Box>
      {availableSections.map(({ id, component: Section }) => (
        <Box
          key={id}
          id={`${id}-panel`}
          role="tabpanel"
          aria-labelledby={`${id}-tab`}
          hidden={activeSection !== id}
        >
          <Section active={activeSection === id} />
        </Box>
      ))}
    </PageShell>
  );
}
