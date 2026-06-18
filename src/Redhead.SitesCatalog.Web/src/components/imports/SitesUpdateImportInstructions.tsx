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
    title: 'Update language only',
    csv: 'Domain,Language\nexample.com,EN\nanother-site.com,UNKNOWN',
    note: 'Only Language changes. Other site fields remain unchanged.',
  },
  {
    title: 'Update Main price',
    csv: 'Domain,Term,PriceUsd\nexample.com,1 year,120',
    note: 'Only the included Main price for the row Term changes.',
  },
  {
    title: 'Clear Main 1 year price',
    csv: 'Domain,Term,PriceUsd\nexample.com,1 year,',
    note: 'The empty included cell clears that exact Main 1-year price option.',
  },
  {
    title: 'Set service availability',
    csv: 'Domain,Term,PriceCasino\nexample.com,,YES\nanother-site.com,,NO\nunknown-site.com,,',
    note:
      'YES means available with unknown price. Empty service cells set Unknown only when no other numeric service prices remain.',
  },
  {
    title: 'Update service price',
    csv: 'Domain,Term,PriceCasino,PriceCrypto\nexample.com,permanent,250,600',
    note: 'Known service prices set that service to Has price.',
  },
  {
    title: 'Clear service price',
    csv: 'Domain,Term,PriceCasino\nexample.com,1 year,',
    note: 'The empty included cell clears that exact Casino 1-year price option.',
  },
  {
    title: 'Invalid Term',
    csv: 'Domain,Term,PriceUsd\nexample.com,1 month,120',
    note: 'Invalid: leave Term empty for No term, or use No term, permanent, or a positive year term.',
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
      pricingColumnsNote="Supported Term values: No term, 1 year, 2 years, n years, permanent. An empty cell is treated as No term."
      rules={RULES}
      examples={EXAMPLES}
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
