import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { Alert, Tab, Tabs, Typography } from '@mui/material';
import type { GridPaginationModel } from '@mui/x-data-grid';
import type { Dayjs } from 'dayjs';
import { BrandButton } from '../components/common/BrandButton';
import { AnalyticsFilters } from '../components/analytics/AnalyticsFilters';
import {
  ALL_CLIENT_OPTION,
  type DateRangePreset,
  type DestinationFilterValue,
  formatApiDate,
  getPresetRange,
  type StatusFilterValue,
} from '../components/analytics/analyticsFilterUtils';
import { AnalyticsLoadingSkeleton } from '../components/analytics/AnalyticsLoadingSkeleton';
import { BusinessDemandTab } from '../components/analytics/BusinessDemandTab';
import { ExportActivityTab } from '../components/analytics/ExportActivityTab';
import { PageShell } from '../components/layout/PageShell';
import { useUserRoles } from '../hooks/useUserRoles';
import { adminAnalyticsService } from '../services/adminAnalytics.service';
import type {
  AnalyticsClientOption,
  BusinessDemandAnalytics,
  ExportActivityAnalytics,
} from '../types/analytics.types';

type AnalyticsTabValue = 'businessDemand' | 'exportActivity';

export const Analytics: React.FC = () => {
  const { isSuperAdmin } = useUserRoles();
  const [activeTab, setActiveTab] = useState<AnalyticsTabValue>('businessDemand');
  const [datePreset, setDatePreset] = useState<DateRangePreset>('last30');
  const [customFrom, setCustomFrom] = useState<Dayjs | null>(() => getPresetRange('last30').from);
  const [customTo, setCustomTo] = useState<Dayjs | null>(() => getPresetRange('last30').to);
  const [clientId, setClientId] = useState<string | null>(null);
  const [destination, setDestination] = useState<DestinationFilterValue>('all');
  const [status, setStatus] = useState<StatusFilterValue>('all');
  const [clientOptions, setClientOptions] = useState<AnalyticsClientOption[]>([]);
  const [clientsLoading, setClientsLoading] = useState(false);
  const [clientsError, setClientsError] = useState<string | null>(null);
  const [businessDemand, setBusinessDemand] = useState<BusinessDemandAnalytics | null>(null);
  const [businessDemandLoading, setBusinessDemandLoading] = useState(false);
  const [businessDemandError, setBusinessDemandError] = useState<string | null>(null);
  const [exportActivity, setExportActivity] = useState<ExportActivityAnalytics | null>(null);
  const [exportActivityLoading, setExportActivityLoading] = useState(false);
  const [exportActivityError, setExportActivityError] = useState<string | null>(null);
  const [recentExportsPaginationModel, setRecentExportsPaginationModel] =
    useState<GridPaginationModel>({
      page: 0,
      pageSize: 25,
    });

  const clientSelectOptions = useMemo(
    () => [ALL_CLIENT_OPTION, ...clientOptions],
    [clientOptions]
  );
  const selectedClient = useMemo(
    () =>
      clientSelectOptions.find((option) => option.id === (clientId ?? 'all')) ??
      ALL_CLIENT_OPTION,
    [clientId, clientSelectOptions]
  );

  const selectedRange = useMemo(() => {
    if (datePreset !== 'custom') {
      return getPresetRange(datePreset);
    }

    if (!customFrom || !customTo || !customFrom.isValid() || !customTo.isValid()) {
      return null;
    }

    return {
      from: customFrom.startOf('day'),
      to: customTo.startOf('day'),
    };
  }, [customFrom, customTo, datePreset]);

  const dateRangeError = useMemo(() => {
    if (datePreset !== 'custom') return null;
    if (!customFrom || !customTo) return 'Choose both custom dates.';
    if (!customFrom.isValid() || !customTo.isValid()) return 'Choose valid custom dates.';
    if (customFrom.startOf('day').isAfter(customTo.startOf('day'))) {
      return 'From date must be earlier than or equal to to date.';
    }
    return null;
  }, [customFrom, customTo, datePreset]);

  const analyticsQueryParams = useMemo(() => {
    if (!selectedRange || dateRangeError) return null;

    return {
      from: formatApiDate(selectedRange.from),
      to: formatApiDate(selectedRange.to),
      clientId: clientId ?? undefined,
      destination: destination === 'all' ? undefined : destination,
      status: status === 'all' ? undefined : status,
    };
  }, [clientId, dateRangeError, destination, selectedRange, status]);

  const loadBusinessDemand = useCallback(async () => {
    if (!isSuperAdmin || !analyticsQueryParams) return;

    setBusinessDemandLoading(true);
    setBusinessDemandError(null);
    try {
      const data = await adminAnalyticsService.getBusinessDemand(analyticsQueryParams);
      setBusinessDemand(data);
    } catch (err) {
      setBusinessDemandError(err instanceof Error ? err.message : 'Failed to load analytics.');
    } finally {
      setBusinessDemandLoading(false);
    }
  }, [analyticsQueryParams, isSuperAdmin]);

  const loadExportActivity = useCallback(async () => {
    if (!isSuperAdmin || !analyticsQueryParams) return;

    setExportActivityLoading(true);
    setExportActivityError(null);
    try {
      const data = await adminAnalyticsService.getExportActivity({
        ...analyticsQueryParams,
        page: recentExportsPaginationModel.page + 1,
        pageSize: recentExportsPaginationModel.pageSize,
      });
      setExportActivity(data);
    } catch (err) {
      setExportActivityError(err instanceof Error ? err.message : 'Failed to load export activity.');
    } finally {
      setExportActivityLoading(false);
    }
  }, [analyticsQueryParams, isSuperAdmin, recentExportsPaginationModel]);

  const loadActiveTab = useCallback(() => {
    if (activeTab === 'businessDemand') {
      void loadBusinessDemand();
      return;
    }

    void loadExportActivity();
  }, [activeTab, loadBusinessDemand, loadExportActivity]);

  useEffect(() => {
    setRecentExportsPaginationModel((current) =>
      current.page === 0 ? current : { ...current, page: 0 }
    );
  }, [analyticsQueryParams]);

  const handleRecentExportsPaginationChange = useCallback((model: GridPaginationModel) => {
    setRecentExportsPaginationModel(model);
  }, []);

  const handleTabChange = (_event: React.SyntheticEvent, value: AnalyticsTabValue) => {
    setActiveTab(value);
  };

  const currentLoading =
    activeTab === 'businessDemand' ? businessDemandLoading : exportActivityLoading;
  const currentError =
    activeTab === 'businessDemand' ? businessDemandError : exportActivityError;

  const clearCurrentError = () => {
    if (activeTab === 'businessDemand') {
      setBusinessDemandError(null);
      return;
    }

    setExportActivityError(null);
  };

  useEffect(() => {
    if (!isSuperAdmin) return;

    setClientsLoading(true);
    setClientsError(null);
    adminAnalyticsService
      .listClients()
      .then(setClientOptions)
      .catch((err) =>
        setClientsError(err instanceof Error ? err.message : 'Failed to load clients.')
      )
      .finally(() => setClientsLoading(false));
  }, [isSuperAdmin]);

  useEffect(() => {
    loadActiveTab();
  }, [loadActiveTab]);

  if (!isSuperAdmin) {
    return <Navigate to="/sites" replace />;
  }

  return (
    <PageShell title="Analytics" maxWidth="xl">
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Business demand and export activity based on client export requests.
      </Typography>

      <AnalyticsFilters
        datePreset={datePreset}
        onDatePresetChange={setDatePreset}
        customFrom={customFrom}
        onCustomFromChange={setCustomFrom}
        customTo={customTo}
        onCustomToChange={setCustomTo}
        dateRangeError={dateRangeError}
        clientSelectOptions={clientSelectOptions}
        selectedClient={selectedClient}
        onClientIdChange={setClientId}
        clientsLoading={clientsLoading}
        destination={destination}
        onDestinationChange={setDestination}
        status={status}
        onStatusChange={setStatus}
      />

      {clientsError && (
        <Alert severity="warning" sx={{ mb: 2 }} onClose={() => setClientsError(null)}>
          {clientsError}
        </Alert>
      )}

      <Tabs
        value={activeTab}
        onChange={handleTabChange}
        sx={{ borderBottom: 1, borderColor: 'divider', mb: 3 }}
      >
        <Tab label="Business Demand" value="businessDemand" />
        <Tab label="Export Activity" value="exportActivity" />
      </Tabs>

      {currentError && (
        <Alert
          severity="error"
          sx={{ mb: 2, alignItems: 'center' }}
          action={
            <BrandButton kind="outline" size="small" onClick={loadActiveTab}>
              Retry
            </BrandButton>
          }
          onClose={clearCurrentError}
        >
          {currentError}
        </Alert>
      )}

      {currentLoading ? (
        <AnalyticsLoadingSkeleton />
      ) : activeTab === 'businessDemand' ? (
        businessDemand && <BusinessDemandTab analytics={businessDemand} />
      ) : (
        exportActivity && (
          <ExportActivityTab
            analytics={exportActivity}
            recentExportsPaginationModel={recentExportsPaginationModel}
            onRecentExportsPaginationChange={handleRecentExportsPaginationChange}
          />
        )
      )}
    </PageShell>
  );
};
