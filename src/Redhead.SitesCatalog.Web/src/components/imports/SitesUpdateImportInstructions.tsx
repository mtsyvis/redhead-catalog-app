import { Alert, Stack } from '@mui/material';
import { SITES_UPDATE_IMPORT_INSTRUCTIONS } from '../../constants/imports.constants';
import { ImportInstructionsPanel } from './ImportInstructionsPanel';

const RULES = [
  'Domain is required and is used to find the site.',
  'Include at least one update column besides Domain.',
  'Only included columns are updated.',
  'Missing columns stay unchanged.',
  'Empty cells are treated as explicit values.',
  'Unknown column names are rejected.',
  'Duplicate domains: last valid row wins.',
];

const EXAMPLES = [
  {
    title: 'Update language only',
    csv: 'Domain,Language\nexample.com,EN\nanother-site.com,UNKNOWN',
    note: 'Only Language changes. Other site fields remain unchanged.',
  },
  {
    title: 'Update Main price',
    csv: 'Domain,PriceUsd [1 year],PriceUsd [permanent]\nexample.com,120,350',
    note: 'Only the included Main price terms change.',
  },
  {
    title: 'Clear Main 1 year price',
    csv: 'Domain,PriceUsd [1 year]\nexample.com,',
    note: 'The empty included cell clears that exact Main 1-year price option.',
  },
  {
    title: 'Set service availability',
    csv: 'Domain,PriceCasinoAvailability\nexample.com,YES\nanother-site.com,NO\nunknown-site.com,',
    note:
      'YES means available with unknown price. Empty availability cells set the service to Unknown and clear service prices.',
  },
  {
    title: 'Update service price',
    csv: 'Domain,PriceCasino [1 year],PriceCasino [permanent]\nexample.com,250,600',
    note: 'Known service prices set that service to Has price.',
  },
  {
    title: 'Clear service price',
    csv: 'Domain,PriceCasino [1 year]\nexample.com,',
    note: 'The empty included cell clears that exact Casino 1-year price option.',
  },
  {
    title: 'Invalid combination',
    csv: 'Domain,PriceCasinoAvailability,PriceCasino [1 year]\nexample.com,NO,250',
    note: 'Invalid: service cannot be NO and have a price in the same row.',
  },
];

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
      pricingColumnsNote="Supported term labels: [unknown term], [1 year], [2 years], [n years], [permanent]."
      availabilityColumns={SITES_UPDATE_IMPORT_INSTRUCTIONS.availabilityColumns}
      availabilityColumnsNote="Availability columns accept empty, YES, or NO."
      rules={RULES}
      examples={EXAMPLES}
      alerts={[
        'Domains not found in the catalog will be reported as unmatched.',
        'YES means available with unknown price. If you know the price, use a term-specific price column instead.',
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
