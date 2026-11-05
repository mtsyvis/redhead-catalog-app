import type { PricingMarket } from '../types/pricing.types';

export function marketDescription(market: PricingMarket | null): string {
  if (!market) return 'LinkDetective has not checked this domain yet.';
  const timestamp = market.checkedAtUtc
    ? `Retrieved: ${new Date(market.checkedAtUtc).toLocaleString()}`
    : 'No successful check yet';
  return `${timestamp}${market.isStale ? ' · Outdated' : ''}${market.lastError ? ` · Last check failed: ${market.lastError}` : ''}${market.checkedAtUtc && market.minimumUsd == null ? ' · No market prices found' : ''}`;
}
