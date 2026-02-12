import { createTheme } from '@mui/material/styles';

// Brand colors from spec
const brandColors = {
  textPrimary: '#262626',
  accentStart: '#FF455B',
  accentEnd: '#FF7C32',
  gradientAngle: 135, // degrees
};

// Create gradient string
const accentGradient = `linear-gradient(${brandColors.gradientAngle}deg, ${brandColors.accentStart} 0%, ${brandColors.accentEnd} 100%)`;

declare module '@mui/material/styles' {
  interface Theme {
    custom: {
      accentGradient: string;
      pillRadius: string;
    };
  }
  interface ThemeOptions {
    custom?: {
      accentGradient?: string;
      pillRadius?: string;
    };
  }
}

/**
 * MUI theme with Redhead brand tokens
 */
export const theme = createTheme({
  typography: {
    fontFamily: '"Outfit", "Roboto", "Helvetica", "Arial", sans-serif',
    button: {
      textTransform: 'none', // Keep natural casing
    },
  },
  palette: {
    primary: {
      main: brandColors.accentStart,
      contrastText: '#ffffff',
    },
    text: {
      primary: brandColors.textPrimary,
    },
  },
  shape: {
    borderRadius: 20, // Pill radius from spec
  },
  components: {
    MuiButton: {
      styleOverrides: {
        root: {
          borderRadius: 20,
          paddingLeft: 24,
          paddingRight: 24,
        },
      },
    },
  },
  custom: {
    accentGradient,
    pillRadius: '20px',
  },
});
