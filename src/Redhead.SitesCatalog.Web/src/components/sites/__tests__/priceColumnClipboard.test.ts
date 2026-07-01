import { describe, expect, it } from 'vitest';
import type {
  MultiSearchResultItem,
  ServiceAvailabilityStatus,
  Site,
  SitePriceOptionDto,
  SitePricingDto,
  SiteServiceAvailabilityDto,
} from '../../../types/sites.types';
import { PRICE_TYPE } from '../../../utils/pricing';
import { SERVICE_AVAILABILITY_STATUS } from '../../../utils/serviceAvailability';
import {
  buildPriceColumnClipboardText,
  formatPriceColumnClipboardValue,
  type CopyablePriceColumn,
} from '../priceColumnClipboard';

function createSite(pricing: SitePricingDto, domain = 'example.com'): Site {
  return {
    domain,
    dr: 50,
    traffic: 1000,
    location: 'United States',
    importedLocationRaw: 'US',
    language: 'EN',
    numberDFLinks: null,
    termType: null,
    termValue: null,
    termUnit: null,
    niche: null,
    categories: null,
    sponsoredTag: null,
    isQuarantined: false,
    quarantineReason: null,
    createdAtUtc: '2026-01-01T00:00:00Z',
    lastPublishedDate: null,
    lastPublishedDateIsMonthOnly: false,
    pricing,
  };
}

function createFoundResult(site: Site): MultiSearchResultItem {
  return { domain: site.domain, found: true, site };
}

function createPricing(
  prices: Array<{
    priceType: SitePriceOptionDto['priceType'];
    amountUsd: number;
    termKey?: string;
  }> = [],
  serviceAvailabilities: Array<{
    serviceType: SiteServiceAvailabilityDto['serviceType'];
    status: ServiceAvailabilityStatus;
  }> = []
): SitePricingDto {
  return {
    prices: prices.map((price, index) => ({
      priceType: price.priceType,
      termKey: price.termKey ?? `finite:${index + 1}:year`,
      termLabel: `${index + 1} year`,
      amountUsd: price.amountUsd,
    })),
    serviceAvailabilities,
  };
}

describe('price column clipboard serialization', () => {
  it.each<[CopyablePriceColumn, string]>([
    ['priceUsd', '128'],
    ['priceCasino', '423'],
    ['priceCrypto', '145'],
    ['priceLinkInsert', '112'],
    ['priceLinkInsertCasino', '225'],
    ['priceDating', '310'],
  ])('uses term-aware pricing for %s instead of legacy flat values', (field, expected) => {
    // Arrange
    const pricing = createPricing([
      { priceType: PRICE_TYPE.Main, amountUsd: 138, termKey: 'finite:2:year' },
      { priceType: PRICE_TYPE.Main, amountUsd: 128, termKey: 'finite:1:year' },
      { priceType: PRICE_TYPE.Casino, amountUsd: 433, termKey: 'finite:2:year' },
      { priceType: PRICE_TYPE.Casino, amountUsd: 423, termKey: 'finite:1:year' },
      { priceType: PRICE_TYPE.Crypto, amountUsd: 145 },
      { priceType: PRICE_TYPE.LinkInsertion, amountUsd: 112 },
      { priceType: PRICE_TYPE.LinkInsertionCasino, amountUsd: 225 },
      { priceType: PRICE_TYPE.Dating, amountUsd: 310 },
    ]);
    const siteWithLegacyPayload = {
      ...createSite(pricing),
      priceUsd: 118,
      priceCasino: 413,
      priceCrypto: 135,
      priceLinkInsert: 102,
      priceLinkInsertCasino: 215,
      priceDating: 300,
    };
    const result = createFoundResult(siteWithLegacyPayload);

    // Act
    const value = formatPriceColumnClipboardValue(result, field);

    // Assert
    expect(value).toBe(expected);
  });

  it.each<[CopyablePriceColumn, string]>([
    ['priceUsd', ''],
    ['priceCasino', 'NO'],
    ['priceCrypto', 'YES'],
    ['priceLinkInsert', ''],
    ['priceLinkInsertCasino', ''],
    ['priceDating', ''],
  ])('serializes availability and unknown states for %s', (field, expected) => {
    // Arrange
    const result = createFoundResult(
      createSite(
        createPricing([], [
          {
            serviceType: PRICE_TYPE.Casino,
            status: SERVICE_AVAILABILITY_STATUS.NotAvailable,
          },
          {
            serviceType: PRICE_TYPE.Crypto,
            status: SERVICE_AVAILABILITY_STATUS.AvailableWithUnknownPrice,
          },
          {
            serviceType: PRICE_TYPE.LinkInsertion,
            status: SERVICE_AVAILABILITY_STATUS.Unknown,
          },
        ])
      )
    );

    // Act
    const value = formatPriceColumnClipboardValue(result, field);

    // Assert
    expect(value).toBe(expected);
  });

  it('returns a blank value for a not-found domain', () => {
    // Arrange
    const result: MultiSearchResultItem = { domain: 'missing.com', found: false, site: null };

    // Act
    const value = formatPriceColumnClipboardValue(result, 'priceUsd');

    // Assert
    expect(value).toBe('');
  });

  it('preserves input order and blank lines when building clipboard text', () => {
    // Arrange
    const first = createFoundResult(
      createSite(createPricing([{ priceType: PRICE_TYPE.Main, amountUsd: 128 }]), 'first.com')
    );
    const missing: MultiSearchResultItem = { domain: 'missing.com', found: false, site: null };
    const third = createFoundResult(
      createSite(createPricing([{ priceType: PRICE_TYPE.Main, amountUsd: 316.5 }]), 'third.com')
    );
    const unknown = createFoundResult(createSite(createPricing(), 'unknown.com'));

    // Act
    const text = buildPriceColumnClipboardText([first, missing, third, unknown], 'priceUsd');

    // Assert
    expect(text).toBe('128\n\n316.5\n');
  });
});
