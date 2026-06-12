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
  description: 'Upload a CSV file to import new sites.',
  requiredColumns: [
    'Domain',
    'DR',
    'Traffic',
    'Location',
    'PriceUsd',
    'PriceCasino',
    'PriceCrypto',
    'PriceLinkInsert',
    'PriceLinkInsertCasino',
    'PriceDating',
    'Niche',
    'Categories',
    'NumberDFLinks',
    'SponsoredTag',
    'Term',
    'Language',
  ],
  optionalNote: 'PriceCasino, PriceCrypto, PriceLinkInsert, PriceLinkInsertCasino, PriceDating, Niche, Categories, NumberDFLinks, SponsoredTag, Term, and Language may be empty.',
};

export const SITES_UPDATE_IMPORT_INSTRUCTIONS = {
  title: 'Update existing sites',
  description:
    'Upload a CSV with Domain and only the fields you want to change. Missing columns stay unchanged.',
  requiredColumn: 'Domain',
  allowedUpdateColumns: [
    'DR',
    'Traffic',
    'Location',
    'PriceUsd',
    'PriceCasino',
    'PriceCrypto',
    'PriceLinkInsert',
    'PriceLinkInsertCasino',
    'PriceDating',
    'Niche',
    'Categories',
    'NumberDFLinks',
    'SponsoredTag',
    'Term',
    'Language',
  ],
  starterTemplate: 'Domain,Language,PriceUsd,Term\nexample.com,EN,120,1 year',
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
  description: 'Upload a CSV file to update the last published date for existing sites.',
  requiredColumns: ['Domain', 'LastPublishedDate'],
  optionalNote: 'LastPublishedDate is required. Supported formats: DD.MM.YYYY, January 2026, Jan 2026.',
};
