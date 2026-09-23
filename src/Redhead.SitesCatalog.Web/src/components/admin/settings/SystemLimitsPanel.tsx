import React from 'react';
import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Divider,
  Stack,
  Typography,
} from '@mui/material';

import type { ApplicationSettingsLimits } from '../../../types/applicationSettings.types';
import { BrandButton } from '../../common/BrandButton';
import { ApplicationSettingValueRow } from './ApplicationSettingValueRow';

interface SystemLimitsPanelProps {
  limits: ApplicationSettingsLimits | null;
  loading: boolean;
  error: string | null;
  onRetry: () => void;
}

export const SystemLimitsPanel: React.FC<SystemLimitsPanelProps> = ({
  limits,
  loading,
  error,
  onRetry,
}) => {
  if (loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error || !limits) {
    return (
      <Alert
        severity="error"
        action={(
          <BrandButton kind="outline" size="small" onClick={onRetry}>
            Retry
          </BrandButton>
        )}
      >
        {error ?? 'System limits are not available.'}
      </Alert>
    );
  }

  const groups = [
    {
      title: 'Catalog tools',
      description: 'Limits shared by catalog workflows.',
      rows: [
        {
          label: 'Global Multi-search input',
          description: 'Applied before role-specific restrictions.',
          value: `${limits.globalMultiSearchMaxInputs.toLocaleString()} / request`,
        },
        {
          label: 'Stop list',
          description: 'Maximum excluded domains.',
          value: `${limits.stopListMaxDomains.toLocaleString()} domains`,
        },
      ],
    },
    {
      title: 'Lite accounts',
      description: 'Restrictions applied to the Lite role.',
      rows: [
        {
          label: 'Multi-search input',
          description: 'Maximum unique domains in one request.',
          value: `${limits.liteMultiSearchMaxDomainsPerRequest.toLocaleString()} domains`,
        },
        {
          label: 'Monthly checks',
          description: 'Per Lite account.',
          value: `${limits.liteMonthlyDomainLimit.toLocaleString()} domains`,
        },
      ],
    },
    {
      title: 'Personal workspace',
      description: 'Per-user customization limits.',
      rows: [
        {
          label: 'Custom table views',
          description: 'Per user and table.',
          value: limits.customTableViewsPerUserTable.toLocaleString(),
        },
        {
          label: 'Saved filter sets',
          description: 'Per user and table.',
          value: limits.savedFilterSetsPerUserTable.toLocaleString(),
        },
      ],
    },
  ];

  return (
    <Box>
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          gap: 2,
          flexWrap: 'wrap',
          mb: 2,
        }}
      >
        <Box>
          <Typography variant="h6">System limits</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            Read-only product rules. Changing built-in values requires a new deployment.
          </Typography>
        </Box>
        <Chip label="Read only" variant="outlined" size="small" />
      </Box>

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', md: 'repeat(3, minmax(0, 1fr))' },
          gap: 3,
          alignItems: 'start',
        }}
      >
        {groups.map((group) => (
          <Card key={group.title}>
            <CardContent sx={{ p: 3 }}>
              <Typography variant="h6">{group.title}</Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 1 }}>
                {group.description}
              </Typography>
              <Stack divider={<Divider flexItem />}>
                {group.rows.map((row) => (
                  <ApplicationSettingValueRow
                    key={row.label}
                    label={row.label}
                    description={row.description}
                    value={row.value}
                    source="Built in"
                    changeBehavior="Deployment required"
                  />
                ))}
              </Stack>
            </CardContent>
          </Card>
        ))}
      </Box>
    </Box>
  );
};
