import { useEffect, useMemo, useState } from 'react';
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

type Props = {
  open: boolean;
  site: Site | null;
  onClose: () => void;
  onSaved: (updated: Site) => void;
};

function parseNumberOrNull(input: string): number | null {
  const t = input.trim();
  if (t === '') return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

function getServiceStateHint(status: ServiceAvailabilityStatusValue): string {
  if (status === SERVICE_AVAILABILITY_STATUS.NotAvailable) {
    return 'Will be shown as NO.';
  }
  if (status === SERVICE_AVAILABILITY_STATUS.Unknown) {
    return 'Will be shown as —.';
  }
  return 'Enter a non-negative price.';
}

export function EditSiteDialog({ open, site, onClose, onSaved }: Props) {
  const [dr, setDr] = useState('');
  const [traffic, setTraffic] = useState('');
  const [location, setLocation] = useState('');
  const [priceUsd, setPriceUsd] = useState('');
  const [priceCasino, setPriceCasino] = useState('');
  const [priceCasinoStatus, setPriceCasinoStatus] = useState<ServiceAvailabilityStatusValue>(
    SERVICE_AVAILABILITY_STATUS.Unknown
  );
  const [priceCrypto, setPriceCrypto] = useState('');
  const [priceCryptoStatus, setPriceCryptoStatus] = useState<ServiceAvailabilityStatusValue>(
    SERVICE_AVAILABILITY_STATUS.Unknown
  );
  const [priceLinkInsert, setPriceLinkInsert] = useState('');
  const [priceLinkInsertStatus, setPriceLinkInsertStatus] = useState<ServiceAvailabilityStatusValue>(
    SERVICE_AVAILABILITY_STATUS.Unknown
  );
  const [niche, setNiche] = useState('');
  const [categories, setCategories] = useState('');
  const [isQuarantined, setIsQuarantined] = useState(false);
  const [reason, setReason] = useState('');

  const [saving, setSaving] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});

  // Reset form when a different site is opened
  useEffect(() => {
    if (!site) return;
    setDr(String(site.dr ?? ''));
    setTraffic(String(site.traffic ?? ''));
    setLocation(site.location ?? '');
    setPriceUsd(site.priceUsd != null ? String(site.priceUsd) : '');
    const casinoStatus = normalizeServiceAvailabilityStatus(site.priceCasinoStatus);
    const cryptoStatus = normalizeServiceAvailabilityStatus(site.priceCryptoStatus);
    const linkInsertStatus = normalizeServiceAvailabilityStatus(site.priceLinkInsertStatus);
    setPriceCasinoStatus(casinoStatus);
    setPriceCryptoStatus(cryptoStatus);
    setPriceLinkInsertStatus(linkInsertStatus);
    setPriceCasino(
      casinoStatus === SERVICE_AVAILABILITY_STATUS.Available && site.priceCasino != null
        ? String(site.priceCasino)
        : ''
    );
    setPriceCrypto(
      cryptoStatus === SERVICE_AVAILABILITY_STATUS.Available && site.priceCrypto != null
        ? String(site.priceCrypto)
        : ''
    );
    setPriceLinkInsert(
      linkInsertStatus === SERVICE_AVAILABILITY_STATUS.Available && site.priceLinkInsert != null
        ? String(site.priceLinkInsert)
        : ''
    );
    setNiche(site.niche ?? '');
    setCategories(site.categories ?? '');
    setIsQuarantined(Boolean(site.isQuarantined));
    setReason(site.quarantineReason ?? '');
    setFieldErrors({});
    setSaving(false);
  }, [site]);

  const canSave = useMemo(() => Boolean(site) && !saving, [site, saving]);
  const isCasinoAvailable = priceCasinoStatus === SERVICE_AVAILABILITY_STATUS.Available;
  const isCryptoAvailable = priceCryptoStatus === SERVICE_AVAILABILITY_STATUS.Available;
  const isLinkInsertAvailable = priceLinkInsertStatus === SERVICE_AVAILABILITY_STATUS.Available;

  const handleSave = async () => {
    if (!site) return;

    setFieldErrors({});

    const parsedDr = parseNumberOrNull(dr);
    if (parsedDr === null || parsedDr < 0 || parsedDr > 100) {
      setFieldErrors({ dr: ['DR must be between 0 and 100.'] });
      return;
    }

    const parsedTraffic = parseNumberOrNull(traffic);
    if (parsedTraffic === null || parsedTraffic < 0) {
      setFieldErrors({ traffic: ['Traffic must be 0 or greater.'] });
      return;
    }

    const trimmedLocation = location.trim();
    if (!trimmedLocation) {
      setFieldErrors({ location: ['Location is required.'] });
      return;
    }

    const parsedPriceUsd = parseNumberOrNull(priceUsd);
    if (parsedPriceUsd === null || parsedPriceUsd < 0) {
      setFieldErrors({ priceUsd: ['Price USD is required and must be 0 or greater.'] });
      return;
    }

    const parsedPriceCasino = parseNumberOrNull(priceCasino);
    const parsedPriceCrypto = parseNumberOrNull(priceCrypto);
    const parsedPriceLinkInsert = parseNumberOrNull(priceLinkInsert);

    const priceErrors: Record<string, string[]> = {};
    if (priceCasinoStatus === SERVICE_AVAILABILITY_STATUS.Available) {
      if (parsedPriceCasino == null) {
        priceErrors.priceCasino = ['Required when status is Available.'];
      } else if (parsedPriceCasino < 0) {
        priceErrors.priceCasino = ['Must be 0 or greater.'];
      }
    }
    if (priceCryptoStatus === SERVICE_AVAILABILITY_STATUS.Available) {
      if (parsedPriceCrypto == null) {
        priceErrors.priceCrypto = ['Required when status is Available.'];
      } else if (parsedPriceCrypto < 0) {
        priceErrors.priceCrypto = ['Must be 0 or greater.'];
      }
    }
    if (priceLinkInsertStatus === SERVICE_AVAILABILITY_STATUS.Available) {
      if (parsedPriceLinkInsert == null) {
        priceErrors.priceLinkInsert = ['Required when status is Available.'];
      } else if (parsedPriceLinkInsert < 0) {
        priceErrors.priceLinkInsert = ['Must be 0 or greater.'];
      }
    }
    if (Object.keys(priceErrors).length > 0) {
      setFieldErrors(priceErrors);
      return;
    }

    const payload: UpdateSitePayload = {
      dr: parsedDr,
      traffic: Math.floor(parsedTraffic),
      location: trimmedLocation,
      priceUsd: parsedPriceUsd,
      priceCasino: priceCasinoStatus === SERVICE_AVAILABILITY_STATUS.Available ? (parsedPriceCasino ?? null) : null,
      priceCasinoStatus,
      priceCrypto: priceCryptoStatus === SERVICE_AVAILABILITY_STATUS.Available ? (parsedPriceCrypto ?? null) : null,
      priceCryptoStatus,
      priceLinkInsert:
        priceLinkInsertStatus === SERVICE_AVAILABILITY_STATUS.Available ? (parsedPriceLinkInsert ?? null) : null,
      priceLinkInsertStatus,
      niche: niche.trim() || null,
      categories: categories.trim() || null,
      isQuarantined,
      quarantineReason: isQuarantined ? (reason.trim() || null) : null,
    };

    setSaving(true);
    try {
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
              value={dr}
              onChange={(e) => setDr(e.target.value)}
              size="small"
              fullWidth
              error={Boolean(fieldErrors.dr?.length)}
              helperText={fieldErrors.dr?.[0]}
            />

            <TextField
              label="Traffic"
              type="number"
              inputProps={{ min: 0 }}
              value={traffic}
              onChange={(e) => setTraffic(e.target.value)}
              size="small"
              fullWidth
              error={Boolean(fieldErrors.traffic?.length)}
              helperText={fieldErrors.traffic?.[0]}
            />

            <TextField
              label="Location"
              value={location}
              onChange={(e) => setLocation(e.target.value)}
              size="small"
              fullWidth
              error={Boolean(fieldErrors.location?.length)}
              helperText={fieldErrors.location?.[0]}
            />

            <TextField
              label="Price USD"
              type="number"
              inputProps={{ min: 0, step: '0.01' }}
              value={priceUsd}
              onChange={(e) => setPriceUsd(e.target.value)}
              size="small"
              fullWidth
              error={Boolean(fieldErrors.priceUsd?.length)}
              helperText={fieldErrors.priceUsd?.[0]}
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

              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>Casino</Typography>
                <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                  <TextField
                    select
                    label="Casino availability"
                    value={priceCasinoStatus}
                    onChange={(e) => {
                      const status = Number(e.target.value) as ServiceAvailabilityStatusValue;
                      setPriceCasinoStatus(status);
                      if (status !== SERVICE_AVAILABILITY_STATUS.Available) {
                        setPriceCasino('');
                      }
                    }}
                    size="small"
                    sx={{ minWidth: 220, flex: '1 1 220px' }}
                    error={Boolean(fieldErrors.priceCasinoStatus?.length)}
                    helperText={fieldErrors.priceCasinoStatus?.[0]}
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
                    inputProps={{ min: 0, step: '0.01' }}
                    value={priceCasino}
                    onChange={(e) => setPriceCasino(e.target.value)}
                    size="small"
                    placeholder={isCasinoAvailable ? 'Enter price' : ''}
                    sx={{ minWidth: 220, flex: '1 1 220px' }}
                    disabled={!isCasinoAvailable}
                    error={Boolean(fieldErrors.priceCasino?.length)}
                    helperText={fieldErrors.priceCasino?.[0] || getServiceStateHint(priceCasinoStatus)}
                  />
                </Box>
              </Box>

              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>Crypto</Typography>
                <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                  <TextField
                    select
                    label="Crypto availability"
                    value={priceCryptoStatus}
                    onChange={(e) => {
                      const status = Number(e.target.value) as ServiceAvailabilityStatusValue;
                      setPriceCryptoStatus(status);
                      if (status !== SERVICE_AVAILABILITY_STATUS.Available) {
                        setPriceCrypto('');
                      }
                    }}
                    size="small"
                    sx={{ minWidth: 220, flex: '1 1 220px' }}
                    error={Boolean(fieldErrors.priceCryptoStatus?.length)}
                    helperText={fieldErrors.priceCryptoStatus?.[0]}
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
                    inputProps={{ min: 0, step: '0.01' }}
                    value={priceCrypto}
                    onChange={(e) => setPriceCrypto(e.target.value)}
                    size="small"
                    placeholder={isCryptoAvailable ? 'Enter price' : ''}
                    sx={{ minWidth: 220, flex: '1 1 220px' }}
                    disabled={!isCryptoAvailable}
                    error={Boolean(fieldErrors.priceCrypto?.length)}
                    helperText={fieldErrors.priceCrypto?.[0] || getServiceStateHint(priceCryptoStatus)}
                  />
                </Box>
              </Box>

              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>Link Insert</Typography>
                <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                  <TextField
                    select
                    label="Link Insert availability"
                    value={priceLinkInsertStatus}
                    onChange={(e) => {
                      const status = Number(e.target.value) as ServiceAvailabilityStatusValue;
                      setPriceLinkInsertStatus(status);
                      if (status !== SERVICE_AVAILABILITY_STATUS.Available) {
                        setPriceLinkInsert('');
                      }
                    }}
                    size="small"
                    sx={{ minWidth: 220, flex: '1 1 220px' }}
                    error={Boolean(fieldErrors.priceLinkInsertStatus?.length)}
                    helperText={fieldErrors.priceLinkInsertStatus?.[0]}
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
                    inputProps={{ min: 0, step: '0.01' }}
                    value={priceLinkInsert}
                    onChange={(e) => setPriceLinkInsert(e.target.value)}
                    size="small"
                    placeholder={isLinkInsertAvailable ? 'Enter price' : ''}
                    sx={{ minWidth: 220, flex: '1 1 220px' }}
                    disabled={!isLinkInsertAvailable}
                    error={Boolean(fieldErrors.priceLinkInsert?.length)}
                    helperText={fieldErrors.priceLinkInsert?.[0] || getServiceStateHint(priceLinkInsertStatus)}
                  />
                </Box>
              </Box>
            </Box>

            <TextField
              label="Niche"
              value={niche}
              onChange={(e) => setNiche(e.target.value)}
              size="small"
              fullWidth
            />

            <TextField
              label="Categories"
              value={categories}
              onChange={(e) => setCategories(e.target.value)}
              size="small"
              fullWidth
              multiline
              minRows={2}
            />

            <FormControlLabel
              control={
                <Switch
                  checked={isQuarantined}
                  onChange={(e) => setIsQuarantined(e.target.checked)}
                />
              }
              label="Unavailable (quarantined)"
            />

            {isQuarantined && (
              <TextField
                label="Reason (optional)"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
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
