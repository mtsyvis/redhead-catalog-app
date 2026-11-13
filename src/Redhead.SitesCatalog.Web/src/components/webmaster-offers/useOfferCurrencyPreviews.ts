import { DEFAULT_CURRENCY } from '../../constants/currency.constants';
import { useEffect, useState } from 'react';
import type { OfferPriceConversion, WebmasterOfferEdit } from '../../types/webmasterOffers.types';
import { webmasterOffersService } from '../../services/webmasterOffers.service';
import { SERVICE_AVAILABILITY_STATUS } from '../../utils/serviceAvailability';
import { PRICE_TYPES, quoteAmountKey, type OfferFormState } from './editWebmasterOfferForm';

interface PreviewState {
  conversion?: OfferPriceConversion;
  error?: string;
}

interface PreviewBatch {
  edit: WebmasterOfferEdit | null;
  key: string;
  prices: Record<number, PreviewState>;
}

export function useOfferCurrencyPreviews(
  edit: WebmasterOfferEdit | null,
  form: OfferFormState | null
) {
  const [results, setResults] = useState<PreviewBatch>({ edit: null, key: '', prices: {} });
  const [retryCount, setRetryCount] = useState(0);
  const inputs = PRICE_TYPES.flatMap((type) => {
    const row = form?.prices[type.value];
    if (
      !row ||
      row.currency === DEFAULT_CURRENCY ||
      row.availabilityStatus !== SERVICE_AVAILABILITY_STATUS.Available ||
      !/^\d+(?:\.\d{1,2})?$/.test(row.amount) ||
      Number(row.amount) <= 0
    )
      return [];
    const saved = edit?.offer.prices.find(
      (price) => price.priceType === type.value || price.priceType === type.enumName
    );
    if (
      saved?.sourceCurrency === row.currency &&
      quoteAmountKey(saved.sourceAmount) === quoteAmountKey(row.amount)
    )
      return [];
    return [{ type: type.value, currency: row.currency, amount: row.amount }];
  });
  const requestKey = JSON.stringify({ inputs, retryCount });
  // Clear on every input/session change, even when returning to a previously previewed amount.
  if (results.edit !== edit || results.key !== requestKey) {
    setResults({ edit, key: requestKey, prices: {} });
  }

  useEffect(() => {
    let active = true;
    const requested: { inputs: typeof inputs } = JSON.parse(requestKey);
    const setPriceResult = (type: number, result: PreviewState) => {
      if (active)
        setResults((current) => current.edit === edit && current.key === requestKey
          ? { ...current, prices: { ...current.prices, [type]: result } }
          : current);
    };
    const timer = window.setTimeout(() => {
      requested.inputs.forEach((input) => {
        webmasterOffersService
          .previewConversion(input.currency, input.amount)
          .then((conversion) => {
            setPriceResult(input.type, { conversion });
          })
          .catch((error: unknown) => {
            setPriceResult(input.type, {
              error: error instanceof Error ? error.message : 'Conversion unavailable',
            });
          });
      });
    }, 300);
    return () => {
      active = false;
      window.clearTimeout(timer);
    };
  }, [requestKey, edit]);

  const conversions: Record<number, OfferPriceConversion | undefined> = {};
  const errors: Record<number, string | undefined> = {};
  let pending = false;
  inputs.forEach((input) => {
    const result = results.edit === edit && results.key === requestKey
      ? results.prices[input.type] : undefined;
    if (!result) {
      pending = true;
      return;
    }
    conversions[input.type] = result.conversion;
    errors[input.type] = result.error;
  });
  const retry = () => {
    setRetryCount((count) => count + 1);
  };
  return { conversions, errors, pending, retry };
}
