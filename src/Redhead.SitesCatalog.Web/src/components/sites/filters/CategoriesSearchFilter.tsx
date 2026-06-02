import { forwardRef, useImperativeHandle, useState } from 'react';
import { Autocomplete, TextField, Typography } from '@mui/material';

export interface CategoriesSearchFilterHandle {
  commitPendingInput: () => string[];
}

interface CategoriesSearchFilterProps {
  value: string[];
  onChange: (terms: string[]) => void;
  title?: string | null;
  inputLabel?: string;
  placeholder?: string;
  helperText?: string | null;
}

const CATEGORY_TERM_SEPARATOR = /[,\r\n]+/;

function parseCategorySearchTerms(input: string): string[] {
  return input
    .split(CATEGORY_TERM_SEPARATOR)
    .map((term) => term.trim())
    .filter((term) => term !== '');
}

function normalizeCategorySearchTerms(terms: string[]): string[] {
  const nextTerms: string[] = [];
  const seenTerms = new Set<string>();

  for (const rawTerm of terms) {
    const term = rawTerm.trim();
    const key = term.toLowerCase();

    if (term !== '' && !seenTerms.has(key)) {
      seenTerms.add(key);
      nextTerms.push(term);
    }
  }

  return nextTerms;
}

function mergeCategorySearchTerms(existingTerms: string[], input: string): string[] {
  return normalizeCategorySearchTerms([...existingTerms, ...parseCategorySearchTerms(input)]);
}

export const CategoriesSearchFilter = forwardRef<
  CategoriesSearchFilterHandle,
  CategoriesSearchFilterProps
>(function CategoriesSearchFilter(
  {
    value,
    onChange,
    title = 'Categories',
    inputLabel,
    placeholder = 'Search categories: travel blog, sports betting, crypto',
    helperText = 'Matches any category phrase. Separate terms with comma or Enter.',
  },
  ref
) {
  const [inputValue, setInputValue] = useState('');

  const commitInput = (input: string, notify: boolean): string[] => {
    const nextTerms = mergeCategorySearchTerms(value, input);
    setInputValue('');

    if (notify) {
      onChange(nextTerms);
    }

    return nextTerms;
  };

  useImperativeHandle(
    ref,
    () => ({
      commitPendingInput: () => {
        if (inputValue.trim() === '') {
          return value;
        }

        const nextTerms = mergeCategorySearchTerms(value, inputValue);
        setInputValue('');
        return nextTerms;
      },
    }),
    [inputValue, value]
  );

  return (
    <>
      {title && (
        <Typography variant="subtitle2" gutterBottom>
          {title}
        </Typography>
      )}
      <Autocomplete
        multiple
        freeSolo
        size="small"
        options={[]}
        value={value}
        inputValue={inputValue}
        onInputChange={(_, newInputValue) => setInputValue(newInputValue)}
        onChange={(_, newValue) => onChange(normalizeCategorySearchTerms(newValue))}
        renderInput={(params) => (
          <TextField
            {...params}
            label={inputLabel}
            placeholder={value.length === 0 ? placeholder : ''}
            helperText={helperText ?? undefined}
            onKeyDown={(event) => {
              if (event.key === 'Enter' || event.key === ',') {
                event.preventDefault();
                commitInput(inputValue, true);
              }
            }}
            onPaste={(event) => {
              const pastedText = event.clipboardData.getData('text');
              if (CATEGORY_TERM_SEPARATOR.test(pastedText)) {
                event.preventDefault();
                commitInput(`${inputValue}${pastedText}`, true);
              }
            }}
          />
        )}
        limitTags={30}
      />
    </>
  );
});
