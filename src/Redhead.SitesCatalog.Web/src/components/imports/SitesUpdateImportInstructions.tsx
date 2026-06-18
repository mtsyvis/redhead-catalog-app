import { Alert, Stack } from '@mui/material';
import { SITES_UPDATE_IMPORT_INSTRUCTIONS } from '../../constants/imports.constants';
import { ImportInstructionsPanel } from './ImportInstructionsPanel';

const RULES = [
  'Domain is required and is used to find the site.',
  'Include at least one update column besides Domain.',
  'Include Term when any pricing or service column is present.',
  'Term alone does not count as an update column.',
  'Leave Term empty to save prices as No term.',
  'Only included columns are updated.',
  'Missing columns stay unchanged.',
  'Empty cells are treated as explicit values.',
  'Unknown column names are rejected.',
  'Duplicate domains: last valid row wins.',
];

const EXAMPLES = [
  {
    title: 'Update all prices and services',
    csv:
      'Domain,Term,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,PriceLinkInsertCasino,PriceDating\n' +
      'example.com,1 year,120,250,YES,NO,,175',
    note:
      'Numeric values set prices. YES means available without a known price. NO means unavailable. An empty service cell clears that Term price and becomes Unknown only when no prices for that service remain on other Terms.',
  },
  {
    title: 'Clear prices for a Term',
    csv: 'Domain,Term,PriceUsd,PriceCasino\nexample.com,1 year,,',
    note: 'Empty included price cells clear those exact price options for the row Term.',
  },
  {
    title: 'Update language only',
    csv: 'Domain,Language\nexample.com,EN\nanother-site.com,UNKNOWN',
    note: 'Only Language changes. Other site fields remain unchanged.',
  },
];

const EXAMPLE_DOWNLOAD = {
  fileName: 'sites-update-import-example.csv',
  csv:
    'Domain,DR,Traffic,Location,Niche,Categories,NumberDFLinks,SponsoredTag,Language,Term,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,PriceLinkInsertCasino,PriceDating\n' +
    'example.com,55,12000,US,Technology,News,3,Sponsored,EN,1 year,120,250,YES,NO,,175',
};

export function SitesUpdateImportInstructions() {
  return (
    <ImportInstructionsPanel
      title={SITES_UPDATE_IMPORT_INSTRUCTIONS.title}
      description={SITES_UPDATE_IMPORT_INSTRUCTIONS.description}
      requiredColumns={SITES_UPDATE_IMPORT_INSTRUCTIONS.requiredColumns}
      requiredColumnsNote="Domain is required. Add at least one supported update column to change data."
      supportedColumnsTitle="Supported base update columns"
      supportedColumns={SITES_UPDATE_IMPORT_INSTRUCTIONS.optionalColumns}
      supportedColumnsNote="Only columns included in the file are updated."
      pricingColumns={SITES_UPDATE_IMPORT_INSTRUCTIONS.pricingColumns}
      pricingColumnsNote="Supported Term values: No term, 1 year, 2 years, n years, permanent. An empty cell is treated as No term."
      rules={RULES}
      examples={EXAMPLES}
      exampleDownload={EXAMPLE_DOWNLOAD}
      alerts={[
        'Domains not found in the catalog will be reported as unmatched.',
        'YES means available with unknown price. If you know the price, put the numeric value in the service column instead.',
      ]}
    />
  );
}

export function SitesUpdateImportUploadNotes() {
  return (
    <Stack spacing={1}>
      <Alert severity="warning">
        Empty cells in columns you include are treated as intentional updates. For some fields this
        clears the value; for required fields it may create an invalid row.
      </Alert>
      <Alert severity="info">
        If the same domain appears more than once, the last valid row is applied.
      </Alert>
    </Stack>
  );
}
