import React from 'react';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Divider,
  Typography,
} from '@mui/material';

import type { ApplicationSettingsLimits } from '../../../types/applicationSettings.types';
import { BrandButton } from '../../common/BrandButton';

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

  const groups: Array<{
    title: string;
    description: string;
    source?: 'Environment' | 'Built in';
    changeBehavior?: string;
    rows: Array<{ label: string; description: string; value: string }>;
  }> = [
    {
      title: 'LinkDetective',
      description: 'Market price refresh policy.',
      source: 'Environment',
      changeBehavior: 'Restart required',
      rows: [{
        label: 'Price cache lifetime',
        description: 'Older prices refresh automatically on a single-domain search. Manual refresh is also available.',
        value: `${limits.linkDetectiveCacheLifetimeDays} ${limits.linkDetectiveCacheLifetimeDays === 1 ? 'day' : 'days'}`,
      }],
    },
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
            Current limits and refresh policies used by the application.
          </Typography>
        </Box>
        <Chip label="Read only" variant="outlined" size="small" />
      </Box>

      <Box
        sx={{
          bgcolor: 'background.paper',
          border: 1,
          borderColor: 'divider',
          borderRadius: 1,
          overflow: 'hidden',
        }}
      >
        {groups.map((group, index) => (
          <Box
            component="section"
            aria-labelledby={`system-limits-group-${index}`}
            key={group.title}
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', md: '220px minmax(0, 1fr)' },
              columnGap: { md: 4 },
              px: { xs: 2, sm: 3 },
              py: 2.5,
              borderTop: index === 0 ? 0 : 1,
              borderColor: 'divider',
            }}
          >
            <Box sx={{ mb: { xs: 2, md: 0 } }}>
              <Typography id={`system-limits-group-${index}`} variant="subtitle1" component="h3" fontWeight={600}>
                {group.title}
              </Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                {group.description}
              </Typography>
              <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap', mt: 1 }}>
                <Chip label={group.source ?? 'Built in'} variant="outlined" size="small" sx={{ height: 22 }} />
                <Typography variant="caption" color="text.secondary">
                  {group.changeBehavior ?? 'Deployment required'}
                </Typography>
              </Box>
            </Box>
            <Box component="dl" sx={{ m: 0, minWidth: 0 }}>
              {group.rows.map((row, rowIndex) => (
                <React.Fragment key={row.label}>
                  {rowIndex > 0 && <Divider sx={{ my: 1.5 }} />}
                  <Box sx={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) auto', columnGap: { xs: 2, sm: 3 }, rowGap: 0.5, alignItems: 'baseline' }}>
                    <Typography component="dt" variant="body2" fontWeight={600}>{row.label}</Typography>
                    <Typography component="dd" variant="body2" fontWeight={600} sx={{ m: 0, textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>
                      {row.value}
                    </Typography>
                    <Typography component="dd" variant="body2" color="text.secondary" sx={{ gridColumn: '1 / -1', m: 0 }}>
                      {row.description}
                    </Typography>
                  </Box>
                </React.Fragment>
              ))}
            </Box>
          </Box>
        ))}
      </Box>
    </Box>
  );
};
