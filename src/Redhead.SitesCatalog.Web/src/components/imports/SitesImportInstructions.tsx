import { SITES_IMPORT_INSTRUCTIONS } from '../../constants/imports.constants';
import { ImportInstructionsPanel } from './ImportInstructionsPanel';

const RULES = [
  'Column order does not matter.',
  'Include Term when any pricing or service column is present.',
  'Leave Term empty to save prices as No term.',
  'PriceUsd accepts empty or a positive numeric value.',
  'Service columns accept a positive numeric price, YES, NO, or empty.',
  'Service values: empty = Unknown, YES = available with unknown price, NO = not available.',
];

const EXAMPLES = [
  {
    title: 'Import a site with prices and services',
    csv:
      'Domain,DR,Traffic,Location,Niche,Categories,NumberDFLinks,SponsoredTag,Language,Term,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,PriceLinkInsertCasino,PriceDating\n' +
      'example.com,40,1000,US,Technology,News,3,Sponsored,EN,1 year,120,250,YES,NO,,175',
    note:
      'The example includes every supported column. Numeric service values set prices; YES, NO, and empty cells set service availability when no numeric price is provided.',
  },
];

const EXAMPLE_DOWNLOAD = {
  fileName: 'sites-import-example.csv',
  csv: EXAMPLES[0].csv,
};

export function SitesImportInstructions() {
  return (
    <ImportInstructionsPanel
      title={SITES_IMPORT_INSTRUCTIONS.title}
      description={SITES_IMPORT_INSTRUCTIONS.description}
      requiredColumns={SITES_IMPORT_INSTRUCTIONS.requiredColumns}
      requiredColumnsNote="These columns must be present and non-empty for each imported site."
      supportedColumns={SITES_IMPORT_INSTRUCTIONS.optionalColumns}
      supportedColumnsNote="These base columns are optional and may be omitted."
      pricingColumns={SITES_IMPORT_INSTRUCTIONS.pricingColumns}
      pricingColumnsNote="Supported Term values: No term, 1 year, 2 years, n years, permanent. An empty cell is treated as No term."
      rules={RULES}
      examples={EXAMPLES}
      exampleDownload={EXAMPLE_DOWNLOAD}
      alerts={[
        'If you know a service price, put the numeric value in the service column instead of YES.',
        'Use short language codes in CSV files, e.g. EN, DE, UNKNOWN, MULTI.',
      ]}
    />
  );
}
