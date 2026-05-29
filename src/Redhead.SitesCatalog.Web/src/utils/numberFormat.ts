import type { GridLocaleText } from '@mui/x-data-grid';

const integerFormatter = new Intl.NumberFormat('en-US');

export function formatInteger(value: number): string {
  return integerFormatter.format(value);
}

export const dataGridLocaleText = {
  paginationDisplayedRows: ({ from, to, count, estimated }) => {
    const formattedFrom = formatInteger(from);
    const formattedTo = formatInteger(to);

    if (!estimated) {
      const formattedCount = count !== -1 ? formatInteger(count) : `more than ${formattedTo}`;
      return `${formattedFrom}–${formattedTo} of ${formattedCount}`;
    }

    const estimatedLabel =
      estimated > to ? `around ${formatInteger(estimated)}` : `more than ${formattedTo}`;
    const formattedCount = count !== -1 ? formatInteger(count) : estimatedLabel;

    return `${formattedFrom}–${formattedTo} of ${formattedCount}`;
  },
} satisfies Partial<GridLocaleText>;
