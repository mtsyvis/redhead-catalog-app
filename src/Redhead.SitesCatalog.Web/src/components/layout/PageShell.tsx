import React from 'react';
import {
  AppBar,
  Toolbar,
  Typography,
  Container,
  Box,
  IconButton,
  Menu,
  MenuItem,
  styled,
  Tabs,
  Tab,
} from '@mui/material';
import { AccountCircle } from '@mui/icons-material';
import { useAuth } from '../../contexts/AuthContext';
import { useNavigate, useLocation } from 'react-router-dom';

const StyledAppBar = styled(AppBar)(({ theme }) => ({
  background: theme.palette.background.paper,
  color: theme.palette.text.primary,
  boxShadow: theme.shadows[1],
}));

export interface PageShellProps {
  children: React.ReactNode;
  title?: string;
  maxWidth?: 'xs' | 'sm' | 'md' | 'lg' | 'xl' | false;
}

/**
 * Page shell component with header, container, and consistent spacing
 */
export const PageShell: React.FC<PageShellProps> = ({
  children,
  title,
  maxWidth = 'lg',
}) => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [anchorEl, setAnchorEl] = React.useState<null | HTMLElement>(null);

  const handleMenu = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const handleClose = () => {
    setAnchorEl(null);
  };

  const handleLogout = async () => {
    handleClose();
    await logout();
    navigate('/login');
  };

  const handleChangePassword = () => {
    handleClose();
    navigate('/change-password');
  };

  const getCurrentTab = () => {
    if (location.pathname === '/sites') return '/sites';
    if (location.pathname === '/dashboard') return '/dashboard';
    return false;
  };

  const handleTabChange = (_event: React.SyntheticEvent, newValue: string) => {
    navigate(newValue);
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
      <StyledAppBar position="static">
        <Toolbar>
          <Typography variant="h6" component="div" sx={{ fontWeight: 600, mr: 4 }}>
            Redhead Sites Catalog
          </Typography>
          
          {user && (
            <>
              <Tabs
                value={getCurrentTab()}
                onChange={handleTabChange}
                sx={{
                  flexGrow: 1,
                  '& .MuiTab-root': {
                    color: 'text.secondary',
                    '&.Mui-selected': {
                      color: 'primary.main',
                    },
                  },
                }}
              >
                <Tab label="Sites" value="/sites" />
                <Tab label="Dashboard" value="/dashboard" />
              </Tabs>

              <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
                <Typography variant="body2" color="text.secondary">
                  {user.email}
                </Typography>
                <IconButton
                  size="large"
                  aria-label="account menu"
                  aria-controls="menu-appbar"
                  aria-haspopup="true"
                  onClick={handleMenu}
                  color="inherit"
                >
                  <AccountCircle />
                </IconButton>
                <Menu
                  id="menu-appbar"
                  anchorEl={anchorEl}
                  anchorOrigin={{
                    vertical: 'bottom',
                    horizontal: 'right',
                  }}
                  keepMounted
                  transformOrigin={{
                    vertical: 'top',
                    horizontal: 'right',
                  }}
                  open={Boolean(anchorEl)}
                  onClose={handleClose}
                >
                  <MenuItem onClick={handleChangePassword}>Change Password</MenuItem>
                  <MenuItem onClick={handleLogout}>Logout</MenuItem>
                </Menu>
              </Box>
            </>
          )}
        </Toolbar>
      </StyledAppBar>

      <Container
        maxWidth={maxWidth}
        sx={{
          flex: 1,
          py: 4,
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        {title && (
          <Typography variant="h4" component="h1" gutterBottom sx={{ mb: 3, fontWeight: 600 }}>
            {title}
          </Typography>
        )}
        {children}
      </Container>

      <Box
        component="footer"
        sx={{
          py: 2,
          px: 2,
          mt: 'auto',
          backgroundColor: (theme) => theme.palette.grey[100],
          textAlign: 'center',
        }}
      >
        <Typography variant="body2" color="text.secondary">
          © {new Date().getFullYear()} Redhead Sites Catalog
        </Typography>
      </Box>
    </Box>
  );
};
