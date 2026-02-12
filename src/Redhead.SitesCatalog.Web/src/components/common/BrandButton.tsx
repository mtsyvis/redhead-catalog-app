import React from 'react';
import { Button, styled } from '@mui/material';
import type { ButtonProps } from '@mui/material';

/**
 * Styled button with brand gradient on hover
 */
const StyledBrandButton = styled(Button)(({ theme }) => ({
  border: `1px solid ${theme.palette.primary.main}`,
  color: theme.palette.primary.main,
  background: 'transparent',
  transition: 'all 0.3s ease',
  
  '&:hover': {
    background: theme.custom.accentGradient,
    color: '#ffffff',
    border: `1px solid transparent`,
    transform: 'translateY(-1px)',
    boxShadow: theme.shadows[4],
  },
  
  '&:active': {
    transform: 'translateY(0)',
    boxShadow: theme.shadows[2],
  },
  
  '&:disabled': {
    background: 'transparent',
    color: theme.palette.action.disabled,
    border: `1px solid ${theme.palette.action.disabled}`,
  },
}));

export interface BrandButtonProps extends Omit<ButtonProps, 'variant'> {
  children: React.ReactNode;
}

/**
 * Branded button component
 * - Outlined by default
 * - Gradient background on hover
 * - White text on hover
 */
export const BrandButton: React.FC<BrandButtonProps> = ({ children, ...props }) => {
  return (
    <StyledBrandButton variant="outlined" {...props}>
      {children}
    </StyledBrandButton>
  );
};
