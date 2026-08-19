import { useEffect, useState, type SyntheticEvent } from 'react';
import {
  Alert,
  Autocomplete,
  Box,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  MenuItem,
  Switch,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import type {
  LocationFilterOption,
  ServiceAvailabilityStatusValue,
  Site,
} from '../../../types/sites.types';
import { sitesService } from '../../../services/sites.service';
import { ApiClientError } from '../../../services/api.client';
import { useChangeHistory } from '../../../hooks/useChangeHistory';
import { BrandButton } from '../../common/BrandButton';
import { ChangeHistoryContent } from '../../common/ChangeHistoryContent';
import { SERVICE_AVAILABILITY_STATUS } from '../../../utils/serviceAvailability';
import { LANGUAGE_OPTIONS, getLanguageOption } from '../../../utils/language';
import {
  PRICE_TYPE,
  TERM_KEY_OPTIONS,
  type PriceTypeValue,
} from '../../../utils/pricing';
import {
  buildUpdateSitePayload,
  clearFieldError,
  CONFIRM_CLEAR_SERVICE_PRICES_MESSAGE,
  createEmptyPricingRow,
  createInitialFormState,
  EMPTY_FORM_STATE,
  getEditSiteFormSignature,
  getPriceRowsForType,
  validateEditSiteForm,
} from './EditSiteDialog.helpers';
import type { EditSiteFormState, PricingPriceRow } from './EditSiteDialog.helpers';
import {
  EditSitePricingTab,
} from './EditSitePricingTab';
import {
  createDefaultPricingExpansion,
  type PricingExpansionState,
} from './EditSitePricingTab.utils';

type Props = {
  open: boolean;
  site: Site | null;
  onClose: () => void;
  onSaved: (updated: Site) => void;
};

const OTHER_LOCATION_FORM_VALUE = '__OTHER__';

function formatDateTime(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleString(undefined, {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
}

function resolveInitialLocationValue(
  site: Site,
  locationOptions: LocationFilterOption[]
): string {
  if (locationOptions.length === 0) return site.location ?? '';
  if (site.location === 'Other') return OTHER_LOCATION_FORM_VALUE;
  if (locationOptions.some((option) => option.key === site.location)) return site.location;

  return locationOptions.find((option) => option.displayName === site.location)?.key ?? site.location;
}

function clearPricingFieldErrors(errors: Record<string, string[]>): Record<string, string[]> {
  const entries = Object.entries(errors).filter(([key]) => !key.startsWith('pricing.'));
  return entries.length === Object.keys(errors).length ? errors : Object.fromEntries(entries);
}

function getNextTermKey(rows: PricingPriceRow[], priceType: PriceTypeValue): string {
  const usedTerms = new Set(
    rows.filter((row) => row.priceType === priceType).map((row) => row.termKey)
  );
  return TERM_KEY_OPTIONS.find((option) => !usedTerms.has(option.termKey))?.termKey ?? 'unknown';
}

export function EditSiteDialog({ open, site, onClose, onSaved }: Readonly<Props>) {
  const [tab, setTab] = useState(0);
  const [form, setForm] = useState<EditSiteFormState>(() =>
    site ? createInitialFormState(site) : EMPTY_FORM_STATE
  );
  const [initialFormSignature, setInitialFormSignature] = useState(() =>
    getEditSiteFormSignature(site ? createInitialFormState(site) : EMPTY_FORM_STATE)
  );
  const [saving, setSaving] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [expandedPricingSections, setExpandedPricingSections] = useState<PricingExpansionState>(
    () => createDefaultPricingExpansion(site ? createInitialFormState(site) : EMPTY_FORM_STATE)
  );
  const [locationOptions, setLocationOptions] = useState<LocationFilterOption[]>([]);
  const [locationOptionsLoading, setLocationOptionsLoading] = useState(false);
  const [locationOptionsError, setLocationOptionsError] = useState<string | null>(null);
  const history = useChangeHistory(open && site ? site.domain : null, async () => {
    if (!site) return [];
    return sitesService.getHistory(site.domain);
  });

  useEffect(() => {
    if (!open || !site) return;
    const nextForm = createInitialFormState(site);
    setForm(nextForm);
    setInitialFormSignature(getEditSiteFormSignature(nextForm));
    setFieldErrors({});
    setExpandedPricingSections(createDefaultPricingExpansion(nextForm));
    setSaving(false);
    setTab(0);
  }, [open, site]);

  useEffect(() => {
    if (!open) return;

    let cancelled = false;
    const loadLocationOptions = async () => {
      setLocationOptionsLoading(true);
      setLocationOptionsError(null);
      try {
        const data = await sitesService.getFilterOptions();
        if (cancelled) return;

        const locations = data.locations;
        if (!locations) {
          setLocationOptions([]);
          setLocationOptionsError('Location options could not be loaded.');
          return;
        }

        const optionsByKey = new Map<string, LocationFilterOption>();
        for (const option of locations.locations) {
          optionsByKey.set(option.key, option);
        }
        optionsByKey.set(locations.special.unknown.key, locations.special.unknown);
        setLocationOptions([...optionsByKey.values()]);
      } catch (error) {
        console.error('Failed to load location options:', error);
        if (!cancelled) {
          setLocationOptions([]);
          setLocationOptionsError('Location options could not be loaded.');
        }
      } finally {
        if (!cancelled) setLocationOptionsLoading(false);
      }
    };

    void loadLocationOptions();
    return () => {
      cancelled = true;
    };
  }, [open]);

  useEffect(() => {
    if (!open || !site || locationOptions.length === 0) return;

    const resolvedLocation = resolveInitialLocationValue(site, locationOptions);
    setInitialFormSignature(getEditSiteFormSignature({
      ...createInitialFormState(site),
      location: resolvedLocation,
    }));

    setForm((previous) => {
      if (locationOptions.some((option) => option.key === previous.location)) return previous;
      if (site.location === 'Other') return { ...previous, location: resolvedLocation };
      return resolvedLocation !== site.location
        ? { ...previous, location: resolvedLocation }
        : previous;
    });
  }, [open, site, locationOptions]);

  const updateField = <K extends keyof EditSiteFormState>(
    key: K,
    value: EditSiteFormState[K]
  ) => {
    setForm((previous) => ({ ...previous, [key]: value }));
    setFieldErrors((previous) => clearFieldError(previous, key));
  };

  const handleAddPrice = (priceType: PriceTypeValue) => {
    setForm((previous) => {
      const termKey = getNextTermKey(previous.pricingRows, priceType);
      const nextRow = { ...createEmptyPricingRow(priceType), termKey };
      return {
        ...previous,
        pricingRows: [...previous.pricingRows, nextRow],
        pricingStatuses:
          priceType === PRICE_TYPE.Main
            ? previous.pricingStatuses
            : {
                ...previous.pricingStatuses,
                [priceType]: SERVICE_AVAILABILITY_STATUS.Available,
              },
      };
    });
    setExpandedPricingSections((previous) => ({ ...previous, [priceType]: true }));
    setFieldErrors(clearPricingFieldErrors);
  };

  const handleDeletePrice = (rowId: string) => {
    setForm((previous) => {
      const removedRow = previous.pricingRows.find((row) => row.id === rowId);
      const pricingRows = previous.pricingRows.filter((row) => row.id !== rowId);
      const shouldResetStatus =
        removedRow &&
        removedRow.priceType !== PRICE_TYPE.Main &&
        !pricingRows.some((row) => row.priceType === removedRow.priceType);

      return {
        ...previous,
        pricingRows,
        pricingStatuses: shouldResetStatus
          ? {
              ...previous.pricingStatuses,
              [removedRow.priceType]: SERVICE_AVAILABILITY_STATUS.Unknown,
            }
          : previous.pricingStatuses,
      };
    });
    setFieldErrors(clearPricingFieldErrors);
  };

  const handlePriceRowChange = (
    rowId: string,
    key: 'termKey' | 'amountUsd',
    value: string
  ) => {
    setForm((previous) => ({
      ...previous,
      pricingRows: previous.pricingRows.map((row) =>
        row.id === rowId ? { ...row, [key]: value } : row
      ),
    }));
    setFieldErrors(clearPricingFieldErrors);
  };

  const handleServiceStatusChange = (
    priceType: PriceTypeValue,
    status: ServiceAvailabilityStatusValue
  ) => {
    const existingRows = getPriceRowsForType(form, priceType);
    if (
      status !== SERVICE_AVAILABILITY_STATUS.Available &&
      existingRows.length > 0 &&
      !window.confirm(CONFIRM_CLEAR_SERVICE_PRICES_MESSAGE)
    ) {
      return;
    }

    setForm((previous) => ({
      ...previous,
      pricingRows:
        status === SERVICE_AVAILABILITY_STATUS.Available
          ? previous.pricingRows
          : previous.pricingRows.filter((row) => row.priceType !== priceType),
      pricingStatuses: { ...previous.pricingStatuses, [priceType]: status },
    }));
    setFieldErrors(clearPricingFieldErrors);
  };

  const selectTabForErrors = (errors: Record<string, string[]>) => {
    setTab(Object.keys(errors).some((key) => key.startsWith('pricing.')) ? 1 : 0);
  };

  const handleSave = async () => {
    if (!site) return;

    const localErrors = validateEditSiteForm(form);
    if (!locationOptions.some((option) => option.key === form.location)) {
      localErrors.location = ['Select a valid location or Unknown.'];
    }

    if (Object.keys(localErrors).length > 0) {
      setFieldErrors(localErrors);
      selectTabForErrors(localErrors);
      setExpandedPricingSections((previous) => ({
        ...previous,
        ...createDefaultPricingExpansion(form, localErrors),
      }));
      return;
    }

    setFieldErrors({});
    setSaving(true);
    try {
      const updated = await sitesService.updateSite(
        site.domain,
        buildUpdateSitePayload(form)
      );
      onSaved(updated);
    } catch (err) {
      if (err instanceof ApiClientError && err.fieldErrors) {
        setFieldErrors(err.fieldErrors);
        selectTabForErrors(err.fieldErrors);
        setExpandedPricingSections((previous) => ({
          ...previous,
          ...createDefaultPricingExpansion(form, err.fieldErrors),
        }));
      } else {
        setFieldErrors({ _form: [err instanceof Error ? err.message : 'Update failed'] });
      }
    } finally {
      setSaving(false);
    }
  };

  const handleTabChange = (_event: SyntheticEvent, nextTab: number) => {
    setTab(nextTab);
    if (nextTab === 2) void history.load();
  };

  const currentLocationOption =
    locationOptions.find((option) => option.key === form.location) ??
    (form.location === OTHER_LOCATION_FORM_VALUE
      ? { key: OTHER_LOCATION_FORM_VALUE, displayName: 'Other' }
      : null);
  const editLocationOptions =
    currentLocationOption?.key === OTHER_LOCATION_FORM_VALUE
      ? [currentLocationOption, ...locationOptions]
      : locationOptions;
  const hasValidLocation = locationOptions.some((option) => option.key === form.location);
  const isDirty = getEditSiteFormSignature(form) !== initialFormSignature;
  const canSave =
    Boolean(site) &&
    isDirty &&
    !saving &&
    !locationOptionsLoading &&
    !locationOptionsError &&
    hasValidLocation;
  const currentLanguageOption = getLanguageOption(form.language);
  const languageOptions =
    currentLanguageOption &&
    !LANGUAGE_OPTIONS.some((option) => option.value === currentLanguageOption.value)
      ? [...LANGUAGE_OPTIONS, currentLanguageOption]
      : LANGUAGE_OPTIONS;
  const importedOtherLocationValue =
    site?.location === 'Other' ? site.importedLocationRaw?.trim() : undefined;
  const locationNeedsReplacement = form.location === OTHER_LOCATION_FORM_VALUE;
  const locationHelperText =
    fieldErrors.location?.[0] ??
    locationOptionsError ??
    (locationNeedsReplacement ? (
      <Box
        component="span"
        sx={{
          display: 'inline-flex',
          alignItems: 'center',
          gap: 0.5,
          color: 'warning.dark',
          fontWeight: 600,
        }}
      >
        <WarningAmberIcon sx={{ fontSize: 16 }} />
        {importedOtherLocationValue
          ? `Imported value: ${importedOtherLocationValue}. Choose a canonical location or Unknown to replace Other.`
          : 'Choose a canonical location or Unknown to replace Other.'}
      </Box>
    ) : undefined);
  const lastUpdatedAt = site?.updatedAtUtc ?? site?.createdAtUtc;
  const lastUpdatedBy = site?.updatedBy ?? site?.createdBy ?? 'system';

  const close = () => {
    if (!saving) onClose();
  };

  return (
    <Dialog
      open={open}
      onClose={close}
      maxWidth="lg"
      fullWidth
      slotProps={{ paper: { sx: { height: 'calc(100% - 64px)' } } }}
    >
      <DialogTitle sx={{ pb: site ? 1.5 : 2 }}>
        <Box
          sx={{
            display: 'flex',
            alignItems: { xs: 'flex-start', sm: 'center' },
            justifyContent: 'space-between',
            flexDirection: { xs: 'column', sm: 'row' },
            gap: 0.5,
            minWidth: 0,
          }}
        >
          <Typography variant="h6" component="div" sx={{ fontWeight: 600, minWidth: 0 }}>
            Edit site
            {site && (
              <Box component="span" sx={{ color: 'text.secondary', fontWeight: 400 }}>
                {' '}· {site.domain}
              </Box>
            )}
          </Typography>
          {site && lastUpdatedAt && (
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{ flexShrink: 0, textAlign: { xs: 'left', sm: 'right' } }}
            >
              Updated {formatDateTime(lastUpdatedAt)} by {lastUpdatedBy}
            </Typography>
          )}
        </Box>
      </DialogTitle>

      {site && (
        <Tabs
          value={tab}
          onChange={handleTabChange}
          sx={{
            flexShrink: 0,
            px: { xs: 1, sm: 3 },
            borderTop: 1,
            borderBottom: 1,
            borderColor: 'divider',
          }}
        >
          <Tab label="Site details" />
          <Tab label="Prices" />
          <Tab label="History" />
        </Tabs>
      )}

      <DialogContent sx={{ display: 'flex', flexDirection: 'column' }}>
        {fieldErrors._form?.[0] && (
          <Alert severity="error" sx={{ mb: 2 }}>{fieldErrors._form[0]}</Alert>
        )}

        {site && tab === 0 && (
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', md: 'minmax(0, 1fr) minmax(0, 1fr)' },
              gap: 2,
              flex: 1,
              minHeight: 0,
            }}
          >
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
              <Typography variant="subtitle2">Site information &amp; status</Typography>
              <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 1 }}>
                <TextField
                  label="DR"
                  type="number"
                  inputProps={{ min: 0, max: 100 }}
                  value={form.dr}
                  onChange={(event) => updateField('dr', event.target.value)}
                  size="small"
                  error={Boolean(fieldErrors.dr?.length)}
                  helperText={fieldErrors.dr?.[0]}
                />
                <TextField
                  label="Traffic"
                  type="number"
                  inputProps={{ min: 0, step: 1 }}
                  value={form.traffic}
                  onChange={(event) => updateField('traffic', event.target.value)}
                  size="small"
                  error={Boolean(fieldErrors.traffic?.length)}
                  helperText={fieldErrors.traffic?.[0]}
                />
              </Box>

              <Autocomplete
                size="small"
                options={editLocationOptions}
                value={currentLocationOption}
                loading={locationOptionsLoading}
                disabled={Boolean(locationOptionsError)}
                getOptionLabel={(option) => option.displayName}
                getOptionDisabled={(option) => option.key === OTHER_LOCATION_FORM_VALUE}
                isOptionEqualToValue={(option, value) => option.key === value.key}
                onChange={(_, option) => updateField('location', option?.key ?? '')}
                renderInput={(params) => (
                  <TextField
                    {...params}
                    label="Location"
                    color={locationNeedsReplacement ? 'warning' : 'primary'}
                    error={Boolean(fieldErrors.location?.length || locationOptionsError)}
                    helperText={locationHelperText}
                    sx={locationNeedsReplacement ? {
                      '& .MuiOutlinedInput-notchedOutline': { borderColor: 'warning.main' },
                      '&:hover .MuiOutlinedInput-notchedOutline': { borderColor: 'warning.dark' },
                    } : undefined}
                    InputProps={{
                      ...params.InputProps,
                      endAdornment: (
                        <>
                          {locationOptionsLoading && <CircularProgress color="inherit" size={18} />}
                          {params.InputProps.endAdornment}
                        </>
                      ),
                    }}
                  />
                )}
              />

              {locationOptionsError && (
                <Alert severity="warning">
                  Location cannot be edited until options are available.
                </Alert>
              )}

              <TextField
                select
                label="Language"
                value={form.language}
                onChange={(event) => updateField('language', event.target.value)}
                size="small"
                error={Boolean(fieldErrors.language?.length)}
                helperText={fieldErrors.language?.[0] ?? 'Optional'}
              >
                <MenuItem value="">Empty</MenuItem>
                {languageOptions.map((option) => (
                  <MenuItem key={option.value} value={option.value}>{option.label}</MenuItem>
                ))}
              </TextField>

              <Box sx={{ borderTop: 1, borderColor: 'divider', pt: 1 }}>
                <FormControlLabel
                  control={(
                    <Switch
                      checked={form.isQuarantined}
                      onChange={(event) => updateField('isQuarantined', event.target.checked)}
                    />
                  )}
                  label="Unavailable (quarantined)"
                />
              </Box>
              {form.isQuarantined && (
                <TextField
                  label="Reason (optional)"
                  value={form.quarantineReason}
                  onChange={(event) => updateField('quarantineReason', event.target.value)}
                  size="small"
                  multiline
                  minRows={2}
                  maxRows={4}
                  error={Boolean(fieldErrors.quarantineReason?.length)}
                  helperText={fieldErrors.quarantineReason?.[0]}
                />
              )}
            </Box>

            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
              <Typography variant="subtitle2">Content details</Typography>
              <TextField
                label="Niche"
                value={form.niche}
                onChange={(event) => updateField('niche', event.target.value)}
                size="small"
                error={Boolean(fieldErrors.niche?.length)}
                helperText={fieldErrors.niche?.[0]}
              />
              <TextField
                label="Number DF Links"
                type="number"
                inputProps={{ min: 1, step: 1 }}
                value={form.numberDFLinks}
                onChange={(event) => updateField('numberDFLinks', event.target.value)}
                size="small"
                error={Boolean(fieldErrors.numberDFLinks?.length)}
                helperText={fieldErrors.numberDFLinks?.[0] ?? 'Optional positive whole number'}
              />
              <TextField
                label="Sponsored Tag"
                value={form.sponsoredTag}
                onChange={(event) => updateField('sponsoredTag', event.target.value)}
                size="small"
                error={Boolean(fieldErrors.sponsoredTag?.length)}
                helperText={fieldErrors.sponsoredTag?.[0]}
              />
              <TextField
                label="Categories"
                value={form.categories}
                onChange={(event) => updateField('categories', event.target.value)}
                size="small"
                multiline
                minRows={3}
                maxRows={6}
                error={Boolean(fieldErrors.categories?.length)}
                helperText={fieldErrors.categories?.[0]}
                sx={{
                  flex: { md: 1 },
                  minHeight: { md: 0 },
                  '& .MuiInputBase-root': {
                    height: { md: '100%' },
                    alignItems: 'flex-start',
                  },
                  '& .MuiInputBase-inputMultiline': {
                    height: { md: '100% !important' },
                    maxHeight: { md: 'none !important' },
                    overflow: { md: 'auto !important' },
                  },
                }}
              />
            </Box>
          </Box>
        )}

        {site && tab === 1 && (
          <EditSitePricingTab
            form={form}
            fieldErrors={fieldErrors}
            expandedSections={expandedPricingSections}
            onSectionChange={(priceType, expanded) =>
              setExpandedPricingSections((previous) => ({
                ...previous,
                [priceType]: expanded,
              }))
            }
            onServiceStatusChange={handleServiceStatusChange}
            onPriceRowChange={handlePriceRowChange}
            onDeletePrice={handleDeletePrice}
            onAddPrice={handleAddPrice}
          />
        )}

        {site && tab === 2 && (
          <ChangeHistoryContent
            items={history.items}
            loading={history.loading}
            error={history.error}
          />
        )}
      </DialogContent>

      <DialogActions sx={{ borderTop: 1, borderColor: 'divider' }}>
        <BrandButton onClick={close} disabled={saving}>Cancel</BrandButton>
        <BrandButton kind="primary" onClick={handleSave} disabled={!canSave}>
          {saving ? <CircularProgress size={18} color="inherit" /> : 'Save changes'}
        </BrandButton>
      </DialogActions>
    </Dialog>
  );
}
