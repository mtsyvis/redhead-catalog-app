import { createTheme } from '@mui/material/styles';

const brand = {
  ink: '#262626',
  accentStart: '#FF455B',
  accentEnd: '#FF7C32',
};

const accentGradient = `linear-gradient(0.25turn, rgba(255,69,91,1) 0%, rgba(255,124,50,1) 100%)`; // как на лендинге :contentReference[oaicite:1]{index=1}

declare module '@mui/material/styles' {
  interface Theme {
    custom: {
      accentGradient: string;
      ink: string;
      radius: number;
      cardRadius: number;
    };
  }
  interface ThemeOptions {
    custom?: Partial<Theme['custom']>;
  }
}

export const theme = createTheme({
  typography: {
    fontFamily: '"Outfit", system-ui, -apple-system, Segoe UI, Roboto, Arial, sans-serif',
    h4: { fontWeight: 800 },
    h5: { fontWeight: 800 },
    button: {
      textTransform: 'none',
      fontWeight: 500,
    },
  },
  palette: {
    primary: { main: brand.accentStart },
    text: {
      primary: brand.ink,
      secondary: 'rgba(38,38,38,0.72)',
    },
    divider: 'rgba(38,38,38,0.12)',
    background: {
      default: '#F6F7FB',
      paper: '#FFFFFF',
    },
  },
  shape: {
    borderRadius: 20,
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          color: brand.ink,
          WebkitFontSmoothing: 'antialiased',
          MozOsxFontSmoothing: 'grayscale',
        },
      },
    },

    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          borderRadius: 20,
          backgroundColor: '#fff',
        },
        notchedOutline: {
          borderColor: 'rgba(38,38,38,0.18)',
        },
      },
    },
    MuiTextField: {
      defaultProps: {
        variant: 'outlined',
      },
    },

    MuiCard: {
      styleOverrides: {
        root: {
          borderRadius: 28,
          border: '1px solid rgba(0,0,0,0.06)',
          boxShadow: '0 18px 60px rgba(0,0,0,0.12)',
        },
      },
    },

    MuiButton: {
      styleOverrides: {
        root: {
          borderRadius: 20,
          transitionDuration: '0.2s',
          transitionTimingFunction: 'ease-in-out',
          transitionProperty: 'background-color,color,border-color,box-shadow,opacity,transform,gap',
        },
      },
    },
  },
  custom: {
    accentGradient,
    ink: brand.ink,
    radius: 20,
    cardRadius: 28,
  },
});
