import React from 'react';
import { Box, Paper, Stack, Typography } from '@mui/material';
import {
  IMPORT_COMMON_INSTRUCTIONS,
} from '../../constants/imports.constants';

export interface ImportInstructionsCardProps {
  title?: React.ReactNode;
  description: React.ReactNode;
  requiredColumns: readonly string[];
  optionalNote?: React.ReactNode;
}

export function ImportInstructionsCard({
  title,
  description,
  requiredColumns,
  optionalNote,
}: ImportInstructionsCardProps) {
  const hasRequiredColumns = requiredColumns.length > 0;

  return (
    <Box sx={{ mb: 3 }}>
      <Stack spacing={2}>
        {title && <Typography variant="subtitle1">{title}</Typography>}

        <Typography variant="body2">{description}</Typography>

        {(hasRequiredColumns || optionalNote) && (
          <Paper
            variant="outlined"
            sx={{
              p: 2,
            }}
          >
            <Stack spacing={1.5}>
              {hasRequiredColumns && (
                <Box>
                  <Typography variant="subtitle2" sx={{ mb: 0.75 }}>
                    Required columns in this exact order
                  </Typography>

                  <Typography variant="body2">
                    {requiredColumns.join(', ')}
                  </Typography>
                </Box>
              )}

              {optionalNote && (
                <Typography variant="body2" color="text.secondary">
                  {optionalNote}
                </Typography>
              )}
            </Stack>
          </Paper>
        )}

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
            {IMPORT_COMMON_INSTRUCTIONS.saveInstructions.map((item, index) => (
              <Typography
                key={index}
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
    </Box>
  );
}
