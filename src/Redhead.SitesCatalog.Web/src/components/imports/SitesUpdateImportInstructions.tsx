import {
  Alert,
  Box,
  Chip,
  Divider,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import {
  IMPORT_COMMON_INSTRUCTIONS,
  SITES_UPDATE_IMPORT_INSTRUCTIONS,
} from '../../constants/imports.constants';

const RULES = [
  'Domain is required and is used to find the site.',
  'Column order does not matter.',
  'Only included columns are updated.',
  'Unknown column names are rejected.',
  'Empty cells are treated as explicit values.',
  'Duplicate domains: last valid row wins.',
];

const EXAMPLES = [
  {
    title: 'Update language only',
    csv: 'Domain,Language\nexample.com,EN\nanother-site.com,UNKNOWN',
    note: 'Only Language changes. Other site fields remain unchanged.',
  },
  {
    title: 'Update price and term only',
    csv: 'Domain,PriceUsd,Term\nexample.com,120,1 year\nanother-site.com,,permanent',
    note:
      'Only PriceUsd and Term change. Empty PriceUsd clears it if the field allows clearing.',
  },
  {
    title: 'Set service availability',
    csv: 'Domain,PriceCasino\nexample.com,YES\nanother-site.com,NO',
    note:
      'YES means available with unknown price. Empty cells in included service columns clear that service to Unknown.',
  },
];

function CsvSnippet({ children }: { readonly children: string }) {
  return (
    <Box
      component="pre"
      sx={{
        m: 0,
        p: 1.5,
        overflowX: 'auto',
        bgcolor: 'action.hover',
        borderRadius: 1,
        fontFamily: 'monospace',
        fontSize: '0.8125rem',
        lineHeight: 1.6,
        whiteSpace: 'pre',
      }}
    >
      {children}
    </Box>
  );
}

export function SitesUpdateImportInstructions() {
  return (
    <Box sx={{ mb: 3 }}>
      <Paper sx={{ p: 3 }}>
        <Stack spacing={2.5}>
          <Stack spacing={0.75}>
            <Typography variant="h6">
              {SITES_UPDATE_IMPORT_INSTRUCTIONS.title}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {SITES_UPDATE_IMPORT_INSTRUCTIONS.description}
            </Typography>
          </Stack>

          <Box>
            <Typography variant="subtitle2" sx={{ mb: 0.75 }}>
              Rules
            </Typography>
            <Box component="ul" sx={{ m: 0, pl: 2.5 }}>
              {RULES.map((rule) => (
                <Typography
                  key={rule}
                  component="li"
                  variant="body2"
                  color="text.secondary"
                  sx={{ mb: 0.5 }}
                >
                  {rule}
                </Typography>
              ))}
            </Box>
          </Box>

          <Divider />

          <Box>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              Example CSV files
            </Typography>
            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: { xs: '1fr', md: 'repeat(2, minmax(0, 1fr))' },
                gap: 2,
              }}
            >
              {EXAMPLES.map((example) => (
                <Stack key={example.title} spacing={1}>
                  <Typography variant="body2" fontWeight={600}>
                    {example.title}
                  </Typography>
                  <CsvSnippet>{example.csv}</CsvSnippet>
                  <Typography variant="caption" color="text.secondary">
                    {example.note}
                  </Typography>
                </Stack>
              ))}
            </Box>
          </Box>

          <Alert severity="info">
            Domains not found in the catalog will be reported as unmatched.
          </Alert>

          <Box>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              Required column
            </Typography>
            <Chip
              label={SITES_UPDATE_IMPORT_INSTRUCTIONS.requiredColumn}
              size="small"
              sx={{ mr: 1, mb: 1 }}
            />

            <Typography variant="subtitle2" sx={{ mt: 1.5, mb: 1 }}>
              Allowed update columns
            </Typography>
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
              {SITES_UPDATE_IMPORT_INSTRUCTIONS.allowedUpdateColumns.map((column) => (
                <Chip key={column} label={column} size="small" variant="outlined" />
              ))}
            </Box>
          </Box>

          <Box>
            <Typography variant="subtitle2" sx={{ mb: 0.75 }}>
              Starter template
            </Typography>
            <CsvSnippet>{SITES_UPDATE_IMPORT_INSTRUCTIONS.starterTemplate}</CsvSnippet>
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{ display: 'block', mt: 0.75 }}
            >
              Remove any columns you do not want to update.
            </Typography>
          </Box>

          <Alert severity="info">
            Use short language codes in CSV files, e.g. EN, DE, UNKNOWN, MULTI.
            Do not use full language names.
          </Alert>

          <Box
            sx={{
              px: 1.5,
              py: 1.25,
              bgcolor: 'action.hover',
              borderLeft: (theme) => `3px solid ${theme.palette.text.primary}`,
            }}
          >
            <Stack spacing={0.5}>
              <Typography variant="subtitle2">
                {IMPORT_COMMON_INSTRUCTIONS.importantTitle}
              </Typography>
              <Typography variant="body2">
                {IMPORT_COMMON_INSTRUCTIONS.importantNote}
              </Typography>
            </Stack>
          </Box>

          <Box>
            <Typography variant="subtitle2" sx={{ mb: 0.75 }}>
              {IMPORT_COMMON_INSTRUCTIONS.saveInstructionsTitle}
            </Typography>
            <Box component="ul" sx={{ m: 0, pl: 2.5 }}>
              {IMPORT_COMMON_INSTRUCTIONS.saveInstructions.map((item) => (
                <Typography
                  key={item}
                  component="li"
                  variant="body2"
                  color="text.secondary"
                  sx={{ mb: 0.5 }}
                >
                  {item}
                </Typography>
              ))}
            </Box>
          </Box>
        </Stack>
      </Paper>
    </Box>
  );
}

export function SitesUpdateImportUploadNotes() {
  return (
    <Stack spacing={1}>
      <Alert severity="warning">
        Empty cells in columns you include are treated as intentional updates. For
        some fields this clears the value; for required fields it may create an
        invalid row.
      </Alert>
      <Alert severity="info">
        If the same domain appears more than once, the last valid row is applied.
      </Alert>
    </Stack>
  );
}
