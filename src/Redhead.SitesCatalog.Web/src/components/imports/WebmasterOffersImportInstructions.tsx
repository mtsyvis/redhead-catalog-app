import {
  Box,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import DownloadIcon from '@mui/icons-material/Download';
import {
  WEBMASTER_OFFERS_IMPORT_COLUMNS,
  WEBMASTER_OFFERS_IMPORT_INSTRUCTIONS,
  WEBMASTER_OFFERS_IMPORT_TEMPLATE_VALUES,
} from '../../constants/imports.constants';
import { ImportInstructionsPanel } from './ImportInstructionsPanel';
import { downloadXlsxTemplate } from '../../utils/xlsxTemplate';

const RULES = [
  'CSV only. Do not upload legacy Excel headers directly.',
  'Use the downloaded template headers and copy values from the matching legacy Excel columns.',
  'The template column order follows the legacy Excel file as closely as possible. Keep the template order unchanged.',
  'Domains are matched by normalized domain and must already exist.',
  'One CSV row creates one webmaster offer.',
  'Repeated domains and repeated contacts are allowed.',
  'Phase 1 supports empty terms and year terms only, e.g. 1 year or 3 years.',
  'Unmapped linkbuilder mailbox aliases are warnings and do not block the offer.',
  'Client-facing prices are not calculated or updated.',
];

const LEGACY_COLUMN_MAPPING = [
  ['URL', 'Domain'],
  ['Price', 'MainWebmasterPriceDetails'],
  ['Price, $', 'MainWebmasterPriceUsd'],
  ['Price casino', 'CasinoWebmasterPriceDetails'],
  ['Price casino, $', 'CasinoWebmasterPriceUsd'],
  ['Price crypto', 'CryptoWebmasterPriceDetails'],
  ['Price Crypto, $', 'CryptoWebmasterPriceUsd'],
  ['Price Dating', 'DatingWebmasterPriceDetails'],
  ['Price Dating, $', 'DatingWebmasterPriceUsd'],
  ['link insertion', 'LinkInsertionWebmasterPriceDetails'],
  ['link insertion, $', 'LinkInsertionWebmasterPriceUsd'],
  ['link insertion 18+', 'LinkInsertion18PlusWebmasterPriceDetails'],
  ['link insertion 18+, $', 'LinkInsertion18PlusWebmasterPriceUsd'],
  ['Banner', 'BannerWebmasterPriceDetails'],
  ['Banner, $', 'BannerWebmasterPriceUsd'],
  ['Banner 18+', 'Banner18PlusWebmasterPriceDetails'],
  ['Banner 18+, $', 'Banner18PlusWebmasterPriceUsd'],
  ['Homepage text link', 'HomepageTextLinkWebmasterPriceDetails'],
  ['Price Homepage text link $', 'HomepageTextLinkWebmasterPriceUsd'],
  ['Homepage text link 18+', 'HomepageTextLink18PlusWebmasterPriceDetails'],
  ['Price Homepage text link 18+ $', 'HomepageTextLink18PlusWebmasterPriceUsd'],
  ['Какие ссылки и сколько', 'LinkPolicyText'],
  ['DF Links', 'DfLinksRawText'],
  ['Sponsored tag', 'SponsoredTagRawText'],
  ['Term', 'Term'],
  ['Новая почта для Линкбилдеров', 'LinkbuilderMailboxRawText'],
  ['Чья почта', 'OutreachSenderRawText'],
  ['Contact', 'ContactRawText'],
  ['Answer/comment', 'CommentText'],
  ['Client', 'ClientRawText'],
] as const;

function LegacyColumnMapping() {
  return (
    <Box>
      <Typography variant="subtitle2" sx={{ mb: 1 }}>
        Legacy Excel column mapping
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
        Start from the downloaded CSV template. For each legacy Excel row, copy the value from the
        left column into the matching new CSV column on the right.
      </Typography>
      <TableContainer sx={{ maxHeight: 360, border: 1, borderColor: 'divider', borderRadius: 1 }}>
        <Table size="small" stickyHeader>
          <TableHead>
            <TableRow>
              <TableCell>Legacy Excel column</TableCell>
              <TableCell>New CSV column</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {LEGACY_COLUMN_MAPPING.map(([legacyColumn, csvColumn]) => (
              <TableRow key={csvColumn}>
                <TableCell>{legacyColumn}</TableCell>
                <TableCell>{csvColumn}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
}

function downloadExcelTemplate() {
  downloadXlsxTemplate({
    fileName: 'webmaster-offers-import-template.xlsx',
    sheetName: 'Webmaster Offers',
    rows: [
      WEBMASTER_OFFERS_IMPORT_COLUMNS,
      WEBMASTER_OFFERS_IMPORT_COLUMNS.map(
        (column) => WEBMASTER_OFFERS_IMPORT_TEMPLATE_VALUES[column] ?? ''
      ),
    ],
  });
}

export function WebmasterOffersImportInstructions() {
  return (
    <ImportInstructionsPanel
      title={WEBMASTER_OFFERS_IMPORT_INSTRUCTIONS.title}
      description={WEBMASTER_OFFERS_IMPORT_INSTRUCTIONS.description}
      requiredColumns={WEBMASTER_OFFERS_IMPORT_INSTRUCTIONS.requiredColumns}
      requiredColumnsNote="Headers must match exactly and remain in this order."
      rules={RULES}
      examplesTitle="Template file"
      exampleActions={
        <Button
          size="small"
          variant="outlined"
          startIcon={<DownloadIcon />}
          onClick={downloadExcelTemplate}
        >
          Download Excel template
        </Button>
      }
      alerts={[
        'For amount columns ending with Usd, use a positive number only. If the legacy cell says NO or contains non-USD text, leave the amount cell empty and put the raw explanation in the matching details column.',
        'Supported Term values: No term, permanent, 1 year, 2 years, n years. Months are not supported and are preserved as raw text but imported as No term.',
      ]}
    >
      <LegacyColumnMapping />
    </ImportInstructionsPanel>
  );
}
