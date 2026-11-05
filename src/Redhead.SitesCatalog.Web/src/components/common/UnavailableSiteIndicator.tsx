import { Box, Tooltip } from '@mui/material';
import ErrorOutlineRoundedIcon from '@mui/icons-material/ErrorOutlineRounded';

export function UnavailableSiteIndicator() {
  return (
    <Tooltip
      title="Unavailable"
      arrow
      placement="top"
      slotProps={{ popper: { modifiers: [{ name: 'offset', options: { offset: [0, 24] } }] } }}
    >
      <Box
        component="span"
        role="img"
        aria-label="Unavailable site"
        tabIndex={0}
        sx={{
          display: 'inline-flex',
          flexShrink: 0,
          color: 'error.dark',
          outline: 'none',
          borderRadius: '50%',
          '&:focus-visible': {
            boxShadow: (theme) => `0 0 0 2px ${theme.palette.error.light}`,
          },
        }}
      >
        <ErrorOutlineRoundedIcon aria-hidden sx={{ fontSize: 18 }} />
      </Box>
    </Tooltip>
  );
}
