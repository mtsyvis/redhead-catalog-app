import { useEffect, useState } from 'react';
import {
  Alert,
  Autocomplete,
  Box,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  FormControlLabel,
  MenuItem,
  Paper,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';

import type {
  LocationFilterOption,
  ServiceAvailabilityStatusValue,
  Site,
} from '../../../types/sites.types';
import { sitesService } from '../../../services/sites.service';
import { ApiClientError } from '../../../services/api.client';
import { BrandButton } from '../../common/BrandButton';
import {
  SERVICE_AVAILABILITY_STATUS,
  SERVICE_AVAILABILITY_STATUS_OPTIONS,
} from '../../../utils/serviceAvailability';
import { LANGUAGE_OPTIONS, getLanguageOption } from '../../../utils/language';
import {
  PRICE_TYPE,
  TERM_KEY_OPTIONS,
  type PriceTypeValue,
  formatTermFilterLabel,
} from '../../../utils/pricing';
import {
  buildUpdateSitePayload,
  clearFieldError,
  CONFIRM_CLEAR_SERVICE_PRICES_MESSAGE,
  createEmptyPricingRow,
  createInitialFormState,
  EMPTY_FORM_STATE,
  getPriceRowsForType,
  pricingAmountErrorKey,
  pricingStatusErrorKey,
  pricingTermErrorKey,
  PRICING_SECTIONS,
  validateEditSiteForm,
} from './EditSiteDialog.helpers';
import type { EditSiteFormState, PricingPriceRow } from './EditSiteDialog.helpers';

// --- Types ---

type Props = {
  open: boolean;
  site: Site | null;
  onClose: () => void;
  onSaved: (updated: Site) => void;
};

const OTHER_LOCATION_FORM_VALUE = '__OTHER__';

function clearPricingFieldErrors(errors: Record<string, string[]>): Record<string, string[]> {
  const entries = Object.entries(errors).filter(([key]) => !key.startsWith('pricing.'));
  return entries.length === Object.keys(errors).length ? errors : Object.fromEntries(entries);
}

function getTermOptionsForRow(row: PricingPriceRow) {
  if (TERM_KEY_OPTIONS.some((option) => option.termKey === row.termKey)) {
    return TERM_KEY_OPTIONS;
  }

  return [
    ...TERM_KEY_OPTIONS,
    { termKey: row.termKey, label: formatTermFilterLabel(row.termKey) },
  ];
}

function getNextTermKey(rows: PricingPriceRow[], priceType: PriceTypeValue): string {
  const usedTerms = new Set(
    rows.filter((row) => row.priceType === priceType).map((row) => row.termKey)
  );
  return TERM_KEY_OPTIONS.find((option) => !usedTerms.has(option.termKey))?.termKey ?? 'unknown';
}

// --- Main component ---

export function EditSiteDialog({ open, site, onClose, onSaved }: Readonly<Props>) {
  const [form, setForm] = useState<EditSiteFormState>(
    site ? createInitialFormState(site) : EMPTY_FORM_STATE
  );
  const [saving, setSaving] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [locationOptions, setLocationOptions] = useState<LocationFilterOption[]>([]);
  const [locationOptionsLoading, setLocationOptionsLoading] = useState(false);
  const [locationOptionsError, setLocationOptionsError] = useState<string | null>(null);

  useEffect(() => {
    if (!open || !site) return;
    setForm(createInitialFormState(site));
    setFieldErrors({});
    setSaving(false);
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
        if (!cancelled) {
          setLocationOptionsLoading(false);
        }
      }
    };

    loadLocationOptions();

    return () => {
      cancelled = true;
    };
  }, [open]);

  useEffect(() => {
    if (!open || !site || locationOptions.length === 0) return;

    setForm((prev) => {
      if (locationOptions.some((option) => option.key === prev.location)) {
        return prev;
      }

      if (site.location === 'Other') {
        return { ...prev, location: OTHER_LOCATION_FORM_VALUE };
      }

      const matchingOption = locationOptions.find((option) => option.displayName === site.location);
      return matchingOption ? { ...prev, location: matchingOption.key } : prev;
    });
  }, [open, site, locationOptions]);

  const updateField = <K extends keyof EditSiteFormState>(
    key: K,
    value: EditSiteFormState[K]
  ) => {
    const nextForm = { ...form, [key]: value };
    setForm(nextForm);
    setFieldErrors((prev) => clearFieldError(prev, key));
  };

  const handleAddPrice = (priceType: PriceTypeValue) => {
    setForm((prev) => {
      const termKey = getNextTermKey(prev.pricingRows, priceType);
      const nextRow = { ...createEmptyPricingRow(priceType), termKey };
      return {
        ...prev,
        pricingRows: [...prev.pricingRows, nextRow],
        pricingStatuses:
          priceType === PRICE_TYPE.Main
            ? prev.pricingStatuses
            : { ...prev.pricingStatuses, [priceType]: SERVICE_AVAILABILITY_STATUS.Available },
      };
    });
    setFieldErrors(clearPricingFieldErrors);
  };

  const handleDeletePrice = (rowId: string) => {
    setForm((prev) => {
      const removedRow = prev.pricingRows.find((row) => row.id === rowId);
      const pricingRows = prev.pricingRows.filter((row) => row.id !== rowId);
      const shouldResetStatus =
        removedRow &&
        removedRow.priceType !== PRICE_TYPE.Main &&
        !pricingRows.some((row) => row.priceType === removedRow.priceType);

      return {
        ...prev,
        pricingRows,
        pricingStatuses: shouldResetStatus
          ? {
              ...prev.pricingStatuses,
              [removedRow.priceType]: SERVICE_AVAILABILITY_STATUS.Unknown,
            }
          : prev.pricingStatuses,
      };
    });
    setFieldErrors(clearPricingFieldErrors);
  };

  const handlePriceRowChange = (
    rowId: string,
    key: 'termKey' | 'amountUsd',
    value: string
  ) => {
    setForm((prev) => ({
      ...prev,
      pricingRows: prev.pricingRows.map((row) =>
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

    setForm((prev) => {
      return {
        ...prev,
        pricingRows:
          status === SERVICE_AVAILABILITY_STATUS.Available
            ? prev.pricingRows
            : prev.pricingRows.filter((row) => row.priceType !== priceType),
        pricingStatuses: { ...prev.pricingStatuses, [priceType]: status },
      };
    });
    setFieldErrors(clearPricingFieldErrors);
  };

  const handleSave = async () => {
    if (!site) return;

    const localErrors = validateEditSiteForm(form);
    if (!locationOptions.some((option) => option.key === form.location)) {
      localErrors.location = ['Select a valid location or Unknown.'];
    }

    if (Object.keys(localErrors).length > 0) {
      setFieldErrors(localErrors);
      return;
    }

    setFieldErrors({});
    setSaving(true);
    try {
      const payload = buildUpdateSitePayload(form);
      const updated = await sitesService.updateSite(site.domain, payload);
      onSaved(updated);
    } catch (err) {
      if (err instanceof ApiClientError && err.fieldErrors) {
        setFieldErrors(err.fieldErrors);
      } else {
        setFieldErrors({ _form: [err instanceof Error ? err.message : 'Update failed'] });
      }
    } finally {
      setSaving(false);
    }
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
  const canSave =
    Boolean(site) &&
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

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Edit site</DialogTitle>
      <DialogContent>
        {fieldErrors._form?.[0] && (
          <Box sx={{ mb: 2, color: 'error.main', fontSize: 14 }}>{fieldErrors._form[0]}</Box>
        )}

        {site && (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
            <TextField label="Domain" value={site.domain} disabled size="small" fullWidth />

            <TextField
              label="DR"
              type="number"
              inputProps={{ min: 0, max: 100 }}
              value={form.dr}
              onChange={(e) => updateField('dr', e.target.value)}
              size="small"
              fullWidth
              error={Boolean(fieldErrors.dr?.length)}
              helperText={fieldErrors.dr?.[0]}
            />

            <TextField
              label="Traffic"
              type="number"
              inputProps={{ min: 0, step: 1 }}
              value={form.traffic}
              onChange={(e) => updateField('traffic', e.target.value)}
              size="small"
              fullWidth
              error={Boolean(fieldErrors.traffic?.length)}
              helperText={fieldErrors.traffic?.[0]}
            />

            <Autocomplete
              size="small"
              options={editLocationOptions}
              value={currentLocationOption}
              loading={locationOptionsLoading}
              disabled={Boolean(locationOptionsError)}
              getOptionLabel={(option) => option.displayName}
              getOptionDisabled={(option) => option.key === OTHER_LOCATION_FORM_VALUE}
              isOptionEqualToValue={(option, value) => option.key === value.key}
              onChange={(_, option) =>
                updateField('location', option?.key ?? '')
              }
              renderInput={(params) => (
                <TextField
                  {...params}
                  label="Location"
                  fullWidth
                  color={locationNeedsReplacement ? 'warning' : 'primary'}
                  error={Boolean(fieldErrors.location?.length || locationOptionsError)}
                  helperText={locationHelperText}
                  sx={
                    locationNeedsReplacement
                      ? {
                          '& .MuiOutlinedInput-notchedOutline': {
                            borderColor: 'warning.main',
                          },
                          '&:hover .MuiOutlinedInput-notchedOutline': {
                            borderColor: 'warning.dark',
                          },
                        }
                      : undefined
                  }
                  InputProps={{
                    ...params.InputProps,
                    endAdornment: (
                      <>
                        {locationOptionsLoading ? (
                          <CircularProgress color="inherit" size={18} />
                        ) : null}
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
              onChange={(e) => updateField('language', e.target.value)}
              size="small"
              fullWidth
              error={Boolean(fieldErrors.language?.length)}
              helperText={fieldErrors.language?.[0] ?? 'Optional'}
            >
              <MenuItem value="">Empty</MenuItem>
              {languageOptions.map((option) => (
                <MenuItem key={option.value} value={option.value}>
                  {option.label}
                </MenuItem>
              ))}
            </TextField>

            <Paper variant="outlined" sx={{ p: 2, borderRadius: 1 }}>
              <Stack spacing={1.5}>
                <Typography variant="subtitle2">Pricing</Typography>
                {PRICING_SECTIONS.map((section) => {
                  const rows = getPriceRowsForType(form, section.priceType);
                  const status =
                    form.pricingStatuses[section.priceType] ??
                    SERVICE_AVAILABILITY_STATUS.Unknown;
                  const rowsVisible =
                    section.priceType === PRICE_TYPE.Main ||
                    status === SERVICE_AVAILABILITY_STATUS.Available;

                  return (
                    <Paper
                      key={section.priceType}
                      variant="outlined"
                      sx={{ p: 1.5, borderRadius: 1, bgcolor: 'background.paper' }}
                    >
                      <Stack spacing={1.25}>
                        <Box
                          sx={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            gap: 1,
                          }}
                        >
                          <Typography variant="body2" sx={{ fontWeight: 700 }}>
                            {section.label}
                          </Typography>
                        </Box>

                        {section.isOptional && (
                          <TextField
                            select
                            label="Status"
                            value={status}
                            onChange={(e) =>
                              handleServiceStatusChange(
                                section.priceType,
                                Number(e.target.value) as ServiceAvailabilityStatusValue
                              )
                            }
                            size="small"
                            sx={{ maxWidth: 260 }}
                            error={Boolean(fieldErrors[pricingStatusErrorKey(section.priceType)]?.length)}
                            helperText={fieldErrors[pricingStatusErrorKey(section.priceType)]?.[0]}
                          >
                            {SERVICE_AVAILABILITY_STATUS_OPTIONS.map((option) => (
                              <MenuItem key={option.value} value={option.value}>
                                {option.label}
                              </MenuItem>
                            ))}
                          </TextField>
                        )}

                        {rowsVisible && (
                          <>
                            {rows.length > 0 && (
                              <Stack divider={<Divider flexItem />} spacing={0}>
                                {rows.map((row) => (
                                  <Box
                                    key={row.id}
                                    sx={{
                                      display: 'grid',
                                      gridTemplateColumns: {
                                        xs: '1fr',
                                        sm: 'minmax(0, 1fr) minmax(140px, 180px) 36px',
                                      },
                                      gap: 1,
                                      py: 0.75,
                                      alignItems: 'flex-start',
                                    }}
                                  >
                                    <TextField
                                      select
                                      label="Term"
                                      value={row.termKey}
                                      onChange={(e) =>
                                        handlePriceRowChange(row.id, 'termKey', e.target.value)
                                      }
                                      size="small"
                                      error={Boolean(fieldErrors[pricingTermErrorKey(row.id)]?.length)}
                                      helperText={fieldErrors[pricingTermErrorKey(row.id)]?.[0]}
                                    >
                                      {getTermOptionsForRow(row).map((option) => (
                                        <MenuItem key={option.termKey} value={option.termKey}>
                                          {option.label}
                                        </MenuItem>
                                      ))}
                                    </TextField>
                                    <TextField
                                      label="Amount USD"
                                      type="number"
                                      inputProps={{ min: 1, step: '1' }}
                                      value={row.amountUsd}
                                      onChange={(e) =>
                                        handlePriceRowChange(row.id, 'amountUsd', e.target.value)
                                      }
                                      size="small"
                                      error={Boolean(fieldErrors[pricingAmountErrorKey(row.id)]?.length)}
                                      helperText={fieldErrors[pricingAmountErrorKey(row.id)]?.[0]}
                                    />
                                    <IconButton
                                      aria-label={`Delete ${section.label} price`}
                                      onClick={() => handleDeletePrice(row.id)}
                                      size="small"
                                      sx={{ mt: 0.25 }}
                                    >
                                      <DeleteOutlineIcon fontSize="small" />
                                    </IconButton>
                                  </Box>
                                ))}
                              </Stack>
                            )}

                            <Box>
                              <BrandButton
                                size="small"
                                startIcon={<AddIcon fontSize="small" />}
                                onClick={() => handleAddPrice(section.priceType)}
                              >
                                Add price
                              </BrandButton>
                            </Box>
                          </>
                        )}
                      </Stack>
                    </Paper>
                  );
                })}
              </Stack>
            </Paper>

            <TextField
              label="Niche"
              value={form.niche}
              onChange={(e) => updateField('niche', e.target.value)}
              size="small"
              fullWidth
              error={Boolean(fieldErrors.niche?.length)}
              helperText={fieldErrors.niche?.[0]}
            />

            <TextField
              label="Categories"
              value={form.categories}
              onChange={(e) => updateField('categories', e.target.value)}
              size="small"
              fullWidth
              multiline
              minRows={2}
              error={Boolean(fieldErrors.categories?.length)}
              helperText={fieldErrors.categories?.[0]}
            />

            <TextField
              label="Number DF Links"
              type="number"
              inputProps={{ min: 1, step: 1 }}
              value={form.numberDFLinks}
              onChange={(e) => updateField('numberDFLinks', e.target.value)}
              size="small"
              fullWidth
              error={Boolean(fieldErrors.numberDFLinks?.length)}
              helperText={fieldErrors.numberDFLinks?.[0] ?? 'Optional positive whole number'}
            />

            <TextField
              label="Sponsored Tag"
              value={form.sponsoredTag}
              onChange={(e) => updateField('sponsoredTag', e.target.value)}
              size="small"
              fullWidth
              error={Boolean(fieldErrors.sponsoredTag?.length)}
              helperText={fieldErrors.sponsoredTag?.[0]}
            />

            <FormControlLabel
              control={
                <Switch
                  checked={form.isQuarantined}
                  onChange={(e) => updateField('isQuarantined', e.target.checked)}
                />
              }
              label="Unavailable (quarantined)"
            />

            {form.isQuarantined && (
              <TextField
                label="Reason (optional)"
                value={form.quarantineReason}
                onChange={(e) => updateField('quarantineReason', e.target.value)}
                size="small"
                fullWidth
                multiline
                minRows={2}
                error={Boolean(fieldErrors.quarantineReason?.length)}
                helperText={fieldErrors.quarantineReason?.[0]}
              />
            )}
          </Box>
        )}
      </DialogContent>
      <DialogActions>
        <BrandButton onClick={onClose} disabled={saving}>
          Cancel
        </BrandButton>
        <BrandButton kind="primary" onClick={handleSave} disabled={!canSave}>
          Save
        </BrandButton>
      </DialogActions>
    </Dialog>
  );
}
