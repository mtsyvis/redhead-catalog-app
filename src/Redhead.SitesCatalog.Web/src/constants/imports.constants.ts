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
    'Niche',
    'Categories',
  ],
  optionalNote: 'PriceCasino, PriceCrypto, PriceLinkInsert, Niche, and Categories may be empty.',
};

export const SITES_UPDATE_IMPORT_INSTRUCTIONS = {
  description: 'Upload a CSV file to update existing sites.',
  requiredColumns: [
    'Domain',
    'DR',
    'Traffic',
    'Location',
    'PriceUsd',
    'PriceCasino',
    'PriceCrypto',
    'PriceLinkInsert',
    'Niche',
    'Categories',
  ],
  optionalNote: 'PriceCasino, PriceCrypto, PriceLinkInsert, Niche, and Categories may be empty.',
};

export const QUARANTINE_IMPORT_INSTRUCTIONS = {
  description: 'Upload a CSV file to update quarantine status for existing sites.',
  requiredColumns: ['Domain', 'Reason'],
  optionalNote: 'Reason may be empty.',
};

export const LAST_PUBLISHED_IMPORT_INSTRUCTIONS = {
  description: 'Upload a CSV file to update the last published date for existing sites.',
  requiredColumns: ['Domain', 'LastPublishedDate'],
  optionalNote: 'LastPublishedDate is required. Supported formats: DD.MM.YYYY, January 2026, Jan 2026.',
};

