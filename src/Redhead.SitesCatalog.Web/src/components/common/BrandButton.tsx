import React from 'react';
import { Button, styled } from '@mui/material';
import type { ButtonProps } from '@mui/material';

type Kind = 'outline' | 'primary';

const Root = styled(Button, {
  shouldForwardProp: (prop) => prop !== 'kind',
})<{ kind: Kind }>(({ theme, kind }) => {
  const ink = theme.custom.ink;

  const base = {
    fontWeight: 500,
    borderRadius: theme.custom.radius,
    textTransform: 'none' as const,
    position: 'relative' as const,
    overflow: 'hidden' as const,
    zIndex: 0,
    '&::after': {
      content: '""',
      position: 'absolute' as const,
      inset: 0,
      backgroundImage: theme.custom.accentGradient,
      opacity: 0,
      transition: 'opacity 0.2s ease-in-out',
      zIndex: -1,
    },
    '&:focus-visible::after': { opacity: 1 },
  };

  if (kind === 'primary') {
    return {
      ...base,
      color: '#fff',
      border: '1px solid transparent',
      backgroundImage: theme.custom.accentGradient,
      '&::after': { ...base['&::after'], opacity: 0 },
      '&:hover': { filter: 'brightness(0.97)' },
      '&:disabled': {
        background: theme.palette.action.disabledBackground,
        color: theme.palette.action.disabled,
        border: `1px solid ${theme.palette.action.disabledBackground}`,
        filter: 'none',
      },
    };
  }

  return {
    ...base,
    color: ink,
    border: `1px solid ${ink}`,
    background: 'transparent',
    '&:hover::after': { opacity: 1 },
    '&:hover': {
      color: '#fff',
      borderColor: '#fff',
    },
    '&:disabled': {
      color: theme.palette.action.disabled,
      border: `1px solid ${theme.palette.action.disabled}`,
    },
  };
});

export interface BrandButtonProps extends Omit<ButtonProps, 'variant' | 'color'> {
  kind?: Kind;
}

export const BrandButton: React.FC<BrandButtonProps> = ({ kind = 'outline', children, ...props }) => {
  return (
    <Root kind={kind} variant={kind === 'primary' ? 'contained' : 'outlined'} disableElevation {...props}>
      {children}
    </Root>
  );
};
