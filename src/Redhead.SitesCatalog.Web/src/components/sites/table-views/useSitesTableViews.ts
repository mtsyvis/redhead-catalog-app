import { useCallback, useEffect, useMemo, useState } from 'react';
import type { GridColumnVisibilityModel } from '@mui/x-data-grid';
import { tableViewsService } from '../../../services/tableViews.service';
import type {
  TableCustomView,
  TableViewDensity,
  TableViewSettings,
  TableViewType,
} from '../../../types/tableViews.types';
import {
  createSitesViewSettings,
  normalizeSitesColumnWidth,
  normalizeSitesVisibleColumnIds,
  SITES_TABLE_KEY,
  sitesColumnRegistry,
  sitesSystemViews,
} from './sitesTableColumns';
import type { SitesColumnMetadata, SitesSystemView } from './sitesTableColumns';

interface UseSitesTableViewsOptions {
  isClient: boolean;
  enabled?: boolean;
}

interface ActiveView {
  type: TableViewType;
  key: string;
  name: string;
}

export function useSitesTableViews({ isClient, enabled = true }: UseSitesTableViewsOptions) {
  const [loading, setLoading] = useState(enabled);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [activeView, setActiveViewState] = useState<ActiveView>({
    type: 'system',
    key: 'default',
    name: 'Default',
  });
  const [customViews, setCustomViews] = useState<TableCustomView[]>([]);
  const [draftVisibleColumnIds, setDraftVisibleColumnIds] = useState<string[]>([]);
  const [draftDensity, setDraftDensity] = useState<TableViewDensity>('standard');
  const [draftColumnWidths, setDraftColumnWidths] = useState<Record<string, number>>({});

  const allowedViewColumns = useMemo(
    () =>
      sitesColumnRegistry.filter(
        (column) =>
          column.includeInViews && !column.systemOnly && !(isClient && column.hiddenForClient)
      ),
    [isClient]
  );

  const effectiveSystemViews = useMemo(
    () =>
      sitesSystemViews.map((view) => {
        const visibleColumnIds =
          view.key === 'full'
            ? allowedViewColumns.map((column) => column.id)
            : normalizeSitesVisibleColumnIds(view.visibleColumnIds, allowedViewColumns);

        return {
          ...view,
          visibleColumnIds,
        };
      }),
    [allowedViewColumns]
  );

  const getSystemView = useCallback(
    (key: string): SitesSystemView =>
      effectiveSystemViews.find((view) => view.key === key) ?? effectiveSystemViews[0],
    [effectiveSystemViews]
  );

  const createSanitizedSettings = useCallback(
    (
      visibleColumnIdsInput: string[],
      density: TableViewDensity,
      columnWidths: Record<string, number> = {}
    ): TableViewSettings =>
      sanitizeSettings(
        createSitesViewSettings(visibleColumnIdsInput, density, columnWidths),
        allowedViewColumns
      ),
    [allowedViewColumns]
  );

  const resolveView = useCallback(
    (viewType: TableViewType, viewKey: string, views: TableCustomView[]) => {
      if (viewType === 'custom') {
        const customView = views.find((view) => view.id === viewKey);
        if (customView) {
          const settings = sanitizeSettings(customView.settings, allowedViewColumns);
          return {
            active: { type: 'custom' as const, key: customView.id, name: customView.name },
            visibleColumnIds: settings.visibleColumnIds,
            density: settings.density,
            columnWidths: settings.columnWidths,
          };
        }
      }

      const systemView = getSystemView(viewKey);
      return {
        active: { type: 'system' as const, key: systemView.key, name: systemView.name },
        visibleColumnIds: systemView.visibleColumnIds,
        density: systemView.density,
        columnWidths: createSanitizedSettings(
          systemView.visibleColumnIds,
          systemView.density,
          systemView.columnWidths
        ).columnWidths,
      };
    },
    [allowedViewColumns, createSanitizedSettings, getSystemView]
  );

  useEffect(() => {
    let ignore = false;

    async function loadPreferences() {
      if (!enabled) {
        const fallback = getSystemView('default');
        const fallbackSettings = createSanitizedSettings(
          fallback.visibleColumnIds,
          fallback.density
        );
        setCustomViews([]);
        setActiveViewState({ type: 'system', key: fallback.key, name: fallback.name });
        setDraftVisibleColumnIds(fallbackSettings.visibleColumnIds);
        setDraftDensity(fallbackSettings.density);
        setDraftColumnWidths(fallbackSettings.columnWidths);
        setLoadError(null);
        setLoading(false);
        return;
      }

      setLoading(true);
      setLoadError(null);
      try {
        const response = await tableViewsService.getTableViews(SITES_TABLE_KEY);
        if (ignore) return;

        setCustomViews(response.customViews);
        const resolved = resolveView(
          response.activeViewType,
          response.activeViewKey,
          response.customViews
        );
        setActiveViewState(resolved.active);
        setDraftVisibleColumnIds(resolved.visibleColumnIds);
        setDraftDensity(resolved.density);
        setDraftColumnWidths(resolved.columnWidths);
      } catch (error) {
        if (ignore) return;
        const fallback = getSystemView('default');
        const fallbackSettings = createSanitizedSettings(
          fallback.visibleColumnIds,
          fallback.density
        );
        setCustomViews([]);
        setActiveViewState({ type: 'system', key: fallback.key, name: fallback.name });
        setDraftVisibleColumnIds(fallbackSettings.visibleColumnIds);
        setDraftDensity(fallbackSettings.density);
        setDraftColumnWidths(fallbackSettings.columnWidths);
        setLoadError(
          error instanceof Error ? error.message : 'Table view preferences could not be loaded'
        );
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    }

    loadPreferences();

    return () => {
      ignore = true;
    };
  }, [createSanitizedSettings, enabled, getSystemView, resolveView]);

  const columnVisibilityModel = useMemo<GridColumnVisibilityModel>(() => {
    const visible = new Set(normalizeSitesVisibleColumnIds(draftVisibleColumnIds, allowedViewColumns));

    return Object.fromEntries(
      allowedViewColumns.map((column) => [column.id, column.required || visible.has(column.id)])
    );
  }, [allowedViewColumns, draftVisibleColumnIds]);

  const activeSettings = useMemo(() => {
    if (activeView.type === 'custom') {
      const customView = customViews.find((view) => view.id === activeView.key);
      return customView
        ? sanitizeSettings(customView.settings, allowedViewColumns)
        : createSanitizedSettings(getSystemView('default').visibleColumnIds, 'standard');
    }

    const systemView = getSystemView(activeView.key);
    return createSanitizedSettings(
      systemView.visibleColumnIds,
      systemView.density,
      systemView.columnWidths
    );
  }, [
    activeView,
    allowedViewColumns,
    createSanitizedSettings,
    customViews,
    getSystemView,
  ]);

  const visibleColumnIds = useMemo(
    () => normalizeSitesVisibleColumnIds(draftVisibleColumnIds, allowedViewColumns),
    [allowedViewColumns, draftVisibleColumnIds]
  );

  const hasUnsavedChanges = useMemo(
    () =>
      draftDensity !== activeSettings.density ||
      !areColumnListsEqual(visibleColumnIds, activeSettings.visibleColumnIds) ||
      !areColumnWidthsEqual(draftColumnWidths, activeSettings.columnWidths, allowedViewColumns),
    [activeSettings, allowedViewColumns, draftColumnWidths, draftDensity, visibleColumnIds]
  );

  const setActiveView = useCallback(
    async (viewType: TableViewType, viewKey: string) => {
      await tableViewsService.setActiveView(SITES_TABLE_KEY, { viewType, viewKey });
      const resolved = resolveView(viewType, viewKey, customViews);
      setActiveViewState(resolved.active);
      setDraftVisibleColumnIds(resolved.visibleColumnIds);
      setDraftDensity(resolved.density);
      setDraftColumnWidths(resolved.columnWidths);
    },
    [customViews, resolveView]
  );

  const updateDraftVisibleColumns = useCallback(
    (columnIds: string[]) => {
      setDraftVisibleColumnIds(normalizeSitesVisibleColumnIds(columnIds, allowedViewColumns));
    },
    [allowedViewColumns]
  );

  const updateDraftDensity = useCallback((density: TableViewDensity) => {
    setDraftDensity(density);
  }, []);

  const updateDraftColumnWidth = useCallback(
    (columnId: string, width: number) => {
      const column = allowedViewColumns.find((item) => item.id === columnId);
      const normalizedWidth = normalizeSitesColumnWidth(column, width);
      if (normalizedWidth == null) return;

      setDraftColumnWidths((current) => {
        if (current[columnId] === normalizedWidth) return current;
        return {
          ...current,
          [columnId]: normalizedWidth,
        };
      });
    },
    [allowedViewColumns]
  );

  const updateDraftSettings = useCallback(
    (settings: TableViewSettings) => {
      const sanitized = sanitizeSettings(settings, allowedViewColumns);
      setDraftVisibleColumnIds(sanitized.visibleColumnIds);
      setDraftDensity(sanitized.density);
      setDraftColumnWidths(sanitized.columnWidths);
    },
    [allowedViewColumns]
  );

  const resetDraftToActive = useCallback(() => {
    setDraftVisibleColumnIds(activeSettings.visibleColumnIds);
    setDraftDensity(activeSettings.density);
    setDraftColumnWidths(activeSettings.columnWidths);
  }, [activeSettings]);

  const createCustomView = useCallback(
    async (name: string, settingsOverride?: TableViewSettings) => {
      const settings = settingsOverride
        ? sanitizeSettings(settingsOverride, allowedViewColumns)
        : createSanitizedSettings(visibleColumnIds, draftDensity, draftColumnWidths);
      const created = await tableViewsService.createCustomView(SITES_TABLE_KEY, {
        name,
        settings,
      });
      const nextCustomViews = [...customViews, created].sort((a, b) =>
        a.name.localeCompare(b.name)
      );
      setCustomViews(nextCustomViews);
      await tableViewsService.setActiveView(SITES_TABLE_KEY, {
        viewType: 'custom',
        viewKey: created.id,
      });
      setActiveViewState({ type: 'custom', key: created.id, name: created.name });
      setDraftVisibleColumnIds(settings.visibleColumnIds);
      setDraftDensity(settings.density);
      setDraftColumnWidths(settings.columnWidths);
      return created;
    },
    [
      allowedViewColumns,
      createSanitizedSettings,
      customViews,
      draftColumnWidths,
      draftDensity,
      visibleColumnIds,
    ]
  );

  const updateCustomView = useCallback(
    async (id: string, name?: string, settings?: TableViewSettings) => {
      const sanitizedSettings = settings
        ? sanitizeSettings(settings, allowedViewColumns)
        : undefined;
      const updated = await tableViewsService.updateCustomView(SITES_TABLE_KEY, id, {
        name,
        settings: sanitizedSettings,
      });
      setCustomViews((views) =>
        views
          .map((view) => (view.id === id ? updated : view))
          .sort((a, b) => a.name.localeCompare(b.name))
      );
      if (activeView.type === 'custom' && activeView.key === id) {
        const sanitized = sanitizeSettings(updated.settings, allowedViewColumns);
        setActiveViewState({ type: 'custom', key: updated.id, name: updated.name });
        if (sanitizedSettings) {
          setDraftVisibleColumnIds(sanitized.visibleColumnIds);
          setDraftDensity(sanitized.density);
          setDraftColumnWidths(sanitized.columnWidths);
        }
      }
      return updated;
    },
    [activeView, allowedViewColumns]
  );

  const deleteCustomView = useCallback(
    async (id: string) => {
      await tableViewsService.deleteCustomView(SITES_TABLE_KEY, id);
      setCustomViews((views) => views.filter((view) => view.id !== id));
      if (activeView.type === 'custom' && activeView.key === id) {
        const fallback = getSystemView('default');
        const fallbackSettings = createSanitizedSettings(
          fallback.visibleColumnIds,
          fallback.density
        );
        setActiveViewState({ type: 'system', key: fallback.key, name: fallback.name });
        setDraftVisibleColumnIds(fallbackSettings.visibleColumnIds);
        setDraftDensity(fallbackSettings.density);
        setDraftColumnWidths(fallbackSettings.columnWidths);
      }
    },
    [activeView, createSanitizedSettings, getSystemView]
  );

  return {
    loading,
    loadError,
    activeView,
    systemViews: effectiveSystemViews,
    customViews,
    allowedViewColumns,
    activeSettings,
    visibleColumnIds,
    density: draftDensity,
    columnWidths: draftColumnWidths,
    columnVisibilityModel,
    setActiveView,
    updateDraftVisibleColumns,
    updateDraftDensity,
    updateDraftColumnWidth,
    updateDraftSettings,
    resetDraftToActive,
    createCustomView,
    updateCustomView,
    deleteCustomView,
    hasUnsavedChanges,
  };
}

function sanitizeSettings(
  settings: TableViewSettings | null | undefined,
  allowedViewColumns: SitesColumnMetadata[]
): TableViewSettings {
  const visibleColumnIds = Array.isArray(settings?.visibleColumnIds)
    ? settings.visibleColumnIds.filter(
        (columnId): columnId is string => typeof columnId === 'string'
      )
    : [];

  const density =
    settings?.density === 'compact' ||
    settings?.density === 'standard' ||
    settings?.density === 'comfortable'
      ? settings.density
      : 'standard';

  const allowedColumnById = new Map(allowedViewColumns.map((column) => [column.id, column]));
  const columnWidths: Record<string, number> = {};

  if (settings?.columnWidths && typeof settings.columnWidths === 'object') {
    for (const [columnId, width] of Object.entries(settings.columnWidths)) {
      const normalizedWidth = normalizeSitesColumnWidth(allowedColumnById.get(columnId), width);
      if (normalizedWidth != null) {
        columnWidths[columnId] = normalizedWidth;
      }
    }
  }

  const normalized = createSitesViewSettings(
    normalizeSitesVisibleColumnIds(visibleColumnIds, allowedViewColumns),
    density,
    columnWidths
  );

  return {
    ...normalized,
    columnWidths: Object.fromEntries(
      allowedViewColumns.map((column) => [column.id, normalized.columnWidths[column.id]])
    ),
  };
}

function areColumnListsEqual(left: string[], right: string[]): boolean {
  if (left.length !== right.length) return false;
  return left.every((columnId, index) => columnId === right[index]);
}

export function getColumnLabel(column: SitesColumnMetadata): string {
  return column.label;
}

function areColumnWidthsEqual(
  left: Record<string, number>,
  right: Record<string, number>,
  allowedViewColumns: SitesColumnMetadata[]
): boolean {
  return allowedViewColumns.every((column) => left[column.id] === right[column.id]);
}
