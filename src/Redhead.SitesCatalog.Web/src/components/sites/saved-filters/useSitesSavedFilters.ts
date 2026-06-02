import { useCallback, useEffect, useMemo, useState } from 'react';
import { savedFiltersService } from '../../../services/savedFilters.service';
import type {
  SavedFilterSet,
  SavedFilterSettings,
} from '../../../types/savedFilters.types';
import { SITES_TABLE_KEY } from '../table-views/sitesTableColumns';

export function useSitesSavedFilters() {
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [filterSets, setFilterSets] = useState<SavedFilterSet[]>([]);
  const [activeFilterSetId, setActiveFilterSetId] = useState<string | null>(null);

  useEffect(() => {
    let ignore = false;

    async function loadFilterSets() {
      setLoading(true);
      setLoadError(null);
      try {
        const response = await savedFiltersService.getFilterSets(SITES_TABLE_KEY);
        if (ignore) return;
        setFilterSets(response.filterSets);
      } catch (error) {
        if (ignore) return;
        setFilterSets([]);
        setLoadError(
          error instanceof Error ? error.message : 'Saved filter sets could not be loaded'
        );
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    }

    loadFilterSets();

    return () => {
      ignore = true;
    };
  }, []);

  const activeFilterSet = useMemo(
    () => filterSets.find((filterSet) => filterSet.id === activeFilterSetId) ?? null,
    [activeFilterSetId, filterSets]
  );

  const createFilterSet = useCallback(
    async (name: string, settings: SavedFilterSettings) => {
      const created = await savedFiltersService.createFilterSet(SITES_TABLE_KEY, {
        name,
        settings,
      });
      setFilterSets((current) => [...current, created].sort(compareSavedFilterSets));
      return created;
    },
    []
  );

  const updateFilterSet = useCallback(
    async (id: string, name?: string, settings?: SavedFilterSettings) => {
      const updated = await savedFiltersService.updateFilterSet(SITES_TABLE_KEY, id, {
        name,
        settings,
      });
      setFilterSets((current) =>
        current.map((filterSet) => (filterSet.id === id ? updated : filterSet)).sort(compareSavedFilterSets)
      );
      setActiveFilterSetId(updated.id);
      return updated;
    },
    []
  );

  const deleteFilterSet = useCallback(async (id: string) => {
    await savedFiltersService.deleteFilterSet(SITES_TABLE_KEY, id);
    setFilterSets((current) => current.filter((filterSet) => filterSet.id !== id));
    setActiveFilterSetId((current) => (current === id ? null : current));
  }, []);

  return {
    loading,
    loadError,
    filterSets,
    activeFilterSetId,
    activeFilterSet,
    setActiveFilterSetId,
    createFilterSet,
    updateFilterSet,
    deleteFilterSet,
  };
}

function compareSavedFilterSets(left: SavedFilterSet, right: SavedFilterSet): number {
  return left.name.localeCompare(right.name);
}
