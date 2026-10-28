import {
  Alert,
  Box,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Typography,
} from '@mui/material';
import { APP_ROLES, ROLE_METADATA } from '../../constants/rbac.constants';
import type { AppRole } from '../../constants/rbac.constants';
import { BrandButton } from '../common/BrandButton';

const ROLE_GUIDE: Record<AppRole, { access: string; limits: string }> = {
  SuperAdmin: {
    access:
      'Full access. Manage users, roles, account access, export limits, application settings, and all catalog tools.',
    limits:
      'Catalog exports are unlimited. Super Admin is a protected role and cannot be assigned through role changes.',
  },
  Admin: {
    access:
      'Browse, edit, import, and export sites and webmaster offers. View webmaster contacts and prices, analytics, users, and settings. Manage Ahrefs sync.',
    limits:
      'Cannot create or change user accounts, reset passwords, change roles or limits, or edit application settings.',
  },
  Editor: {
    access:
      'Browse and edit sites and webmaster offers. View internal site fields, webmaster contacts, and webmaster prices.',
    limits: 'No imports, exports, analytics, user management, or settings.',
  },
  Linkbuilder: {
    access:
      'Browse sites and webmaster offers, including internal site fields, webmaster contacts, and webmaster prices.',
    limits: 'View only. No editing, imports, exports, analytics, user management, or settings.',
  },
  Internal: {
    access:
      'Browse sites, view internal site fields, and export sites when enabled. Read webmaster offer conditions and notes.',
    limits:
      'No webmaster contacts or prices. No editing, imports, analytics, user management, or settings.',
  },
  Client: {
    access:
      'Browse and filter sites, use Multi-search, and export sites when enabled. Only client-safe site fields are visible.',
    limits:
      'No internal fields, webmaster offers, editing, or admin tools. Catalog selections are limited to 100 sites by default; Super Admin can lower this or mark the account as a Trusted client.',
  },
  Lite: {
    access:
      'Use Multi-search to check a list of domains and see found and not-found results with client-safe site fields.',
    limits:
      'No catalog browsing, filters, exports, webmaster offers, editing, or admin tools.',
  },
};

export function RoleGuideDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullWidth
      maxWidth="md"
      aria-labelledby="role-guide-title"
      aria-describedby="role-guide-description"
    >
      <DialogTitle id="role-guide-title">What can each role do?</DialogTitle>
      <DialogContent dividers>
        <Typography id="role-guide-description" variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Each user has one role. All roles can use Multi-search and customize their table views.
          Use this guide to choose the right level of access.
        </Typography>
        <Box component="dl" sx={{ m: 0 }}>
          {APP_ROLES.map((role) => (
            <Box
              key={role}
              component="div"
              sx={{
                display: 'grid',
                gridTemplateColumns: { xs: '1fr', sm: '140px 1fr' },
                gap: { xs: 0.5, sm: 2 },
                py: 1.5,
                borderTop: 1,
                borderColor: 'divider',
              }}
            >
              <Typography component="dt" variant="subtitle2">
                {ROLE_METADATA[role].label}
              </Typography>
              <Box component="dd" sx={{ m: 0 }}>
                <Typography variant="body2">{ROLE_GUIDE[role].access}</Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                  {ROLE_GUIDE[role].limits}
                </Typography>
              </Box>
            </Box>
          ))}
        </Box>
        <Alert severity="info" sx={{ mt: 2 }}>
          Catalog exports for Admin, Internal, and Client depend on role settings and personal
          overrides. Clients also have daily and weekly export quotas. Trusted clients have no catalog
          selection cap, but export limits still apply. Editor, Linkbuilder, and Lite cannot export.
        </Alert>
      </DialogContent>
      <DialogActions>
        <BrandButton onClick={onClose}>Close</BrandButton>
      </DialogActions>
    </Dialog>
  );
}
