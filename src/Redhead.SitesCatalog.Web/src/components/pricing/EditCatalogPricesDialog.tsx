import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Tab,
  Tabs,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import { BrandButton } from "../common/BrandButton";
import { ChangeHistoryContent } from "../common/ChangeHistoryContent";
import { CatalogPricingFields } from "./CatalogPricingFields";
import { PricingMarketReference } from "./PricingMarketReference";
import { WebmasterOffersLink } from "../webmaster-offers/WebmasterOffersLink";
import { pricingService } from "../../services/pricing.service";
import { sitesService } from "../../services/sites.service";
import { ApiClientError } from "../../services/api.client";
import { useChangeHistory } from "../../hooks/useChangeHistory";
import type { PricingMarket, PricingSite } from "../../types/pricing.types";
import type { ServiceAvailabilityStatusValue } from "../../types/sites.types";
import { PRICE_TYPE_LABELS, type PriceTypeValue } from "../../utils/pricing";
import { SERVICE_AVAILABILITY_STATUS } from "../../utils/serviceAvailability";
import { CONFIRM_CLEAR_SERVICE_PRICES_MESSAGE } from "../sites/dialogs/EditSiteDialog.helpers";
import {
  buildPricingPayload,
  changePriceAmount,
  changeServiceStatus,
  createPricingDraft,
  getPricingTerms,
  hasServicePrices,
  getPricingChangeSummary,
  type CatalogPricingDraft,
} from "./EditCatalogPricesDialog.helpers";

export function EditCatalogPricesDialog({
  domain,
  initialTermKey,
  onMarketUpdated,
  onClose,
  onSaved,
}: {
  domain: string;
  initialTermKey?: string;
  onMarketUpdated: (market: PricingMarket) => void;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [site, setSite] = useState<PricingSite | null>(null);
  const [draft, setDraft] = useState<CatalogPricingDraft>({ amounts: {}, statuses: {} });
  const [tab, setTab] = useState(0);
  const [termKey, setTermKey] = useState("unknown");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [conflict, setConflict] = useState(false);
  const [discardOpen, setDiscardOpen] = useState(false);
  const [reload, setReload] = useState(0);
  const history = useChangeHistory(domain, () => sitesService.getHistory(domain));
  useEffect(() => {
    let active = true;
    const timer = window.setTimeout(() => {
      setLoading(true);
      pricingService
        .get(domain)
        .then((data) => {
          if (!active) return;
          setSite(data);
          setDraft(createPricingDraft(data));
          setTermKey(
            data.terms.find((term) => term.termKey === initialTermKey)?.termKey ??
              data.terms[0]?.termKey ??
              "unknown"
          );
          setError(null);
          setConflict(false);
        })
        .catch((reason) => {
          if (active)
            setError(reason instanceof Error ? reason.message : "Failed to load catalog prices.");
        })
        .finally(() => {
          if (active) setLoading(false);
        });
    }, 0);
    return () => {
      active = false;
      window.clearTimeout(timer);
    };
  }, [domain, initialTermKey, reload]);

  const terms = getPricingTerms(site);
  const changes = site ? getPricingChangeSummary(site, draft) : null;
  const dirty = changes != null && (changes.priceCount > 0 || changes.availabilityCount > 0);
  const changeSummary = changes
    ? [
        changes.priceCount > 0
          ? `${changes.priceCount} price ${changes.priceCount === 1 ? "change" : "changes"}`
          : null,
        changes.termCount > 0
          ? `${changes.termCount} ${changes.termCount === 1 ? "term" : "terms"}`
          : null,
        changes.availabilityCount > 0
          ? `${changes.availabilityCount} availability ${changes.availabilityCount === 1 ? "change" : "changes"}`
          : null,
      ]
        .filter(Boolean)
        .join(" · ")
    : "";
  const { pricing, errors } = buildPricingPayload(terms, draft);
  const unfavorable = pricing.prices.filter((price) => {
    const offer = site?.prices.find(
      (row) => row.priceType === price.priceType && row.termKey === price.termKey
    )?.offerPrice;
    return offer != null && price.amountUsd - offer <= 0;
  });
  const close = () => {
    if (saving) return;
    if (dirty) setDiscardOpen(true);
    else onClose();
  };
  const changeStatus = (type: PriceTypeValue, status: ServiceAvailabilityStatusValue) => {
    if (status !== SERVICE_AVAILABILITY_STATUS.Available && hasServicePrices(draft, type)) {
      if (!window.confirm(CONFIRM_CLEAR_SERVICE_PRICES_MESSAGE)) return;
    }
    setDraft((current) => changeServiceStatus(current, type, status));
  };
  const save = async () => {
    if (!site || errors.length || site.hasInvalidTerms) return;
    setSaving(true);
    setError(null);
    try {
      await pricingService.save(site, pricing);
      onSaved();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Failed to save catalog prices.");
      setConflict(reason instanceof ApiClientError && reason.statusCode === 409);
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <Dialog
        open
        onClose={close}
        maxWidth="md"
        fullWidth
        slotProps={{ paper: { sx: { height: "calc(100% - 64px)" } } }}
        aria-labelledby="edit-catalog-prices-title"
      >
        <DialogTitle
          id="edit-catalog-prices-title"
          sx={{ overflowWrap: "anywhere", py: 1.5, borderBottom: 1, borderColor: "divider" }}
        >
          <Stack
            direction="row"
            flexWrap="wrap"
            alignItems="center"
            justifyContent="space-between"
            gap={1}
          >
            <Box component="span" sx={{ flex: "1 1 240px", minWidth: 0 }}>
              Edit catalog prices · {domain}
            </Box>
            <WebmasterOffersLink domain={domain} />
          </Stack>
        </DialogTitle>
        <Stack
          direction="row"
          flexWrap="wrap"
          alignItems="center"
          justifyContent="space-between"
          gap={1}
          sx={{ px: 3, py: 1 }}
        >
          <Tabs
            sx={{ flexShrink: 0, minHeight: 40, "& .MuiTab-root": { minHeight: 40, py: 1 } }}
            value={tab}
            onChange={(_, value: number) => {
              setTab(value);
              if (value === 1) void history.load();
            }}
            aria-label="Catalog price dialog sections"
          >
            <Tab id="catalog-prices-tab" aria-controls="catalog-prices-panel" label="Prices" />
            <Tab id="catalog-history-tab" aria-controls="catalog-history-panel" label="History" />
          </Tabs>
          {tab === 0 &&
            site &&
            !loading &&
            (terms.length === 1 ? (
              <Chip label={terms[0].label} variant="outlined" size="small" />
            ) : (
              <Stack
                direction="row"
                alignItems="center"
                spacing={1}
                sx={{ minWidth: 0, maxWidth: "100%" }}
              >
                <Typography variant="caption" color="text.secondary" sx={{ flexShrink: 0 }}>
                  Term
                </Typography>
                <Box sx={{ minWidth: 0, overflowX: "auto" }}>
                  <ToggleButtonGroup
                    exclusive
                    size="small"
                    value={termKey}
                    onChange={(_, value: string | null) => {
                      if (value !== null) setTermKey(value);
                    }}
                    aria-label="Catalog placement term"
                    sx={{
                      "& .MuiToggleButton-root": {
                        px: 1.5,
                        py: 0.5,
                        textTransform: "none",
                        whiteSpace: "nowrap",
                        "&.Mui-selected": { color: "primary.main", bgcolor: "action.selected" },
                      },
                    }}
                  >
                    {terms.map((term) => (
                      <ToggleButton key={term.termKey} value={term.termKey}>
                        {term.label}
                      </ToggleButton>
                    ))}
                  </ToggleButtonGroup>
                </Box>
              </Stack>
            ))}
        </Stack>
        <DialogContent dividers sx={{ py: 1.5 }}>
          <Stack spacing={1.5}>
            {error && (
              <Alert
                severity="error"
                action={
                  <BrandButton
                    size="small"
                    disabled={saving}
                    onClick={() => {
                      if (
                        !dirty ||
                        window.confirm("Discard your changes and reload the latest catalog prices?")
                      )
                        setReload((value) => value + 1);
                    }}
                  >
                    Reload
                  </BrandButton>
                }
              >
                {error}
              </Alert>
            )}
            {loading && (
              <Box sx={{ textAlign: "center", py: 4 }}>
                <CircularProgress />
              </Box>
            )}
            {tab === 1 && (
              <Stack
                id="catalog-history-panel"
                role="tabpanel"
                aria-labelledby="catalog-history-tab"
                spacing={1}
              >
                <ChangeHistoryContent
                  items={history.items}
                  loading={history.loading}
                  error={history.error}
                />
                {history.error && (
                  <BrandButton onClick={() => void history.load()} disabled={history.loading}>
                    Retry history
                  </BrandButton>
                )}
              </Stack>
            )}
            {site && (
              <Stack
                id="catalog-prices-panel"
                role="tabpanel"
                aria-labelledby="catalog-prices-tab"
                spacing={1.5}
                sx={{ display: tab === 0 && !loading ? "flex" : "none" }}
              >
                <PricingMarketReference
                  domain={domain}
                  market={site.market}
                  onUpdated={(market) => {
                    setSite((current) => current && { ...current, market });
                    onMarketUpdated(market);
                  }}
                />
                {site.hasInvalidTerms && (
                  <Alert severity="warning">
                    Some placement terms need correction in the full site editor before these prices
                    can be saved.
                  </Alert>
                )}
                <CatalogPricingFields
                  site={site}
                  termKey={termKey}
                  amounts={draft.amounts}
                  statuses={draft.statuses}
                  disabled={saving || conflict}
                  onStatusChange={changeStatus}
                  onAmountChange={(type, value) =>
                    setDraft((current) => changePriceAmount(current, type, termKey, value))
                  }
                />
                {unfavorable.length > 0 && (
                  <Alert severity="warning">
                    Zero or negative margin:{" "}
                    {[
                      ...new Set(unfavorable.map((price) => PRICE_TYPE_LABELS[price.priceType])),
                    ].join(", ")}
                    .
                  </Alert>
                )}
                {errors.length > 0 && <Alert severity="error">{errors.join(" ")}</Alert>}
              </Stack>
            )}
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, flexWrap: "wrap", rowGap: 1 }}>
          <Typography
            variant="caption"
            color="text.secondary"
            aria-live="polite"
            sx={{ flex: "1 1 180px", mr: 1 }}
          >
            {changeSummary}
          </Typography>
          <BrandButton onClick={close} disabled={saving}>
            Cancel
          </BrandButton>
          <BrandButton
            kind="primary"
            onClick={() => void save()}
            disabled={
              loading ||
              saving ||
              !dirty ||
              errors.length > 0 ||
              !site ||
              site.hasInvalidTerms ||
              conflict
            }
          >
            {saving ? "Saving…" : "Save prices"}
          </BrandButton>
        </DialogActions>
      </Dialog>
      <Dialog open={discardOpen} onClose={() => setDiscardOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Discard price changes?</DialogTitle>
        <DialogContent>Your changes have not been saved.</DialogContent>
        <DialogActions>
          <BrandButton onClick={() => setDiscardOpen(false)}>Keep editing</BrandButton>
          <BrandButton kind="primary" onClick={onClose}>
            Discard changes
          </BrandButton>
        </DialogActions>
      </Dialog>
    </>
  );
}
