import { useTableViews } from '../common/table-views/useTableViews';
import { pricingTableColumns, pricingSystemViews } from './pricingColumns';

export function usePricingTableViews(enabled: boolean) {
  return useTableViews({
    tableKey: 'pricing',
    columns: pricingTableColumns,
    systemViews: pricingSystemViews,
    enabled,
  });
}
