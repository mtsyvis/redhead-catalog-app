import React, { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Divider,
  FormControlLabel,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material';

import { applicationSettingsService } from '../../../services/applicationSettings.service';
import { ApiClientError } from '../../../services/api.client';
import type {
  ApplicationSettingsLimits,
  ClientCatalogProtectionSettings,
} from '../../../types/applicationSettings.types';
import { BrandButton } from '../../common/BrandButton';
import { ApplicationSettingValueRow } from './ApplicationSettingValueRow';

interface ClientProtectionSettingsPanelProps {
  limits: ApplicationSettingsLimits | null;
  limitsLoading: boolean;
  limitsError: string | null;
  onRetryLimits: () => void;
}

function parsePositiveInt(value: string): number | null {
  const trimmed = value.trim();
  if (!/^\d+$/.test(trimmed)) return null;
  const parsed = Number(trimmed);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function formatDateTime(value: string | null): string | null {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toLocaleString();
}

function getErrorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiClientError ? error.message : fallback;
}

interface RelatedProtectionLimitsProps {
  limits: ApplicationSettingsLimits | null;
  loading: boolean;
  error: string | null;
  onRetry: () => void;
}

const RelatedProtectionLimits: React.FC<RelatedProtectionLimitsProps> = ({
  limits,
  loading,
  error,
  onRetry,
}) => (
  <Card>
    <CardContent sx={{ p: 3 }}>
      <Typography variant="h6">Related protection limits</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 1 }}>
        Current values used by the running application.
      </Typography>

      {loading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
          <CircularProgress size={28} />
        </Box>
      ) : error ? (
        <Alert
          severity="error"
          action={(
            <BrandButton kind="outline" size="small" onClick={onRetry}>
              Retry
            </BrandButton>
          )}
          sx={{ mt: 2 }}
        >
          {error}
        </Alert>
      ) : limits ? (
        <Stack divider={<Divider flexItem />}>
          <ApplicationSettingValueRow
            label="Default client selection"
            description={`Personal overrides can be set from 1 to ${limits.maxClientSelectionLimit.toLocaleString()}.`}
            value={`${limits.defaultClientSelectionLimit.toLocaleString()} sites`}
            source="Built in"
            changeBehavior="Deployment required"
          />
          <ApplicationSettingValueRow
            label="Request rate"
            value={`${limits.clientRequestsPerMinute.toLocaleString()} / minute`}
            source="Environment"
            changeBehavior="Restart required"
          />
          <ApplicationSettingValueRow
            label="Short-term site budget"
            description="Does not apply to Trusted clients."
            value={`${limits.clientUniqueSitesPerFiveMinutes.toLocaleString()} / 5 minutes`}
            source="Environment"
            changeBehavior="Restart required"
          />
          <ApplicationSettingValueRow
            label="Activity alert"
            description="Requests review but does not disable the account."
            value={`${limits.clientAlertUniqueSitesPerHour.toLocaleString()} / hour`}
            source="Environment"
            changeBehavior="Restart required"
          />
        </Stack>
      ) : null}
    </CardContent>
  </Card>
);

export const ClientProtectionSettingsPanel: React.FC<ClientProtectionSettingsPanelProps> = ({
  limits,
  limitsLoading,
  limitsError,
  onRetryLimits,
}) => {
  const [settings, setSettings] = useState<ClientCatalogProtectionSettings | null>(null);
  const [enabled, setEnabled] = useState(false);
  const [threshold, setThreshold] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [confirmationOpen, setConfirmationOpen] = useState(false);

  const loadSettings = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await applicationSettingsService.getClientProtection();
      setSettings(response);
      setEnabled(response.autoBanEnabled);
      setThreshold(String(response.autoBanUniqueSitesPer24Hours));
    } catch (loadError) {
      setError(getErrorMessage(loadError, 'Failed to load Client protection settings.'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadSettings();
  }, [loadSettings]);

  const parsedThreshold = parsePositiveInt(threshold);
  const thresholdInvalid = parsedThreshold === null;
  const hasChanges = settings !== null && parsedThreshold !== null && (
    enabled !== settings.autoBanEnabled ||
    parsedThreshold !== settings.autoBanUniqueSitesPer24Hours
  );
  const requiresConfirmation = settings !== null && parsedThreshold !== null && enabled && (
    !settings.autoBanEnabled || parsedThreshold < settings.autoBanUniqueSitesPer24Hours
  );

  const clearMessages = () => {
    setError(null);
    setSuccess(null);
  };

  const saveSettings = async () => {
    if (parsedThreshold === null) {
      setError('Enter a positive whole-number threshold.');
      return;
    }

    setSaving(true);
    clearMessages();
    try {
      const response = await applicationSettingsService.updateClientProtection({
        autoBanEnabled: enabled,
        autoBanUniqueSitesPer24Hours: parsedThreshold,
      });
      setSettings(response);
      setEnabled(response.autoBanEnabled);
      setThreshold(String(response.autoBanUniqueSitesPer24Hours));
      setSuccess('Client protection settings saved. Changes apply within one minute.');
      setConfirmationOpen(false);
    } catch (saveError) {
      setError(getErrorMessage(saveError, 'Failed to save Client protection settings.'));
      setConfirmationOpen(false);
    } finally {
      setSaving(false);
    }
  };

  const handleSave = () => {
    if (parsedThreshold === null) {
      setError('Enter a positive whole-number threshold.');
      return;
    }

    if (requiresConfirmation) {
      setConfirmationOpen(true);
      return;
    }

    void saveSettings();
  };

  const updatedAt = formatDateTime(settings?.updatedAtUtc ?? null);
  const updatedBy = settings?.updatedByDisplayName ?? settings?.updatedByUserId;

  return (
    <>
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', lg: 'minmax(0, 3fr) minmax(320px, 2fr)' },
          gap: 3,
          alignItems: 'start',
        }}
      >
        <Card>
          <CardContent sx={{ p: 3 }}>
            <Box
              sx={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'flex-start',
                gap: 2,
                flexWrap: 'wrap',
                mb: 2.5,
              }}
            >
              <Box>
                <Typography variant="h6">Automatic client bans</Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                  Disable suspicious Client accounts using rolling catalog activity.
                </Typography>
              </Box>
              {settings && (
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                  {hasChanges && (
                    <Chip label="Unsaved changes" color="warning" variant="outlined" size="small" />
                  )}
                  <Chip label="Database" variant="outlined" size="small" />
                  <Chip
                    label={settings.autoBanEnabled ? 'Enabled' : 'Disabled'}
                    color={settings.autoBanEnabled ? 'success' : 'default'}
                    variant={settings.autoBanEnabled ? 'filled' : 'outlined'}
                    size="small"
                  />
                </Box>
              )}
            </Box>

            {loading ? (
              <Box sx={{ display: 'flex', justifyContent: 'center', py: 5 }}>
                <CircularProgress />
              </Box>
            ) : !settings ? (
              <Alert
                severity="error"
                action={(
                  <BrandButton kind="outline" size="small" onClick={() => void loadSettings()}>
                    Retry
                  </BrandButton>
                )}
              >
                {error ?? 'Client protection settings are not available.'}
              </Alert>
            ) : (
              <Stack spacing={2.5}>
                {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}
                {success && <Alert severity="success" onClose={() => setSuccess(null)}>{success}</Alert>}

                <FormControlLabel
                  control={(
                    <Switch
                      checked={enabled}
                      onChange={(event) => {
                        setEnabled(event.target.checked);
                        clearMessages();
                      }}
                      disabled={saving}
                    />
                  )}
                  label="Enable automatic bans"
                />

                <Box
                  sx={{
                    display: 'grid',
                    gridTemplateColumns: { xs: '1fr', sm: 'minmax(0, 1fr) minmax(220px, 1fr)' },
                    gap: 2,
                    alignItems: 'start',
                  }}
                >
                  <TextField
                    label="Unique-site threshold"
                    type="number"
                    value={threshold}
                    onChange={(event) => {
                      setThreshold(event.target.value);
                      clearMessages();
                    }}
                    error={thresholdInvalid}
                    helperText={thresholdInvalid
                      ? 'Enter a positive whole number.'
                      : 'Active Clients are disabled at or above this value.'}
                    disabled={saving}
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    fullWidth
                  />
                  <Box sx={{ border: 1, borderColor: 'divider', borderRadius: 1, px: 1.75, py: 1.25 }}>
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                      Rolling period
                    </Typography>
                    <Typography variant="body2" sx={{ fontWeight: 600, mt: 0.25 }}>
                      24 hours
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      Trusted clients are always excluded.
                    </Typography>
                  </Box>
                </Box>

                {requiresConfirmation && (
                  <Alert severity="warning">
                    Enabling automatic bans or lowering the threshold evaluates activity already recorded
                    during the previous 24 hours. Eligible clients may be disabled on the next scan.
                  </Alert>
                )}

                <Divider />

                <Box
                  sx={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: { xs: 'flex-start', sm: 'center' },
                    flexDirection: { xs: 'column', sm: 'row' },
                    gap: 1.5,
                  }}
                >
                  <Typography variant="caption" color="text.secondary">
                    {updatedAt
                      ? `Last updated ${updatedAt}${updatedBy ? ` by ${updatedBy}` : ''}.`
                      : 'Not changed since initial setup.'}
                    {' '}Changes apply within one minute.
                  </Typography>
                  <BrandButton
                    onClick={handleSave}
                    disabled={saving || !hasChanges || parsedThreshold === null}
                    sx={{ flexShrink: 0 }}
                  >
                    {saving ? <CircularProgress size={20} color="inherit" /> : 'Save protection settings'}
                  </BrandButton>
                </Box>
              </Stack>
            )}
          </CardContent>
        </Card>

        <RelatedProtectionLimits
          limits={limits}
          loading={limitsLoading}
          error={limitsError}
          onRetry={onRetryLimits}
        />
      </Box>

      <Dialog open={confirmationOpen} onClose={() => !saving && setConfirmationOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>
          {settings?.autoBanEnabled ? 'Apply stricter automatic-ban settings?' : 'Enable automatic bans?'}
        </DialogTitle>
        <DialogContent>
          <DialogContentText>
            Activity already recorded during the previous 24 hours will be evaluated. Eligible Clients
            that have reached the {parsedThreshold?.toLocaleString()}-site threshold may be disabled within one minute.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <BrandButton kind="outline" onClick={() => setConfirmationOpen(false)} disabled={saving}>
            Cancel
          </BrandButton>
          <BrandButton onClick={() => void saveSettings()} disabled={saving}>
            {saving ? <CircularProgress size={20} color="inherit" /> : 'Apply settings'}
          </BrandButton>
        </DialogActions>
      </Dialog>
    </>
  );
};
