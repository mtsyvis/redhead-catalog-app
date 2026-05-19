import React from 'react';
import { Box, Button, Container, Divider, Link, Stack, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';

import logoLockup from '../assets/brand/redhead-lockup.svg';

const supportEmail = 'support@rhda.us';

interface PublicPageShellProps {
  eyebrow: string;
  title: string;
  children: React.ReactNode;
}

const PublicPageShell: React.FC<PublicPageShellProps> = ({ eyebrow, title, children }) => {
  return (
    <Box
      sx={{
        minHeight: '100vh',
        background: `
          radial-gradient(circle at 14% 10%, rgba(255,69,91,0.10), transparent 40%),
          radial-gradient(circle at 86% 6%, rgba(255,124,50,0.08), transparent 36%),
          linear-gradient(180deg, #ffffff 0%, #F6F7FB 100%)
        `,
      }}
    >
      <Box
        component="header"
        sx={{
          borderBottom: '1px solid',
          borderColor: 'divider',
          backgroundColor: 'rgba(255,255,255,0.82)',
          backdropFilter: 'blur(12px)',
        }}
      >
        <Container
          maxWidth="md"
          sx={{
            minHeight: 72,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 3,
            py: 2,
          }}
        >
          <Box component={RouterLink} to="/oauth-home" sx={{ display: 'inline-flex' }}>
            <Box component="img" src={logoLockup} alt="Redhead" sx={{ height: 32, width: 'auto' }} />
          </Box>

          <Stack direction="row" spacing={{ xs: 1, sm: 2 }} useFlexGap flexWrap="wrap">
            <Button component={RouterLink} to="/oauth-home" color="inherit" size="small">
              Home
            </Button>
            <Button component={RouterLink} to="/privacy-policy" color="inherit" size="small">
              Privacy
            </Button>
            <Button component={RouterLink} to="/terms-of-service" color="inherit" size="small">
              Terms
            </Button>
          </Stack>
        </Container>
      </Box>

      <Container maxWidth="md" component="main" sx={{ py: { xs: 5, md: 8 } }}>
        <Stack spacing={4}>
          <Box>
            <Typography
              variant="overline"
              sx={{ color: 'primary.main', fontWeight: 800, letterSpacing: 0 }}
            >
              {eyebrow}
            </Typography>
            <Typography variant="h4" component="h1" sx={{ mt: 1, fontWeight: 800 }}>
              {title}
            </Typography>
          </Box>

          <Box
            sx={{
              p: { xs: 3, sm: 4 },
              border: '1px solid',
              borderColor: 'divider',
              borderRadius: (theme) => `${theme.custom.cardRadius}px`,
              backgroundColor: 'background.paper',
              boxShadow: '0 18px 60px rgba(0,0,0,0.08)',
              '& h2': {
                mt: 3,
                mb: 1,
                fontSize: '1.125rem',
                fontWeight: 800,
              },
              '& h2:first-of-type': {
                mt: 0,
              },
              '& p': {
                color: 'text.secondary',
                lineHeight: 1.7,
                mb: 1.5,
              },
              '& p:last-child': {
                mb: 0,
              },
              '& ul': {
                pl: 3,
                my: 1.5,
                color: 'text.secondary',
              },
              '& li': {
                mb: 1,
                lineHeight: 1.65,
              },
            }}
          >
            {children}
          </Box>
        </Stack>
      </Container>

      <Box component="footer" sx={{ py: 3, textAlign: 'center' }}>
        <Typography variant="body2" color="text.secondary">
          © {new Date().getFullYear()} Redhead Digital Agency
        </Typography>
      </Box>
    </Box>
  );
};

const ContactLink: React.FC = () => (
  <Link href={`mailto:${supportEmail}`} color="primary" underline="hover">
    {supportEmail}
  </Link>
);

export const OAuthHome: React.FC = () => (
  <PublicPageShell eyebrow="Private catalog platform" title="Redhead Catalog">
    <Typography component="p">
      Redhead Catalog is a private platform used by Redhead Digital Agency and authorized clients
      to browse a curated website catalog, search and filter available sites, and export selected
      data for business workflows.
    </Typography>

    <Typography component="p">
      Google Drive integration is optional. When a signed-in user connects Google Drive, Redhead
      Catalog can save user-generated Excel export files to that user's Google Drive account.
    </Typography>

    <Divider sx={{ my: 3 }} />

    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
      <Button component={RouterLink} to="/login" variant="contained" disableElevation>
        Open app
      </Button>
      <Button component={RouterLink} to="/privacy-policy" variant="outlined">
        Privacy Policy
      </Button>
      <Button component={RouterLink} to="/terms-of-service" variant="outlined">
        Terms of Service
      </Button>
    </Stack>

    <Typography component="p" sx={{ mt: 3 }}>
      For support, contact <ContactLink />.
    </Typography>
  </PublicPageShell>
);

export const PrivacyPolicy: React.FC = () => (
  <PublicPageShell eyebrow="Privacy Policy" title="Privacy Policy">
    <Typography component="p">Last updated: May 19, 2026</Typography>

    <Typography variant="h2" component="h2">
      Overview
    </Typography>
    <Typography component="p">
      Redhead Catalog is a private application for Redhead Digital Agency and authorized clients.
      Google Drive integration is optional and is used only when an authenticated user chooses to
      connect Google Drive for export delivery.
    </Typography>

    <Typography component="p">
      Redhead Catalog's use and transfer of information received from Google APIs complies with the
      Google API Services User Data Policy, including the Limited Use requirements.
    </Typography>

    <Typography variant="h2" component="h2">
      Google data we access
    </Typography>
    <Typography component="p">
      After user consent, the app accesses the user's Google account email or identifier and uses
      Google Drive permission only to create and manage export files generated by Redhead Catalog.
    </Typography>

    <Typography variant="h2" component="h2">
      Google Drive export files
    </Typography>
    <Box component="ul">
      <li>The app may create a dedicated export folder in the user's Google Drive.</li>
      <li>The app uploads user-generated Excel export files to that folder.</li>
      <li>The app does not read unrelated Google Drive files.</li>
    </Box>

    <Typography variant="h2" component="h2">
      Data we store
    </Typography>
    <Typography component="p">
      To provide the Google Drive export feature, Redhead Catalog stores the Google email or
      identifier, an encrypted refresh token, and the export folder id and name.
    </Typography>

    <Typography variant="h2" component="h2">
      Data use and sharing
    </Typography>
    <Box component="ul">
      <li>Redhead Catalog does not sell Google user data.</li>
      <li>Redhead Catalog does not use Google user data for advertising.</li>
      <li>
        Redhead Catalog does not share Google user data except as necessary to provide the feature
        or comply with law.
      </li>
    </Box>

    <Typography variant="h2" component="h2">
      User control
    </Typography>
    <Typography component="p">
      Users can disconnect Google Drive in the app if supported, or revoke access from their Google
      Account permissions page.
    </Typography>

    <Typography variant="h2" component="h2">
      Contact
    </Typography>
    <Typography component="p">
      For privacy questions, contact <ContactLink />.
    </Typography>
  </PublicPageShell>
);

export const TermsOfService: React.FC = () => (
  <PublicPageShell eyebrow="Terms of Service" title="Terms of Service">
    <Typography component="p">Last updated: May 19, 2026</Typography>

    <Typography variant="h2" component="h2">
      Authorized use
    </Typography>
    <Typography component="p">
      Redhead Catalog is available only to authorized Redhead Digital Agency users and authorized
      clients. There is no public self-service registration.
    </Typography>

    <Typography variant="h2" component="h2">
      Google Drive integration
    </Typography>
    <Typography component="p">
      Google Drive integration is optional. Users may choose to connect Google Drive so Redhead
      Catalog can save user-generated Excel export files to their Google Drive.
    </Typography>

    <Typography variant="h2" component="h2">
      Exported data
    </Typography>
    <Typography component="p">
      Users are responsible for exported catalog data and for how they store, use, and share exported
      files after export.
    </Typography>

    <Typography variant="h2" component="h2">
      Service changes
    </Typography>
    <Typography component="p">
      Redhead Digital Agency may change, suspend, or discontinue Redhead Catalog or the Google Drive
      export feature at any time.
    </Typography>

    <Typography variant="h2" component="h2">
      Limitation of liability
    </Typography>
    <Typography component="p">
      To the maximum extent permitted by law, Redhead Digital Agency is not liable for indirect,
      incidental, special, consequential, or punitive damages arising from use of Redhead Catalog,
      Google Drive exports, or exported files.
    </Typography>

    <Typography variant="h2" component="h2">
      Contact
    </Typography>
    <Typography component="p">
      For questions about these terms, contact <ContactLink />.
    </Typography>
  </PublicPageShell>
);
