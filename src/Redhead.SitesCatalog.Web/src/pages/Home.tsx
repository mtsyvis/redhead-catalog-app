import React from 'react';
import { Typography, Card, CardContent, Box } from '@mui/material';
import { PageShell } from '../components/layout/PageShell';
import { useAuth } from '../contexts/AuthContext';

/**
 * Home page - placeholder for future sites listing
 */
export const Home: React.FC = () => {
  const { user } = useAuth();

  return (
    <PageShell title="Dashboard">
      <Card>
        <CardContent sx={{ p: 4 }}>
          <Typography variant="h5" gutterBottom sx={{ fontWeight: 600 }}>
            Welcome, {user?.email}!
          </Typography>
          
          <Typography variant="body1" color="text.secondary" paragraph>
            Your role(s): {user?.roles.join(', ')}
          </Typography>

          <Box sx={{ mt: 3, p: 3, bgcolor: 'grey.50', borderRadius: 2 }}>
            <Typography variant="body2" color="text.secondary">
              Sites listing feature coming in Step 7...
            </Typography>
          </Box>
        </CardContent>
      </Card>
    </PageShell>
  );
};
