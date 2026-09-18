import React, { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, Tab, Tabs } from "@mui/material";
import type { GridPaginationModel } from "@mui/x-data-grid";
import type { Dayjs } from "dayjs";
import { BrandButton } from "../common/BrandButton";
import { AnalyticsFilters } from "./AnalyticsFilters";
import {
  ALL_CLIENT_OPTION,
  type DateRangePreset,
  type DestinationFilterValue,
  formatApiDate,
  getPresetRange,
  type StatusFilterValue,
} from "./analyticsFilterUtils";
import { AnalyticsLoadingSkeleton } from "./AnalyticsLoadingSkeleton";
import { BusinessDemandTab } from "./BusinessDemandTab";
import { ExportActivityTab } from "./ExportActivityTab";
import { useUserRoles } from "../../hooks/useUserRoles";
import { adminAnalyticsService } from "../../services/adminAnalytics.service";
import type {
  AnalyticsClientOption,
  BusinessDemandAnalytics,
  ExportActivityAnalytics,
} from "../../types/analytics.types";

type AnalyticsTabValue = "businessDemand" | "exportActivity";
interface ExportFilters {
  datePreset: DateRangePreset;
  customFrom: Dayjs | null;
  customTo: Dayjs | null;
  clientId: string | null;
  destination: DestinationFilterValue;
  status: StatusFilterValue;
}
type AnalyticsResult = {
  key: string;
  report?:
    | { type: "businessDemand"; data: BusinessDemandAnalytics }
    | { type: "exportActivity"; data: ExportActivityAnalytics };
  error?: string;
};

export const ExportAnalytics: React.FC<{ active: boolean }> = ({ active }) => {
  const { canReadAnalytics } = useUserRoles();
  const [activeTab, setActiveTab] = useState<AnalyticsTabValue>("businessDemand");
  const [filters, setFilters] = useState<ExportFilters>(() => {
    const range = getPresetRange("last30");
    return {
      datePreset: "last30",
      customFrom: range.from,
      customTo: range.to,
      clientId: null,
      destination: "all",
      status: "all",
    };
  });
  const { datePreset, customFrom, customTo, clientId, destination, status } = filters;
  const [clientOptions, setClientOptions] = useState<AnalyticsClientOption[]>([]);
  const [clientsLoading, setClientsLoading] = useState(true);
  const [clientsError, setClientsError] = useState<string | null>(null);
  const [result, setResult] = useState<AnalyticsResult | null>(null);
  const [retry, setRetry] = useState(0);
  const [recentExportsPaginationModel, setRecentExportsPaginationModel] =
    useState<GridPaginationModel>({
      page: 0,
      pageSize: 25,
    });

  const updateFilters = (changes: Partial<ExportFilters>) => {
    setFilters((current) => ({ ...current, ...changes }));
    setRecentExportsPaginationModel((current) => ({ ...current, page: 0 }));
  };

  const clientSelectOptions = useMemo(() => [ALL_CLIENT_OPTION, ...clientOptions], [clientOptions]);
  const selectedClient = useMemo(
    () =>
      clientSelectOptions.find((option) => option.id === (clientId ?? "all")) ?? ALL_CLIENT_OPTION,
    [clientId, clientSelectOptions]
  );

  const selectedRange = useMemo(() => {
    if (datePreset !== "custom") {
      return getPresetRange(datePreset);
    }

    if (!customFrom || !customTo || !customFrom.isValid() || !customTo.isValid()) {
      return null;
    }

    return {
      from: customFrom.startOf("day"),
      to: customTo.startOf("day"),
    };
  }, [customFrom, customTo, datePreset]);

  const dateRangeError = useMemo(() => {
    if (datePreset !== "custom") return null;
    if (!customFrom || !customTo) return "Choose both custom dates.";
    if (!customFrom.isValid() || !customTo.isValid()) return "Choose valid custom dates.";
    if (customFrom.startOf("day").isAfter(customTo.startOf("day"))) {
      return "From date must be earlier than or equal to to date.";
    }
    return null;
  }, [customFrom, customTo, datePreset]);

  const analyticsQueryParams = useMemo(() => {
    if (!selectedRange || dateRangeError) return null;

    return {
      from: formatApiDate(selectedRange.from),
      to: formatApiDate(selectedRange.to),
      clientId: clientId ?? undefined,
      destination: destination === "all" ? undefined : destination,
      status: status === "all" ? undefined : status,
    };
  }, [clientId, dateRangeError, destination, selectedRange, status]);

  const requestKey = JSON.stringify([
    activeTab,
    analyticsQueryParams,
    recentExportsPaginationModel,
    retry,
  ]);

  useEffect(() => {
    if (!active || !canReadAnalytics || !analyticsQueryParams) return;
    let current = true;

    const load = async () => {
      try {
        const report: NonNullable<AnalyticsResult["report"]> =
          activeTab === "businessDemand"
            ? {
                type: "businessDemand",
                data: await adminAnalyticsService.getBusinessDemand(analyticsQueryParams),
              }
            : {
                type: "exportActivity",
                data: await adminAnalyticsService.getExportActivity({
                  ...analyticsQueryParams,
                  page: recentExportsPaginationModel.page + 1,
                  pageSize: recentExportsPaginationModel.pageSize,
                }),
              };
        if (current) setResult({ key: requestKey, report });
      } catch (err) {
        if (current)
          setResult({
            key: requestKey,
            error: err instanceof Error ? err.message : "Failed to load analytics.",
          });
      }
    };
    void load();
    return () => {
      current = false;
    };
  }, [
    active,
    activeTab,
    analyticsQueryParams,
    canReadAnalytics,
    recentExportsPaginationModel,
    requestKey,
  ]);

  const handleRecentExportsPaginationChange = useCallback((model: GridPaginationModel) => {
    setRecentExportsPaginationModel(model);
  }, []);

  const handleTabChange = (_event: React.SyntheticEvent, value: AnalyticsTabValue) => {
    setActiveTab(value);
  };

  const currentResult = result?.key === requestKey ? result : null;
  const report = analyticsQueryParams ? currentResult?.report : undefined;
  const currentError = analyticsQueryParams ? currentResult?.error : undefined;
  const currentLoading = !!analyticsQueryParams && !report && !currentError;

  useEffect(() => {
    if (!canReadAnalytics || !active) return;
    let current = true;

    adminAnalyticsService
      .listClients()
      .then((options) => {
        if (current) {
          setClientOptions(options);
          setClientsError(null);
        }
      })
      .catch((err) => {
        if (current)
          setClientsError(err instanceof Error ? err.message : "Failed to load clients.");
      })
      .finally(() => {
        if (current) setClientsLoading(false);
      });
    return () => {
      current = false;
    };
  }, [canReadAnalytics, active]);

  if (!canReadAnalytics) return null;

  return (
    <>
      <Tabs
        value={activeTab}
        onChange={handleTabChange}
        aria-label="Export analytics reports"
        sx={{ borderBottom: 1, borderColor: "divider", mb: 2 }}
      >
        <Tab label="Business Demand" value="businessDemand" />
        <Tab label="Export Activity" value="exportActivity" />
      </Tabs>

      <AnalyticsFilters
        datePreset={datePreset}
        onDatePresetChange={(datePreset) => updateFilters({ datePreset })}
        customFrom={customFrom}
        onCustomFromChange={(customFrom) => updateFilters({ customFrom })}
        customTo={customTo}
        onCustomToChange={(customTo) => updateFilters({ customTo })}
        dateRangeError={dateRangeError}
        clientSelectOptions={clientSelectOptions}
        selectedClient={selectedClient}
        onClientIdChange={(clientId) => updateFilters({ clientId })}
        clientsLoading={clientsLoading}
        destination={destination}
        onDestinationChange={(destination) => updateFilters({ destination })}
        status={status}
        onStatusChange={(status) => updateFilters({ status })}
      />

      {clientsError && (
        <Alert severity="warning" sx={{ mb: 2 }} onClose={() => setClientsError(null)}>
          {clientsError}
        </Alert>
      )}

      {currentError && (
        <Alert
          severity="error"
          sx={{ mb: 2, alignItems: "center" }}
          action={
            <BrandButton kind="outline" size="small" onClick={() => setRetry((value) => value + 1)}>
              Retry
            </BrandButton>
          }
        >
          {currentError}
        </Alert>
      )}

      {currentLoading ? (
        <AnalyticsLoadingSkeleton />
      ) : report?.type === "businessDemand" ? (
        <BusinessDemandTab analytics={report.data} />
      ) : (
        report?.type === "exportActivity" && (
          <ExportActivityTab
            analytics={report.data}
            recentExportsPaginationModel={recentExportsPaginationModel}
            onRecentExportsPaginationChange={handleRecentExportsPaginationChange}
          />
        )
      )}
    </>
  );
};
