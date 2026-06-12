import { SITES_IMPORT_INSTRUCTIONS } from '../../constants/imports.constants';
import { ImportInstructionsPanel } from './ImportInstructionsPanel';

const RULES = [
  'Column order does not matter.',
  'Pricing columns are optional dynamic columns.',
  'Empty price cells mean no price.',
  'Availability values: empty = Unknown, YES = available with unknown price, NO = not available.',
  'A service cannot be YES or NO and have a price in the same row.',
];

const EXAMPLES = [
  {
    title: 'Main prices',
    csv:
      'Domain,DR,Traffic,Location,PriceUsd [1 year],PriceUsd [permanent]\n' +
      'example.com,40,1000,US,120,350',
  },
  {
    title: 'Service availability',
    csv:
      'Domain,DR,Traffic,Location,PriceCasinoAvailability\n' +
      'example.com,40,1000,US,YES\n' +
      'another-site.com,35,500,GB,NO',
  },
  {
    title: 'Service prices',
    csv:
      'Domain,DR,Traffic,Location,PriceCasino [1 year],PriceCasino [permanent]\n' +
      'example.com,40,1000,US,250,600',
  },
];

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
      pricingColumnsNote="Supported term labels: [unknown term], [1 year], [2 years], [n years], [permanent]."
      availabilityColumns={SITES_IMPORT_INSTRUCTIONS.availabilityColumns}
      availabilityColumnsNote="Availability columns accept empty, YES, or NO."
      rules={RULES}
      examples={EXAMPLES}
      alerts={[
        'If you know a service price, use a term-specific price column instead of YES.',
        'Use short language codes in CSV files, e.g. EN, DE, UNKNOWN, MULTI.',
      ]}
    />
  );
}
