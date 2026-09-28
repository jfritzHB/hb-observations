import { useCallback, useEffect, useState } from 'react';
import { useSession } from '../session/session';
import { apiGet } from './client';

export type Loadable<T> =
  { state: 'loading' } | { state: 'ready'; data: T } | { state: 'error'; error: Error };

interface Entry<T> {
  key: string;
  result: Loadable<T>;
}

/** GETs an API path as the current persona; refetches when the path, persona or retry count changes. */
export function useApiGet<T>(path: string | null): { result: Loadable<T>; reload: () => void } {
  const { persona } = useSession();
  const [attempt, setAttempt] = useState(0);
  const [entry, setEntry] = useState<Entry<T> | null>(null);
  const key = `${path ?? ''}#${persona ?? ''}#${String(attempt)}`;

  useEffect(() => {
    if (path === null) {
      return;
    }

    const controller = new AbortController();
    apiGet<T>(path, { persona, signal: controller.signal }).then(
      (data) => {
        setEntry({ key, result: { state: 'ready', data } });
      },
      (error: unknown) => {
        if (!controller.signal.aborted) {
          setEntry({
            key,
            result: { state: 'error', error: error instanceof Error ? error : new Error('Request failed.') },
          });
        }
      },
    );

    return () => {
      controller.abort();
    };
  }, [key, path, persona]);

  const reload = useCallback(() => {
    setAttempt((value) => value + 1);
  }, []);

  return { result: entry?.key === key ? entry.result : { state: 'loading' }, reload };
}
