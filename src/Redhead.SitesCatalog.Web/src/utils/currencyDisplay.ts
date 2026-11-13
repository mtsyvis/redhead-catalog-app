export function formatOfferRateSource(currency: string, rateDate?: string | null) {
  const source = currency === 'EUR' || currency === 'GBP' ? 'Company rate' : 'ExchangeRate-API';
  if (!rateDate) return source;

  const date = new Date(rateDate).toLocaleDateString('en-US', {
    day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC',
  });
  return `${source} · ${date}`;
}
