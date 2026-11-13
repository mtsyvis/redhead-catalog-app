import { DEFAULT_CURRENCY } from '../../constants/currency.constants';
import { useEffect, useMemo, useState, type SyntheticEvent } from 'react';
import {
  Alert,
  Box,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Tab,
  Tabs,
  Typography,
} from '@mui/material';
import { useChangeHistory } from '../../hooks/useChangeHistory';
import { ApiClientError } from '../../services/api.client';
import { webmasterOffersService } from '../../services/webmasterOffers.service';
import type { WebmasterOfferEdit } from '../../types/webmasterOffers.types';
import { BrandButton } from '../common/BrandButton';
import { ChangeHistoryContent } from '../common/ChangeHistoryContent';
import {
  buildWebmasterOfferPayload,
  createWebmasterOfferForm,
  validateWebmasterOfferForm,
  webmasterOfferFormSignature,
  type OfferFormState,
  type PriceFormRow,
} from './editWebmasterOfferForm';
import { WebmasterOfferDetailsTab } from './WebmasterOfferDetailsTab';
import { WebmasterOfferPricesTab } from './WebmasterOfferPricesTab';
import { useOfferCurrencyPreviews } from './useOfferCurrencyPreviews';

interface Props {
  readonly open: boolean;
  readonly offerId: string | null;
  readonly domain: string;
  readonly onClose: () => void;
  readonly onSaved: () => void;
}

function formatDateTime(value: string) {
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

export function EditWebmasterOfferDialog({ open, offerId, domain, onClose, onSaved }: Props) {
  const [tab, setTab] = useState(0);
  const [edit, setEdit] = useState<WebmasterOfferEdit | null>(null);
  const [form, setForm] = useState<OfferFormState | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [currencies, setCurrencies] = useState<string[]>([DEFAULT_CURRENCY, 'EUR', 'GBP']);
  const previews = useOfferCurrencyPreviews(open ? edit : null, open ? form : null);
  const history = useChangeHistory(open ? offerId : null, async () => {
    if (!offerId) return [];
    return webmasterOffersService.getHistory(offerId);
  });

  useEffect(() => {
    if (!open || !offerId) return;

    let active = true;
    setLoading(true);
    setEdit(null);
    setForm(null);
    setCurrencies([DEFAULT_CURRENCY, 'EUR', 'GBP']);
    setError(null);
    setFieldErrors({});
    setTab(0);
    webmasterOffersService.getCurrencies().then((codes) => { if (active) setCurrencies(codes); })
      .catch(() => { /* USD/EUR/GBP remain usable if the currency list request fails. */ });
    webmasterOffersService.getForEdit(offerId)
      .then((result) => {
        if (!active) return;
        setEdit(result);
        setForm(createWebmasterOfferForm(result));
      })
      .catch((err) => {
        if (active) setError(err instanceof Error ? err.message : 'Failed to load offer');
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [open, offerId]);

  const selectedMailboxes = useMemo(() => {
    if (!edit || !form) return [];
    const selected = new Set(form.mailboxIds);
    return edit.availableMailboxes.filter((mailbox) => selected.has(mailbox.id));
  }, [edit, form]);

  const initialFormSignature = useMemo(
    () => edit ? webmasterOfferFormSignature(createWebmasterOfferForm(edit)) : null,
    [edit]
  );
  const isDirty = useMemo(
    () => Boolean(form && initialFormSignature !== webmasterOfferFormSignature(form)),
    [form, initialFormSignature]
  );

  const updateField = <K extends keyof OfferFormState>(field: K, value: OfferFormState[K]) => {
    setForm((current) => current ? { ...current, [field]: value } : current);
    setFieldErrors((current) => {
      const next = { ...current };
      delete next[field];
      delete next._form;
      return next;
    });
  };

  const updatePrice = (priceType: number, patch: Partial<PriceFormRow>) => {
    setError(null);
    setForm((current) => current ? {
      ...current,
      prices: {
        ...current.prices,
        [priceType]: { ...current.prices[priceType], ...patch },
      },
    } : current);
    setFieldErrors((current) => {
      const next = { ...current };
      delete next[`prices.${priceType}.availabilityStatus`];
      delete next[`prices.${priceType}.webmasterPriceUsd`];
      delete next[`prices.${priceType}.webmasterPriceDetails`];
      delete next[`prices.${priceType}.sourceAmount`];
      delete next[`prices.${priceType}.sourceCurrency`];
      delete next._form;
      return next;
    });
  };

  const handleSave = async () => {
    if (!offerId || !edit || !form) return;
    if (previews.pending || Object.values(previews.errors).some(Boolean)) { setTab(1); return; }
    const clientErrors = validateWebmasterOfferForm(form);
    if (Object.keys(clientErrors).length > 0) {
      setFieldErrors(clientErrors);
      setTab(Object.keys(clientErrors).some((key) => key.startsWith('prices.')) ? 1 : 0);
      return;
    }

    setSaving(true);
    setError(null);
    setFieldErrors({});
    try {
      await webmasterOffersService.update(
        offerId,
        buildWebmasterOfferPayload(edit, form, previews.conversions)
      );
      onSaved();
    } catch (err) {
      if (err instanceof ApiClientError) {
        setFieldErrors(err.fieldErrors ?? {});
        setError(err.message);
        if (err.fieldErrors && Object.keys(err.fieldErrors).some((key) => key.startsWith('prices.'))) {
          setTab(1);
        }
      } else {
        setError(err instanceof Error ? err.message : 'Failed to save offer');
      }
    } finally {
      setSaving(false);
    }
  };

  const close = () => {
    if (!saving) onClose();
  };

  const handleTabChange = (_event: SyntheticEvent, value: number) => {
    setTab(value);
    if (value === 2) void history.load();
  };

  return (
    <Dialog
      open={open}
      onClose={close}
      maxWidth="lg"
      fullWidth
      slotProps={{ paper: { sx: { height: 'calc(100% - 64px)' } } }}
    >
      <DialogTitle sx={{ pb: edit && form && !loading ? 1.5 : 2 }}>
        <Box sx={{ minWidth: 0 }}>
          <Typography variant="h6" component="div" sx={{ fontWeight: 600 }}>
            Edit webmaster offer
            <Box component="span" sx={{ color: 'text.secondary', fontWeight: 400 }}> · {domain}</Box>
          </Typography>
          {edit && form && !loading && (
            <Box
              sx={{
                display: 'flex',
                alignItems: { xs: 'flex-start', sm: 'center' },
                justifyContent: 'space-between',
                flexDirection: { xs: 'column', sm: 'row' },
                gap: 0.5,
                mt: 0.25,
              }}
            >
              <Typography variant="body2" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>
                Webmaster: {edit.offer.primaryEmail || 'Not set'}
              </Typography>
              <Typography
                variant="caption"
                color="text.secondary"
                sx={{ flexShrink: 0, textAlign: { xs: 'left', sm: 'right' } }}
              >
                Updated {formatDateTime(edit.offer.updatedAtUtc)} by {edit.offer.updatedBy || 'system'}
              </Typography>
            </Box>
          )}
        </Box>
      </DialogTitle>

      {edit && form && !loading && (
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
          <Tab label="Offer details" />
          <Tab label="Prices" />
          <Tab label="History" />
        </Tabs>
      )}

      <DialogContent>
        {loading && (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 5 }}>
            <CircularProgress />
          </Box>
        )}
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        {edit && form && !loading && (
          <>
            {tab === 0 && (
              <WebmasterOfferDetailsTab
                edit={edit}
                form={form}
                fieldErrors={fieldErrors}
                selectedMailboxes={selectedMailboxes}
                updateField={updateField}
              />
            )}
            {tab === 1 && (
              <WebmasterOfferPricesTab
                form={form}
                fieldErrors={fieldErrors}
                updatePrice={updatePrice}
                edit={edit}
                currencies={currencies}
                previews={previews}
              />
            )}
            {tab === 2 && offerId && (
              <ChangeHistoryContent
                items={history.items}
                loading={history.loading}
                error={history.error}
              />
            )}
          </>
        )}
      </DialogContent>
      <DialogActions sx={{ borderTop: 1, borderColor: 'divider' }}>
        <BrandButton onClick={close} disabled={saving}>Cancel</BrandButton>
        <BrandButton kind="primary" onClick={handleSave} disabled={loading || saving || !edit || !form || !isDirty || previews.pending || Object.values(previews.errors).some(Boolean)}>
          {saving ? <CircularProgress size={18} color="inherit" /> : 'Save changes'}
        </BrandButton>
      </DialogActions>
    </Dialog>
  );
}
