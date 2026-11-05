import { useMemo } from 'react';
import { useTableViews } from '../../common/table-views/useTableViews';
import { SITES_TABLE_KEY, sitesColumnRegistry, sitesSystemViews } from './sitesTableColumns';

export function useSitesTableViews({
  isClient,
  enabled = true,
}: {
  isClient: boolean;
  enabled?: boolean;
}) {
  const columns = useMemo(
    () => sitesColumnRegistry.filter((column) => !(isClient && column.hiddenForClient)),
    [isClient]
  );
  return useTableViews({
    tableKey: SITES_TABLE_KEY,
    columns,
    systemViews: sitesSystemViews,
    enabled,
  });
}
