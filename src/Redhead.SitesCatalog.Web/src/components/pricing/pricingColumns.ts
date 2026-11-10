import { PRICE_TYPE_LABELS, PRICING_SECTION_ORDER, type PriceTypeValue } from '../../utils/pricing';
import type {
  TableColumnGroup,
  TableColumnMetadata,
  TableSystemView,
} from '../common/table-views/tableViewColumns';

export interface PricingColumn {
  id: string;
  label: string;
  priceType?: PriceTypeValue;
  marketField?: 'minimumUsd' | 'averageUsd' | 'medianUsd';
}
const priceColumn = (priceType: PriceTypeValue): PricingColumn => ({
  id: `price${priceType}`,
  label: PRICE_TYPE_LABELS[priceType],
  priceType,
});
export const pricingColumns: PricingColumn[] = [
  priceColumn(PRICING_SECTION_ORDER[0]),
  { id: 'ldMinimum', label: 'LD Minimum', marketField: 'minimumUsd' },
  { id: 'ldAverage', label: 'LD Average', marketField: 'averageUsd' },
  { id: 'ldMedian', label: 'LD Median', marketField: 'medianUsd' },
  ...PRICING_SECTION_ORDER.slice(1).map(priceColumn),
];
export const defaultPricingColumnIds = pricingColumns.map((column) => column.id);

export const pricingColumnGroups: TableColumnGroup[] = ['Main', 'Prices', 'Market prices'];
export const pricingTableColumns: TableColumnMetadata[] = [
  {
    id: 'domain',
    label: 'Domain',
    group: 'Main',
    required: true,
    includeInViews: true,
    width: 300,
    minWidth: 300,
  },
  {
    id: 'term',
    label: 'Term',
    group: 'Main',
    required: true,
    includeInViews: true,
    width: 100,
    minWidth: 100,
  },
  ...pricingColumns.map((column) => ({
    id: column.id,
    label: column.label,
    group: column.marketField ? ('Market prices' as const) : ('Prices' as const),
    includeInViews: true,
    width: 145,
    minWidth: 145,
  })),
];
export const pricingSystemViews: TableSystemView[] = [
  {
    key: 'default',
    name: 'Default',
    density: 'standard',
    visibleColumnIds: ['domain', 'term', ...defaultPricingColumnIds],
  },
];
