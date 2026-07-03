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
      Users may sign in with Google. A first Google sign-in with a verified email creates a Lite
      account using the user's Google account identifier, email, name, and optional profile picture.
      Google Drive integration is a separate, optional feature that eligible signed-in users may
      connect to save user-generated Excel exports to their own Google Drive.
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
    <Typography component="p">Last updated: July 3, 2026</Typography>

    <Typography variant="h2" component="h2">
      Overview
    </Typography>
    <Typography component="p">
      Redhead Catalog is operated by Redhead Digital Agency for agency users and clients. Users may
      receive access through an invitation or register and sign in with Google. This policy explains
      how the application accesses, uses, stores, protects, and shares personal information,
      including information received from Google APIs.
    </Typography>

    <Typography component="p">
      Redhead Catalog's use and transfer of information received from Google APIs complies with the
      Google API Services User Data Policy, including the Limited Use requirements.
    </Typography>

    <Typography variant="h2" component="h2">
      Google sign-in data
    </Typography>
    <Typography component="p">
      When a user continues with Google, Redhead Catalog requests only the openid, profile, and email
      permissions. Google may provide the following information:
    </Typography>
    <Box component="ul">
      <li>A stable Google account identifier used to recognize the same account on later sign-ins.</li>
      <li>The user's email address and whether Google has verified it.</li>
      <li>The user's display name, when available.</li>
      <li>A Google-hosted profile picture URL, when available.</li>
    </Box>
    <Typography component="p">
      We use this information to authenticate the user, create and maintain the application account,
      prevent duplicate or unauthorized account linking, display the user's name and profile picture,
      enforce account status, and assign new Google-registered accounts the Lite role. Redhead Catalog
      does not receive or store the user's Google password and does not store Google OAuth access or
      refresh tokens from the sign-in flow.
    </Typography>

    <Typography variant="h2" component="h2">
      Separate Google Drive integration
    </Typography>
    <Typography component="p">
      Google Drive authorization is separate from Google sign-in. Eligible authenticated users may
      choose to connect Google Drive using the drive.file permission. This permission is used only to
      create and manage files and folders created by Redhead Catalog; it does not permit the app to
      read unrelated Drive files.
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
    <Box component="ul">
      <li>
        For Google sign-in: the stable Google identifier, verified email, display name when valid,
        optional profile picture URL, and normal application account and activity records.
      </li>
      <li>
        For an optional Google Drive connection: the Google email or identifier, granted scopes, an
        encrypted refresh token, connection status, and the export folder id and name.
      </li>
      <li>
        Essential authentication cookies used to keep a user signed in and temporary security cookies
        used to validate the Google sign-in redirect.
      </li>
    </Box>

    <Typography variant="h2" component="h2">
      Security
    </Typography>
    <Typography component="p">
      We use administrative, technical, and access-control measures intended to protect personal and
      Google user data. The service uses HTTPS in production, restricts account and administrative
      access by role, does not expose OAuth client secrets to the browser, and protects stored Google
      Drive refresh tokens using application encryption. No security measure can guarantee absolute
      security.
    </Typography>

    <Typography variant="h2" component="h2">
      Data use and sharing
    </Typography>
    <Box component="ul">
      <li>Redhead Catalog does not sell Google user data.</li>
      <li>Redhead Catalog does not use Google user data for advertising.</li>
      <li>Redhead Catalog does not use Google user data to train generalized AI models.</li>
      <li>
        We disclose information only to service providers needed to operate and secure the application,
        when directed by the user, in connection with legal obligations, or to protect users and the service.
        Service providers may process information only for those operational purposes.
      </li>
    </Box>

    <Typography variant="h2" component="h2">
      Retention and deletion
    </Typography>
    <Typography component="p">
      We retain account, security, and Google-related records for as long as reasonably necessary to
      provide the service, maintain business and security records, resolve disputes, and comply with
      legal obligations. Disabling an account prevents access but may preserve its records and history.
      When information is no longer required, it is deleted or anonymized according to applicable
      retention requirements.
    </Typography>
    <Typography component="p">
      Users may request deletion of their account and associated Google data by contacting <ContactLink />.
      We may need to verify the request and may retain limited information where required or permitted by law.
    </Typography>

    <Typography variant="h2" component="h2">
      User control
    </Typography>
    <Typography component="p">
      Eligible users can disconnect Google Drive in the app. Users can also revoke Redhead Catalog's
      Google permissions from their Google Account permissions page. Revoking Google access stops
      future Google-authorized access but does not automatically delete the Redhead Catalog account
      or records already stored by the application. Google-only users may lose the ability to sign in
      until access is restored or an administrator assists them.
    </Typography>

    <Typography variant="h2" component="h2">
      Policy changes
    </Typography>
    <Typography component="p">
      We may update this policy when application functionality or data practices change. The updated
      version and effective date will be published on this page. Where required, we will provide
      additional notice or request renewed consent.
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
    <Typography component="p">Last updated: July 3, 2026</Typography>

    <Typography variant="h2" component="h2">
      Acceptance
    </Typography>
    <Typography component="p">
      By accessing or using Redhead Catalog, you agree to these terms and the Privacy Policy. If you
      do not agree, do not use the service.
    </Typography>

    <Typography variant="h2" component="h2">
      Authorized use
    </Typography>
    <Typography component="p">
      Redhead Catalog is intended for Redhead Digital Agency users and clients. Access may be provided
      through an administrator invitation. A user with a Google-verified email may also register through
      Google sign-in; a newly registered Google account receives Lite access and the restrictions of that role.
      Registration does not guarantee continued access or eligibility for additional roles or features.
    </Typography>

    <Typography variant="h2" component="h2">
      Accounts and security
    </Typography>
    <Box component="ul">
      <li>Provide accurate account information and keep access credentials secure.</li>
      <li>Do not share an account or attempt to access another user's account.</li>
      <li>Notify Redhead Digital Agency promptly if you suspect unauthorized account access.</li>
      <li>Account permissions and feature limits are determined by the role assigned in Redhead Catalog.</li>
    </Box>

    <Typography variant="h2" component="h2">
      Google sign-in
    </Typography>
    <Typography component="p">
      Google sign-in is used to authenticate the user and may create a Lite account on first use. A
      Google-only account does not have a local Redhead Catalog password and must continue to use Google
      to sign in. Use of a Google account is also subject to Google's applicable terms and policies.
      Revoking Google access may prevent future sign-in but does not automatically delete the Redhead
      Catalog account.
    </Typography>

    <Typography variant="h2" component="h2">
      Google Drive integration
    </Typography>
    <Typography component="p">
      Google Drive integration is optional, separate from Google sign-in, and available only to eligible
      roles. Users who connect it authorize Redhead Catalog to create and manage application-generated
      export files in their Google Drive. Users may disconnect the integration or revoke access through
      their Google Account.
    </Typography>

    <Typography variant="h2" component="h2">
      Acceptable use
    </Typography>
    <Typography component="p">Users must not:</Typography>
    <Box component="ul">
      <li>Bypass role restrictions, usage limits, or other technical controls.</li>
      <li>Use the service unlawfully or to infringe the rights of others.</li>
      <li>Probe, disrupt, overload, reverse engineer, or compromise the service or its data.</li>
      <li>Use automated access except where explicitly authorized by Redhead Digital Agency.</li>
    </Box>

    <Typography variant="h2" component="h2">
      Exported data
    </Typography>
    <Typography component="p">
      Users are responsible for exported catalog data and for how they store, use, and share exported
      files after export. Catalog information may change and should be verified before relying on it for
      business decisions.
    </Typography>

    <Typography variant="h2" component="h2">
      Suspension and termination
    </Typography>
    <Typography component="p">
      Redhead Digital Agency may limit, suspend, disable, or terminate access when required for security,
      policy enforcement, legal compliance, operational reasons, or misuse of the service. Users may
      request account deletion by contacting support, subject to applicable retention obligations.
    </Typography>

    <Typography variant="h2" component="h2">
      Service changes
    </Typography>
    <Typography component="p">
      Redhead Digital Agency may change, suspend, or discontinue Redhead Catalog or the Google Drive
      export feature at any time. We may also update these terms. Continued use after updated terms take
      effect constitutes acceptance where permitted by law.
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
      Privacy
    </Typography>
    <Typography component="p">
      Our <Link component={RouterLink} to="/privacy-policy">Privacy Policy</Link> explains how we
      handle account information and Google user data and is incorporated into these terms.
    </Typography>

    <Typography variant="h2" component="h2">
      Contact
    </Typography>
    <Typography component="p">
      For questions about these terms, contact <ContactLink />.
    </Typography>
  </PublicPageShell>
);
