import type React from 'react';
import { useState } from 'react';
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  Chip,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { IMPORT_COMMON_INSTRUCTIONS } from '../../constants/imports.constants';

export interface ImportInstructionExample {
  title: string;
  csv: string;
  note?: React.ReactNode;
}

export interface ImportInstructionsPanelProps {
  title: React.ReactNode;
  description: React.ReactNode;
  requiredColumns: readonly string[];
  requiredColumnsNote?: React.ReactNode;
  supportedColumnsTitle?: React.ReactNode;
  supportedColumns?: readonly string[];
  supportedColumnsNote?: React.ReactNode;
  pricingColumns?: readonly string[];
  pricingColumnsNote?: React.ReactNode;
  rules?: readonly React.ReactNode[];
  examples?: readonly ImportInstructionExample[];
  alerts?: readonly React.ReactNode[];
  children?: React.ReactNode;
}

export function CsvSnippet({ children }: { readonly children: string }) {
  return (
    <Box
      component="pre"
      sx={{
        m: 0,
        p: 1.25,
        overflowX: 'auto',
        bgcolor: 'action.hover',
        borderRadius: 1,
        fontFamily: 'monospace',
        fontSize: '0.78rem',
        lineHeight: 1.6,
        whiteSpace: 'pre',
      }}
    >
      {children}
    </Box>
  );
}

function CompactAccordion({
  title,
  children,
  expanded,
  onChange,
}: {
  title: React.ReactNode;
  children: React.ReactNode;
  expanded: boolean;
  onChange: (expanded: boolean) => void;
}) {
  return (
    <Accordion
      variant="outlined"
      disableGutters
      expanded={expanded}
      onChange={(_event, nextExpanded) => onChange(nextExpanded)}
      sx={{
        borderRadius: 1,
        '&::before': { display: 'none' },
        '& + &': { mt: 1 },
      }}
    >
      <AccordionSummary
        expandIcon={<ExpandMoreIcon />}
        sx={{
          minHeight: 44,
          '& .MuiAccordionSummary-content': {
            my: 1,
          },
        }}
      >
        <Typography variant="subtitle2">{title}</Typography>
      </AccordionSummary>
      <AccordionDetails sx={{ pt: 0 }}>
        {children}
      </AccordionDetails>
    </Accordion>
  );
}

function ChipList({ items }: { readonly items: readonly string[] }) {
  if (items.length === 0) return null;

  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
      {items.map((item) => (
        <Chip key={item} label={item} size="small" variant="outlined" />
      ))}
    </Box>
  );
}

function ColumnSection({
  title,
  items,
  note,
}: {
  title: React.ReactNode;
  items: readonly string[];
  note?: React.ReactNode;
}) {
  if (items.length === 0 && !note) return null;

  return (
    <Box>
      <Typography variant="subtitle2" sx={{ mb: 1 }}>
        {title}
      </Typography>
      <ChipList items={items} />
      {note && (
        <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
          {note}
        </Typography>
      )}
    </Box>
  );
}

function CommonFileInstructions() {
  return (
    <>
      <Box
        sx={{
          px: 1.5,
          py: 1.25,
          bgcolor: 'action.hover',
          borderLeft: (theme) => `3px solid ${theme.palette.text.primary}`,
        }}
      >
        <Stack spacing={0.5}>
          <Typography variant="subtitle2">{IMPORT_COMMON_INSTRUCTIONS.importantTitle}</Typography>
          <Typography variant="body2">{IMPORT_COMMON_INSTRUCTIONS.importantNote}</Typography>
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
    </>
  );
}

export function ImportInstructionsPanel({
  title,
  description,
  requiredColumns,
  requiredColumnsNote,
  supportedColumnsTitle = 'Supported optional columns',
  supportedColumns = [],
  supportedColumnsNote,
  pricingColumns = [],
  pricingColumnsNote,
  rules = [],
  examples = [],
  alerts = [],
  children,
}: ImportInstructionsPanelProps) {
  const [rulesExpanded, setRulesExpanded] = useState(false);
  const [examplesExpanded, setExamplesExpanded] = useState(false);
  const [selectedExample, setSelectedExample] = useState<string | null>(null);
  const activeExample =
    examples.find((example) => example.title === selectedExample) ?? examples[0] ?? null;
  const hasColumnSections =
    requiredColumns.length > 0 ||
    supportedColumns.length > 0 ||
    pricingColumns.length > 0;

  return (
    <Box sx={{ mb: 3 }}>
      <Paper sx={{ p: 3 }}>
        <Stack spacing={2.5}>
          <Stack spacing={0.75}>
            <Typography variant="h6">{title}</Typography>
            <Typography variant="body2" color="text.secondary">
              {description}
            </Typography>
          </Stack>

          {hasColumnSections && (
            <Stack spacing={2}>
              <ColumnSection
                title="Required columns"
                items={requiredColumns}
                note={requiredColumnsNote}
              />
              <ColumnSection
                title={supportedColumnsTitle}
                items={supportedColumns}
                note={supportedColumnsNote}
              />
              <ColumnSection
                title="Supported pricing columns"
                items={pricingColumns}
                note={pricingColumnsNote}
              />
            </Stack>
          )}

          {rules.length > 0 && (
            <CompactAccordion title="Rules" expanded={rulesExpanded} onChange={setRulesExpanded}>
              <Box component="ul" sx={{ m: 0, pl: 2.5 }}>
                {rules.map((rule, index) => (
                  <Typography
                    key={index}
                    component="li"
                    variant="body2"
                    color="text.secondary"
                    sx={{ mb: 0.5 }}
                  >
                    {rule}
                  </Typography>
                ))}
              </Box>
            </CompactAccordion>
          )}

          {examples.length > 0 && (
            <CompactAccordion
              title="Example CSV files"
              expanded={examplesExpanded}
              onChange={(nextExpanded) => {
                setExamplesExpanded(nextExpanded);
                if (nextExpanded) {
                  setSelectedExample((current) => current ?? examples[0]?.title ?? null);
                } else {
                  setSelectedExample(null);
                }
              }}
            >
              <Stack spacing={1.5}>
                <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
                  {examples.map((example) => {
                    const selected = activeExample?.title === example.title;
                    return (
                      <Button
                        key={example.title}
                        size="small"
                        variant={selected ? 'contained' : 'outlined'}
                        onClick={() => setSelectedExample(example.title)}
                        sx={{
                          borderRadius: 999,
                          minHeight: 30,
                          px: 1.5,
                          py: 0.35,
                          textTransform: 'none',
                        }}
                      >
                        {example.title}
                      </Button>
                    );
                  })}
                </Box>

                {activeExample && (
                  <Box
                    sx={{
                      p: 1.5,
                      border: 1,
                      borderColor: 'divider',
                      borderRadius: 1,
                      bgcolor: 'background.paper',
                    }}
                  >
                    <Stack spacing={1}>
                      <Typography variant="body2" fontWeight={600}>
                        {activeExample.title}
                      </Typography>
                      <CsvSnippet>{activeExample.csv}</CsvSnippet>
                      {activeExample.note && (
                        <Typography variant="caption" color="text.secondary">
                          {activeExample.note}
                        </Typography>
                      )}
                    </Stack>
                  </Box>
                )}
              </Stack>
            </CompactAccordion>
          )}

          {alerts.map((alert, index) => (
            <Alert key={index} severity="info">
              {alert}
            </Alert>
          ))}

          {children}

          <CommonFileInstructions />
        </Stack>
      </Paper>
    </Box>
  );
}
