import { useCallback, useEffect, useRef, useState } from 'react';
import type { EntityChangeHistoryItem } from '../types/changeHistory.types';

interface ChangeHistoryState {
  readonly items: EntityChangeHistoryItem[] | null;
  readonly loading: boolean;
  readonly error: string | null;
  readonly load: () => Promise<void>;
}

export function useChangeHistory(
  entityKey: string | null,
  loadHistory: () => Promise<EntityChangeHistoryItem[]>
): ChangeHistoryState {
  const [items, setItems] = useState<EntityChangeHistoryItem[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const requestIdRef = useRef(0);
  const loadingRef = useRef(false);
  const loadedEntityKeyRef = useRef<string | null>(null);
  const loadHistoryRef = useRef(loadHistory);
  loadHistoryRef.current = loadHistory;

  useEffect(() => {
    requestIdRef.current += 1;
    loadingRef.current = false;
    loadedEntityKeyRef.current = null;
    setItems(null);
    setLoading(false);
    setError(null);
  }, [entityKey]);

  const load = useCallback(async () => {
    if (
      !entityKey ||
      loadingRef.current ||
      loadedEntityKeyRef.current === entityKey
    ) {
      return;
    }

    const requestId = ++requestIdRef.current;
    loadingRef.current = true;
    setLoading(true);
    setError(null);

    try {
      const result = await loadHistoryRef.current();
      if (requestId !== requestIdRef.current) return;

      setItems(result);
      loadedEntityKeyRef.current = entityKey;
    } catch (err) {
      if (requestId !== requestIdRef.current) return;
      setError(err instanceof Error ? err.message : 'Failed to load history');
    } finally {
      if (requestId === requestIdRef.current) {
        loadingRef.current = false;
        setLoading(false);
      }
    }
  }, [entityKey]);

  return { items, loading, error, load };
}
