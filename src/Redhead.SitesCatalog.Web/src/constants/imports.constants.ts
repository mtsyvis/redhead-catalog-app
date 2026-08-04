export const IMPORT_COMMON_INSTRUCTIONS = {
  importantTitle: 'CSV UTF-8 only',
  importantNote: 'Save the file in UTF-8. Other encodings may import corrupted text.',
  saveInstructionsTitle: 'How to save the file correctly',
  saveInstructions: [
    'Excel: File → Save As → CSV UTF-8 (Comma delimited)',
    'Google Sheets: File → Download → Comma-separated values (.csv)',
  ],
}

export const SITES_IMPORT_INSTRUCTIONS = {
  title: 'Import new sites',
  description: 'Upload a CSV file to import new sites.',
  requiredColumns: [
    'Domain',
    'DR',
    'Traffic',
    'Location',
  ],
  optionalColumns: [
    'Niche',
    'Categories',
    'NumberDFLinks',
    'SponsoredTag',
    'Language',
  ],
  pricingColumns: [
    'Term',
    'PriceUsd',
    'PriceCasino',
    'PriceCrypto',
    'PriceLinkInsert',
    'PriceLinkInsertCasino',
    'PriceDating',
  ],
};

export const SITES_UPDATE_IMPORT_INSTRUCTIONS = {
  title: 'Update existing sites',
  description:
    'Upload a CSV with Domain and only the fields you want to change. Missing columns stay unchanged.',
  requiredColumns: ['Domain'],
  optionalColumns: [
    'DR',
    'Traffic',
    'Location',
    'Niche',
    'Categories',
    'NumberDFLinks',
    'SponsoredTag',
    'Language',
    'TrafficValueUsd',
    'PagesCount',
  ],
  pricingColumns: [
    'Term',
    'PriceUsd',
    'PriceCasino',
    'PriceCrypto',
    'PriceLinkInsert',
    'PriceLinkInsertCasino',
    'PriceDating',
  ],
  starterTemplate: 'Domain,TrafficValueUsd,PagesCount\nexample.com,1234.56,987',
};

export const WEBMASTER_OFFERS_IMPORT_COLUMNS = [
  'Domain',
  'MainWebmasterPriceDetails',
  'MainWebmasterPriceUsd',
  'CasinoWebmasterPriceDetails',
  'CasinoWebmasterPriceUsd',
  'CryptoWebmasterPriceDetails',
  'CryptoWebmasterPriceUsd',
  'DatingWebmasterPriceDetails',
  'DatingWebmasterPriceUsd',
  'LinkInsertionWebmasterPriceDetails',
  'LinkInsertionWebmasterPriceUsd',
  'LinkInsertion18PlusWebmasterPriceDetails',
  'LinkInsertion18PlusWebmasterPriceUsd',
  'BannerWebmasterPriceDetails',
  'BannerWebmasterPriceUsd',
  'Banner18PlusWebmasterPriceDetails',
  'Banner18PlusWebmasterPriceUsd',
  'HomepageTextLinkWebmasterPriceDetails',
  'HomepageTextLinkWebmasterPriceUsd',
  'HomepageTextLink18PlusWebmasterPriceDetails',
  'HomepageTextLink18PlusWebmasterPriceUsd',
  'LinkPolicyText',
  'DfLinksRawText',
  'SponsoredTagRawText',
  'Term',
  'LinkbuilderMailboxRawText',
  'OutreachSenderRawText',
  'ContactRawText',
  'CommentText',
  'ClientRawText',
] as const;

export const WEBMASTER_OFFERS_IMPORT_TEMPLATE_VALUES: Partial<
  Record<(typeof WEBMASTER_OFFERS_IMPORT_COLUMNS)[number], string>
> = {
  Domain: 'ze.nl',
  ContactRawText:
    'redactie@ze.nl\nsamenwerken@eenmedia.nl\nsamenwerken@eenmediapublishing.nl (отвечают тут)',
  OutreachSenderRawText: 'sharon.ratliffs@collaboffer.com',
  Term: '3 years',
  MainWebmasterPriceUsd: '956',
  MainWebmasterPriceDetails: '765 EUR',
  CasinoWebmasterPriceUsd: '1375',
  CasinoWebmasterPriceDetails: '1100 EUR',
  CryptoWebmasterPriceUsd: '1375',
  CryptoWebmasterPriceDetails: '1100 EUR',
  LinkInsertionWebmasterPriceDetails: 'NO',
  LinkInsertion18PlusWebmasterPriceDetails: 'NO',
  BannerWebmasterPriceDetails: 'NO',
  Banner18PlusWebmasterPriceDetails: 'NO',
  HomepageTextLinkWebmasterPriceDetails: 'NO',
  HomepageTextLink18PlusWebmasterPriceDetails: 'NO',
  LinkPolicyText: 'DF 2',
  DfLinksRawText: '2 DF links',
  SponsoredTagRawText: 'Sponsored',
  CommentText:
    '350 to 600 words\none duty-free picture (minimum of 900 x 600 pixels)\nTexts and photos are provided in a Word file and must be written in flawless Dutch',
  ClientRawText: 'Повторная связь для PM',
};

function toCsvValue(value: string) {
  if (!/[",\r\n]/.test(value)) {
    return value;
  }

  return `"${value.replace(/"/g, '""')}"`;
}

export const WEBMASTER_OFFERS_IMPORT_TEMPLATE = `${WEBMASTER_OFFERS_IMPORT_COLUMNS.join(',')}\n${WEBMASTER_OFFERS_IMPORT_COLUMNS.map(
  (column) => toCsvValue(WEBMASTER_OFFERS_IMPORT_TEMPLATE_VALUES[column] ?? '')
).join(',')}`;

export const WEBMASTER_OFFERS_IMPORT_INSTRUCTIONS = {
  title: 'Import webmaster offers',
  description: 'Upload the clean Webmaster Offers CSV. Domains must already exist in the catalog.',
  requiredColumns: WEBMASTER_OFFERS_IMPORT_COLUMNS,
  template: WEBMASTER_OFFERS_IMPORT_TEMPLATE,
};

export const AVAILABILITY_IMPORT_INSTRUCTIONS = {
  markUnavailable: {
    description: 'Upload a CSV file to mark existing sites as unavailable and optionally save a reason.',
    requiredColumns: ['Domain', 'Reason'],
    optionalNote: 'Reason may be empty.',
  },
  restoreAvailable: {
    description:
      'Upload a CSV file to restore existing sites as available. This will clear quarantine status and remove the quarantine reason.',
    requiredColumns: ['Domain'],
    optionalNote: 'Quarantine reason will be cleared for matched sites.',
  },
};

export const LAST_PUBLISHED_IMPORT_INSTRUCTIONS = {
  title: 'Update last published dates',
  description: 'Upload a CSV file to update the last published date for existing sites.',
  requiredColumns: ['Domain', 'LastPublishedDate'],
  optionalNote: 'LastPublishedDate is required. Supported formats: DD.MM.YYYY, January 2026, Jan 2026.',
};
