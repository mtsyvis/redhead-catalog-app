import { useEffect, useState } from 'react';
import {
  Box,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  MenuItem,
  Switch,
  TextField,
  Typography,
} from '@mui/material';

import type {
  ServiceAvailabilityStatusValue,
  Site,
  TermTypeValue,
} from '../../types/sites.types';
import { sitesService } from '../../services/sites.service';
import { ApiClientError } from '../../services/api.client';
import { BrandButton } from '../common/BrandButton';
import {
  SERVICE_AVAILABILITY_STATUS,
  SERVICE_AVAILABILITY_STATUS_OPTIONS,
} from '../../utils/serviceAvailability';
import { TERM_TYPE } from '../../utils/term';
import {
  buildUpdateSitePayload,
  clearFieldError,
  createInitialFormState,
  EMPTY_FORM_STATE,
  getServiceStateHint,
  validateEditSiteForm,
} from './EditSiteDialog.helpers';
import type {
  EditSiteFormState,
  OptionalServicePriceField,
  OptionalServiceStatusField,
} from './EditSiteDialog.helpers';

// --- Types ---

type Props = {
  open: boolean;
  site: Site | null;
  onClose: () => void;
  onSaved: (updated: Site) => void;
};

type OptionalServiceSectionProps = {
  label: string;
  status: ServiceAvailabilityStatusValue;
  price: string;
  statusError?: string;
  priceError?: string;
  onStatusChange: (status: ServiceAvailabilityStatusValue) => void;
  onPriceChange: (price: string) => void;
};

// --- OptionalServiceSection sub-component ---

function OptionalServiceSection({
  label,
  status,
  price,
  statusError,
  priceError,
  onStatusChange,
  onPriceChange,
}: Readonly<OptionalServiceSectionProps>) {
  const isAvailable = status === SERVICE_AVAILABILITY_STATUS.Available;
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Typography variant="body2" sx={{ fontWeight: 600 }}>{label}</Typography>
      <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
        <TextField
          select
          label={`${label} availability`}
          value={status}
          onChange={(e) => onStatusChange(Number(e.target.value) as ServiceAvailabilityStatusValue)}
          size="small"
          sx={{ minWidth: 220, flex: '1 1 220px' }}
          error={Boolean(statusError)}
          helperText={statusError}
        >
          {SERVICE_AVAILABILITY_STATUS_OPTIONS.map((option) => (
            <MenuItem key={option.value} value={option.value}>
              {option.label}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          label="Price"
          type="number"
          value={price}
          onChange={(e) => onPriceChange(e.target.value)}
          size="small"
          placeholder={isAvailable ? 'Enter price' : ''}
          sx={{ minWidth: 220, flex: '1 1 220px' }}
          disabled={!isAvailable}
          error={Boolean(priceError)}
          helperText={priceError || getServiceStateHint(status)}
        />
      </Box>
    </Box>
  );
}

// --- Main component ---

export function EditSiteDialog({ open, site, onClose, onSaved }: Readonly<Props>) {
  const [form, setForm] = useState<EditSiteFormState>(
    site ? createInitialFormState(site) : EMPTY_FORM_STATE
  );
  const [saving, setSaving] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (!open || !site) return;
    setForm(createInitialFormState(site));
    setFieldErrors({});
    setSaving(false);
  }, [open, site]);

  const updateField = <K extends keyof EditSiteFormState>(
    key: K,
    value: EditSiteFormState[K]
  ) => {
    setForm((prev) => ({ ...prev, [key]: value }));
    setFieldErrors((prev) => clearFieldError(prev, key));
  };

  const handleOptionalServiceStatusChange = (
    statusKey: OptionalServiceStatusField,
    priceKey: OptionalServicePriceField,
    s: ServiceAvailabilityStatusValue
  ) => {
    setForm((prev) => ({
      ...prev,
      [statusKey]: s,
      [priceKey]: s === SERVICE_AVAILABILITY_STATUS.Available ? prev[priceKey] : '',
    }));
    setFieldErrors((prev) => clearFieldError(clearFieldError(prev, statusKey), priceKey));
  };

  const handleCasinoStatusChange = (s: ServiceAvailabilityStatusValue) =>
    handleOptionalServiceStatusChange('priceCasinoStatus', 'priceCasino', s);

  const handleCryptoStatusChange = (s: ServiceAvailabilityStatusValue) =>
    handleOptionalServiceStatusChange('priceCryptoStatus', 'priceCrypto', s);

  const handleLinkInsertStatusChange = (s: ServiceAvailabilityStatusValue) =>
    handleOptionalServiceStatusChange('priceLinkInsertStatus', 'priceLinkInsert', s);

  const handleLinkInsertCasinoStatusChange = (s: ServiceAvailabilityStatusValue) =>
    handleOptionalServiceStatusChange('priceLinkInsertCasinoStatus', 'priceLinkInsertCasino', s);

  const handleDatingStatusChange = (s: ServiceAvailabilityStatusValue) =>
    handleOptionalServiceStatusChange('priceDatingStatus', 'priceDating', s);

  const handleTermTypeChange = (value: string) => {
    const nextTermType = value === '' ? '' : (Number(value) as TermTypeValue);
    setForm((prev) => ({
      ...prev,
      termType: nextTermType,
      termValue: nextTermType === TERM_TYPE.Finite ? prev.termValue : '',
    }));
    setFieldErrors((prev) =>
      clearFieldError(
        clearFieldError(clearFieldError(prev, 'termType'), 'termValue'),
        'termUnit'
      )
    );
  };

  const handleSave = async () => {
    if (!site) return;

    const localErrors = validateEditSiteForm(form);
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

  const canSave = Boolean(site) && !saving;

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

            <TextField
              label="Location"
              value={form.location}
              onChange={(e) => updateField('location', e.target.value)}
              size="small"
              fullWidth
              error={Boolean(fieldErrors.location?.length)}
              helperText={fieldErrors.location?.[0]}
            />

            <TextField
              label="Price USD"
              type="number"
              inputProps={{ min: 0, step: '1' }}
              value={form.priceUsd}
              onChange={(e) => updateField('priceUsd', e.target.value)}
              size="small"
              fullWidth
              error={Boolean(fieldErrors.priceUsd?.length)}
              helperText={fieldErrors.priceUsd?.[0] ?? 'Optional – leave empty if no USD price'}
            />

            <Box
              sx={{
                p: 2,
                border: '1px solid',
                borderColor: 'divider',
                borderRadius: 2,
                backgroundColor: 'background.paper',
                display: 'flex',
                flexDirection: 'column',
                gap: 1.5,
              }}
            >
              <Typography variant="subtitle2">Optional Services</Typography>
              <Typography variant="caption" color="text.secondary">
                Available = show price, Not available = NO, Unknown = —.
              </Typography>

              <OptionalServiceSection
                label="Casino"
                status={form.priceCasinoStatus}
                price={form.priceCasino}
                statusError={fieldErrors.priceCasinoStatus?.[0]}
                priceError={fieldErrors.priceCasino?.[0]}
                onStatusChange={handleCasinoStatusChange}
                onPriceChange={(p) => updateField('priceCasino', p)}
              />

              <OptionalServiceSection
                label="Crypto"
                status={form.priceCryptoStatus}
                price={form.priceCrypto}
                statusError={fieldErrors.priceCryptoStatus?.[0]}
                priceError={fieldErrors.priceCrypto?.[0]}
                onStatusChange={handleCryptoStatusChange}
                onPriceChange={(p) => updateField('priceCrypto', p)}
              />

              <OptionalServiceSection
                label="Link Insert"
                status={form.priceLinkInsertStatus}
                price={form.priceLinkInsert}
                statusError={fieldErrors.priceLinkInsertStatus?.[0]}
                priceError={fieldErrors.priceLinkInsert?.[0]}
                onStatusChange={handleLinkInsertStatusChange}
                onPriceChange={(p) => updateField('priceLinkInsert', p)}
              />

              <OptionalServiceSection
                label="Link Insert Casino"
                status={form.priceLinkInsertCasinoStatus}
                price={form.priceLinkInsertCasino}
                statusError={fieldErrors.priceLinkInsertCasinoStatus?.[0]}
                priceError={fieldErrors.priceLinkInsertCasino?.[0]}
                onStatusChange={handleLinkInsertCasinoStatusChange}
                onPriceChange={(p) => updateField('priceLinkInsertCasino', p)}
              />

              <OptionalServiceSection
                label="Dating"
                status={form.priceDatingStatus}
                price={form.priceDating}
                statusError={fieldErrors.priceDatingStatus?.[0]}
                priceError={fieldErrors.priceDating?.[0]}
                onStatusChange={handleDatingStatusChange}
                onPriceChange={(p) => updateField('priceDating', p)}
              />
            </Box>

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
              label="Link Type"
              value={form.linkType}
              onChange={(e) => updateField('linkType', e.target.value)}
              size="small"
              fullWidth
              error={Boolean(fieldErrors.linkType?.length)}
              helperText={fieldErrors.linkType?.[0]}
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

            <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
              <TextField
                select
                label="Term"
                value={form.termType}
                onChange={(e) => handleTermTypeChange(e.target.value)}
                size="small"
                sx={{ minWidth: 220, flex: '1 1 220px' }}
                error={Boolean(fieldErrors.termType?.length)}
                helperText={fieldErrors.termType?.[0]}
              >
                <MenuItem value="">Empty</MenuItem>
                <MenuItem value={TERM_TYPE.Permanent}>Permanent</MenuItem>
                <MenuItem value={TERM_TYPE.Finite}>Finite</MenuItem>
              </TextField>

              {form.termType === TERM_TYPE.Finite && (
                <>
                  <TextField
                    label="Term value"
                    type="number"
                    inputProps={{ min: 1, step: 1 }}
                    value={form.termValue}
                    onChange={(e) => updateField('termValue', e.target.value)}
                    size="small"
                    sx={{ minWidth: 160, flex: '1 1 160px' }}
                    error={Boolean(fieldErrors.termValue?.length)}
                    helperText={fieldErrors.termValue?.[0] ?? 'Positive whole number'}
                  />
                  <TextField
                    label="Unit"
                    value="Year"
                    size="small"
                    sx={{ minWidth: 120, flex: '0 1 120px' }}
                    disabled
                    error={Boolean(fieldErrors.termUnit?.length)}
                    helperText={fieldErrors.termUnit?.[0]}
                  />
                </>
              )}
            </Box>

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
