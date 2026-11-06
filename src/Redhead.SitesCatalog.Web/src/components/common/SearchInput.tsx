import { useRef } from 'react';
import { Box, CircularProgress, IconButton, TextField } from '@mui/material';
import type { TextFieldProps } from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';

type SearchInputProps = Omit<
  TextFieldProps,
  'value' | 'size' | 'variant' | 'InputProps' | 'inputRef'
> & {
  value: string;
  onClear: () => void;
  searchSize?: 'compact' | 'large';
  loading?: boolean;
};

export function SearchInput({
  value,
  onClear,
  searchSize = 'large',
  loading = false,
  multiline = false,
  sx,
  ...props
}: SearchInputProps) {
  const inputRef = useRef<HTMLInputElement | HTMLTextAreaElement>(null);
  const compact = searchSize === 'compact';

  return (
    <TextField
      {...props}
      value={value}
      inputRef={inputRef}
      multiline={multiline}
      size={compact ? 'small' : 'medium'}
      InputProps={{
        startAdornment: (
          <SearchIcon
            fontSize={compact ? 'small' : 'medium'}
            sx={{ mr: 1, color: 'text.secondary' }}
          />
        ),
        endAdornment:
          loading || value ? (
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
              {loading && <CircularProgress size={18} color="inherit" aria-label="Searching" />}
              {value && (
                <IconButton
                  aria-label="Clear search"
                  edge="end"
                  size="small"
                  onMouseDown={(event) => event.preventDefault()}
                  onClick={() => {
                    onClear();
                    inputRef.current?.focus();
                  }}
                  sx={{ color: 'text.secondary' }}
                >
                  <ClearIcon fontSize="small" />
                </IconButton>
              )}
            </Box>
          ) : undefined,
      }}
      sx={[
        compact
          ? { width: { xs: '100%', sm: 260 }, flexShrink: 0 }
          : multiline
            ? {}
            : { flex: '1 1 360px', minWidth: { xs: '100%', md: 320 } },
        ...(Array.isArray(sx) ? sx : [sx]),
      ]}
    />
  );
}
