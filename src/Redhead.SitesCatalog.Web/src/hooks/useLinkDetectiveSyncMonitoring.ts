import { useCallback, useEffect, useRef, useState } from 'react';
import { linkDetectiveSyncService } from '../services/linkDetectiveSync.service';
import type { LinkDetectiveSyncStatus } from '../types/linkDetectiveSync.types';
import {
  SYNC_IDLE_POLL_INTERVAL_MS,
  SYNC_POLL_INTERVAL_MS,
} from '../utils/linkDetectiveSyncDisplay';

// loadData must be memoized by the caller's page and filter values.
export function useLinkDetectiveSyncMonitoring<T>(enabled: boolean, loadData: () => Promise<T>) {
  const [status, setStatus] = useState<LinkDetectiveSyncStatus | null>(null);
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshKey, setRefreshKey] = useState(0);
  const lastStatus = useRef<LinkDetectiveSyncStatus | null>(null);
  const refresh = useCallback(() => setRefreshKey((value) => value + 1), []);

  useEffect(() => {
    if (!enabled) return;
    let active = true;
    let timer: number;

    const load = async () => {
      setLoading(true);
      const results = await Promise.allSettled([linkDetectiveSyncService.getStatus(), loadData()]);
      if (!active) return;

      const [statusResult, dataResult] = results;
      if (statusResult.status === 'fulfilled') {
        lastStatus.current = statusResult.value;
        setStatus(statusResult.value);
      }
      if (dataResult.status === 'fulfilled') setData(dataResult.value);
      const errors = results.flatMap((result) =>
        result.status === 'rejected'
          ? [
              result.reason instanceof Error
                ? result.reason.message
                : 'Failed to load LinkDetective monitoring.',
            ]
          : []
      );
      setError(errors.join(' ') || null);
      setLoading(false);

      // Schedule only after both requests settle. Failed refreshes retain saved data
      // and use the last known configuration to decide whether to keep polling.
      const currentStatus = lastStatus.current;
      if (currentStatus?.enabled && currentStatus.configured) {
        timer = window.setTimeout(
          () => void load(),
          currentStatus.activeRun ? SYNC_POLL_INTERVAL_MS : SYNC_IDLE_POLL_INTERVAL_MS
        );
      }
    };

    timer = window.setTimeout(() => void load(), 0);
    return () => {
      active = false;
      window.clearTimeout(timer);
    };
  }, [enabled, loadData, refreshKey]);

  return { status, data, error, loading, refresh };
}
