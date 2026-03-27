import { Box, Button, Stack, Typography } from '@mui/material';
import { useState } from 'react';

export interface DuplicateDomainsPreviewProps {
  readonly duplicateDomainsCount: number;
  readonly duplicateDomainsPreview: string[];
}

export function DuplicateDomainsPreview({
  duplicateDomainsCount,
  duplicateDomainsPreview,
}: DuplicateDomainsPreviewProps) {
  const [showDuplicateDomains, setShowDuplicateDomains] = useState(false);

  if (duplicateDomainsCount <= 0) {
    return null;
  }

  return (
    <Stack spacing={1}>
      <Button
        variant="text"
        size="small"
        onClick={() => setShowDuplicateDomains((current) => !current)}
        sx={{
          alignSelf: 'flex-start',
          px: 0.5,
          minWidth: 0,
          color: 'text.secondary',
          fontWeight: 500,
          '&:hover': {
            color: 'text.primary',
            backgroundColor: 'transparent',
          },
        }}
      >
        {showDuplicateDomains
          ? 'Hide duplicate domains'
          : `Show duplicate domains (${duplicateDomainsCount})`}
      </Button>
      {showDuplicateDomains && (
        <Stack spacing={1}>
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
            {duplicateDomainsPreview.map((domain, index) => (
              <Box
                key={`${domain}-${index}`}
                sx={{
                  px: 1.25,
                  py: 0.5,
                  borderRadius: '999px',
                  bgcolor: 'action.hover',
                  fontSize: '0.75rem',
                  lineHeight: 1.4,
                }}
              >
                {domain}
              </Box>
            ))}
          </Box>
          {duplicateDomainsCount > duplicateDomainsPreview.length && duplicateDomainsPreview.length === 100 && (
            <Typography variant="caption" color="text.secondary">
              Showing first 100 duplicate domains.
            </Typography>
          )}
        </Stack>
      )}
    </Stack>
  );
}
