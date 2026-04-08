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

import type { ServiceAvailabilityStatusValue, Site, UpdateSitePayload } from '../../types/sites.types';
import { sitesService } from '../../services/sites.service';
import { ApiClientError } from '../../services/api.client';
import { BrandButton } from '../common/BrandButton';
import {
  normalizeServiceAvailabilityStatus,
  SERVICE_AVAILABILITY_STATUS,
  SERVICE_AVAILABILITY_STATUS_OPTIONS,
} from '../../utils/serviceAvailability';

// --- Types ---

type EditSiteFormState = {
  dr: string;
  traffic: string;
  location: string;
  priceUsd: string;
  priceCasino: string;
  priceCasinoStatus: ServiceAvailabilityStatusValue;
  priceCrypto: string;
  priceCryptoStatus: ServiceAvailabilityStatusValue;
  priceLinkInsert: string;
  priceLinkInsertStatus: ServiceAvailabilityStatusValue;
  niche: string;
  categories: string;
  linkType: string;
  sponsoredTag: string;
  isQuarantined: boolean;
  quarantineReason: string;
};

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

// --- Module-level helpers ---

function parseNumberOrNull(input: string): number | null {
  const t = input.trim();
  if (t === '') return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

function getServiceStateHint(status: ServiceAvailabilityStatusValue): string {
  if (status === SERVICE_AVAILABILITY_STATUS.NotAvailable) return 'Will be shown as NO';
  if (status === SERVICE_AVAILABILITY_STATUS.Unknown) return 'Will be shown as —';
  return 'Enter a non-negative price';
}

function clearFieldError(
  errors: Record<string, string[]>,
  key: string
): Record<string, string[]> {
  if (errors[key]) {
    const next = { ...errors };
    delete next[key];
    return next;
  }
  return errors;
}

const EMPTY_FORM_STATE: EditSiteFormState = {
  dr: '',
  traffic: '',
  location: '',
  priceUsd: '',
  priceCasino: '',
  priceCasinoStatus: SERVICE_AVAILABILITY_STATUS.Unknown,
  priceCrypto: '',
  priceCryptoStatus: SERVICE_AVAILABILITY_STATUS.Unknown,
  priceLinkInsert: '',
  priceLinkInsertStatus: SERVICE_AVAILABILITY_STATUS.Unknown,
  niche: '',
  categories: '',
  linkType: '',
  sponsoredTag: '',
  isQuarantined: false,
  quarantineReason: '',
};

function createInitialFormState(site: Site): EditSiteFormState {
  const casinoStatus = normalizeServiceAvailabilityStatus(site.priceCasinoStatus);
  const cryptoStatus = normalizeServiceAvailabilityStatus(site.priceCryptoStatus);
  const linkInsertStatus = normalizeServiceAvailabilityStatus(site.priceLinkInsertStatus);
  return {
    dr: String(site.dr ?? ''),
    traffic: String(site.traffic ?? ''),
    location: site.location ?? '',
    priceUsd: site.priceUsd == null ? '' : String(site.priceUsd),
    priceCasinoStatus: casinoStatus,
    priceCasino:
      casinoStatus === SERVICE_AVAILABILITY_STATUS.Available && site.priceCasino != null
        ? String(site.priceCasino)
        : '',
    priceCryptoStatus: cryptoStatus,
    priceCrypto:
      cryptoStatus === SERVICE_AVAILABILITY_STATUS.Available && site.priceCrypto != null
        ? String(site.priceCrypto)
        : '',
    priceLinkInsertStatus: linkInsertStatus,
    priceLinkInsert:
      linkInsertStatus === SERVICE_AVAILABILITY_STATUS.Available && site.priceLinkInsert != null
        ? String(site.priceLinkInsert)
        : '',
    niche: site.niche ?? '',
    categories: site.categories ?? '',
    linkType: site.linkType ?? '',
    sponsoredTag: site.sponsoredTag ?? '',
    isQuarantined: Boolean(site.isQuarantined),
    quarantineReason: site.quarantineReason ?? '',
  };
}

function validateOptionalServicePrice(
  price: string,
  fieldKey: string,
  errors: Record<string, string[]>
): void {
  const parsed = parseNumberOrNull(price);
  if (parsed === null) {
    errors[fieldKey] = ['Required when status is Available.'];
  } else if (parsed < 0) {
    errors[fieldKey] = ['Must be 0 or greater.'];
  }
}

function validateEditSiteForm(form: EditSiteFormState): Record<string, string[]> {
  const errors: Record<string, string[]> = {};

  const parsedDr = parseNumberOrNull(form.dr);
  if (parsedDr === null || parsedDr < 0 || parsedDr > 100) {
    errors.dr = ['DR must be between 0 and 100.'];
  }

  const parsedTraffic = parseNumberOrNull(form.traffic);
  if (parsedTraffic === null || parsedTraffic < 0) {
    errors.traffic = ['Traffic must be 0 or greater.'];
  } else if (!Number.isInteger(parsedTraffic)) {
    errors.traffic = ['Traffic must be a whole number.'];
  }

  if (!form.location.trim()) {
    errors.location = ['Location is required.'];
  }

  const parsedPriceUsd = parseNumberOrNull(form.priceUsd);
  if (parsedPriceUsd !== null && parsedPriceUsd < 0) {
    errors.priceUsd = ['Price USD must be 0 or greater.'];
  }

  if (form.priceCasinoStatus === SERVICE_AVAILABILITY_STATUS.Available) {
    validateOptionalServicePrice(form.priceCasino, 'priceCasino', errors);
  }
  if (form.priceCryptoStatus === SERVICE_AVAILABILITY_STATUS.Available) {
    validateOptionalServicePrice(form.priceCrypto, 'priceCrypto', errors);
  }
  if (form.priceLinkInsertStatus === SERVICE_AVAILABILITY_STATUS.Available) {
    validateOptionalServicePrice(form.priceLinkInsert, 'priceLinkInsert', errors);
  }

  const casinoNumericPrice =
    form.priceCasinoStatus === SERVICE_AVAILABILITY_STATUS.Available
      ? parseNumberOrNull(form.priceCasino)
      : null;
  const cryptoNumericPrice =
    form.priceCryptoStatus === SERVICE_AVAILABILITY_STATUS.Available
      ? parseNumberOrNull(form.priceCrypto)
      : null;
  const linkInsertNumericPrice =
    form.priceLinkInsertStatus === SERVICE_AVAILABILITY_STATUS.Available
      ? parseNumberOrNull(form.priceLinkInsert)
      : null;

  if (
    parsedPriceUsd === null &&
    casinoNumericPrice === null &&
    cryptoNumericPrice === null &&
    linkInsertNumericPrice === null
  ) {
    const errorText = 'At least one numeric price is required (Price USD, Casino, Crypto, or Link Insert).';
    errors.priceUsd = [errorText];
    errors.priceCasino = [errorText];
    errors.priceCrypto = [errorText];
    errors.priceLinkInsert = [errorText];
  }

  return errors;
}

function buildUpdateSitePayload(form: EditSiteFormState): UpdateSitePayload {
  return {
    dr: parseNumberOrNull(form.dr)!,
    traffic: parseNumberOrNull(form.traffic)!,
    location: form.location.trim(),
    priceUsd: parseNumberOrNull(form.priceUsd),
    priceCasino:
      form.priceCasinoStatus === SERVICE_AVAILABILITY_STATUS.Available
        ? (parseNumberOrNull(form.priceCasino) ?? null)
        : null,
    priceCasinoStatus: form.priceCasinoStatus,
    priceCrypto:
      form.priceCryptoStatus === SERVICE_AVAILABILITY_STATUS.Available
        ? (parseNumberOrNull(form.priceCrypto) ?? null)
        : null,
    priceCryptoStatus: form.priceCryptoStatus,
    priceLinkInsert:
      form.priceLinkInsertStatus === SERVICE_AVAILABILITY_STATUS.Available
        ? (parseNumberOrNull(form.priceLinkInsert) ?? null)
        : null,
    priceLinkInsertStatus: form.priceLinkInsertStatus,
    niche: form.niche.trim() || null,
    categories: form.categories.trim() || null,
    LinkType: form.linkType.trim() || null,
    SponsoredTag: form.sponsoredTag.trim() || null,
    isQuarantined: form.isQuarantined,
    quarantineReason: form.isQuarantined ? (form.quarantineReason.trim() || null) : null,
  };
}

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

  const handleCasinoStatusChange = (s: ServiceAvailabilityStatusValue) => {
    setForm((prev) => ({
      ...prev,
      priceCasinoStatus: s,
      priceCasino: s === SERVICE_AVAILABILITY_STATUS.Available ? prev.priceCasino : '',
    }));
    setFieldErrors((prev) => clearFieldError(clearFieldError(prev, 'priceCasinoStatus'), 'priceCasino'));
  };

  const handleCryptoStatusChange = (s: ServiceAvailabilityStatusValue) => {
    setForm((prev) => ({
      ...prev,
      priceCryptoStatus: s,
      priceCrypto: s === SERVICE_AVAILABILITY_STATUS.Available ? prev.priceCrypto : '',
    }));
    setFieldErrors((prev) => clearFieldError(clearFieldError(prev, 'priceCryptoStatus'), 'priceCrypto'));
  };

  const handleLinkInsertStatusChange = (s: ServiceAvailabilityStatusValue) => {
    setForm((prev) => ({
      ...prev,
      priceLinkInsertStatus: s,
      priceLinkInsert: s === SERVICE_AVAILABILITY_STATUS.Available ? prev.priceLinkInsert : '',
    }));
    setFieldErrors((prev) =>
      clearFieldError(clearFieldError(prev, 'priceLinkInsertStatus'), 'priceLinkInsert')
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
