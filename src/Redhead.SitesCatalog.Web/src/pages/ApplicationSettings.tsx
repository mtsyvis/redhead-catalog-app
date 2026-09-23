import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Navigate, useSearchParams } from 'react-router-dom';
import { Box, Tab, Tabs, Typography } from '@mui/material';

import { ClientProtectionSettingsPanel } from '../components/admin/settings/ClientProtectionSettingsPanel';
import { SystemLimitsPanel } from '../components/admin/settings/SystemLimitsPanel';
import { PageShell } from '../components/layout/PageShell';
import { useUserRoles } from '../hooks/useUserRoles';
import { ApiClientError } from '../services/api.client';
import { applicationSettingsService } from '../services/applicationSettings.service';
import type { ApplicationSettingsLimits } from '../types/applicationSettings.types';
import { RoleSettingsPanel } from './RoleSettings';

type SettingsSection = 'roles' | 'protection' | 'limits';

interface SettingsTabPanelProps {
  activeSection: SettingsSection;
  section: SettingsSection;
  children: React.ReactNode;
}

const SettingsTabPanel: React.FC<SettingsTabPanelProps> = ({
  activeSection,
  section,
  children,
}) => (
  <Box
    role="tabpanel"
    hidden={activeSection !== section}
    id={`${section}-settings-panel`}
    aria-labelledby={`${section}-settings-tab`}
    sx={{ pt: 3 }}
  >
    {children}
  </Box>
);

export const ApplicationSettings: React.FC = () => {
  const { canReadRoleSettings, isSuperAdmin } = useUserRoles();
  const [searchParams, setSearchParams] = useSearchParams();
  const [limits, setLimits] = useState<ApplicationSettingsLimits | null>(null);
  const [limitsLoading, setLimitsLoading] = useState(true);
  const [limitsError, setLimitsError] = useState<string | null>(null);

  const requestedSection = searchParams.get('section');
  const activeSection = useMemo<SettingsSection>(() => {
    if (requestedSection === 'limits') return 'limits';
    if (requestedSection === 'protection' && isSuperAdmin) return 'protection';
    return 'roles';
  }, [isSuperAdmin, requestedSection]);

  useEffect(() => {
    if (requestedSection !== activeSection) {
      setSearchParams({ section: activeSection }, { replace: true });
    }
  }, [activeSection, requestedSection, setSearchParams]);

  const loadLimits = useCallback(async () => {
    if (!canReadRoleSettings) return;

    setLimitsLoading(true);
    setLimitsError(null);

    try {
      setLimits(await applicationSettingsService.getLimits());
    } catch (loadError) {
      setLimits(null);
      setLimitsError(
        loadError instanceof ApiClientError
          ? loadError.message
          : 'Failed to load application limits.',
      );
    } finally {
      setLimitsLoading(false);
    }
  }, [canReadRoleSettings]);

  useEffect(() => {
    void loadLimits();
  }, [loadLimits]);

  if (!canReadRoleSettings) {
    return <Navigate to="/sites" replace />;
  }

  const handleSectionChange = (_event: React.SyntheticEvent, value: SettingsSection) => {
    setSearchParams({ section: value });
  };

  return (
    <PageShell title="Application settings" maxWidth="lg">
      <Typography variant="body2" color="text.secondary">
        Manage role policies, Client protection and the application&apos;s current system limits.
      </Typography>

      <Tabs
        value={activeSection}
        onChange={handleSectionChange}
        variant="scrollable"
        scrollButtons="auto"
        sx={{ mt: 2, borderBottom: 1, borderColor: 'divider' }}
        aria-label="Application settings sections"
      >
        <Tab
          id="roles-settings-tab"
          aria-controls="roles-settings-panel"
          label="Roles & exports"
          value="roles"
        />
        {isSuperAdmin && (
          <Tab
            id="protection-settings-tab"
            aria-controls="protection-settings-panel"
            label="Client protection"
            value="protection"
          />
        )}
        <Tab
          id="limits-settings-tab"
          aria-controls="limits-settings-panel"
          label="System limits"
          value="limits"
        />
      </Tabs>

      <SettingsTabPanel activeSection={activeSection} section="roles">
        <RoleSettingsPanel />
      </SettingsTabPanel>

      {isSuperAdmin && (
        <SettingsTabPanel activeSection={activeSection} section="protection">
          <ClientProtectionSettingsPanel
            limits={limits}
            limitsLoading={limitsLoading}
            limitsError={limitsError}
            onRetryLimits={() => void loadLimits()}
          />
        </SettingsTabPanel>
      )}

      <SettingsTabPanel activeSection={activeSection} section="limits">
        <SystemLimitsPanel
          limits={limits}
          loading={limitsLoading}
          error={limitsError}
          onRetry={() => void loadLimits()}
        />
      </SettingsTabPanel>
    </PageShell>
  );
};
