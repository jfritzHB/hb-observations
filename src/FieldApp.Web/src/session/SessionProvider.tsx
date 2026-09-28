import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { apiGet, isApiError } from '../api/client';
import type { CurrentUser, DevPersona } from '../api/types';
import { readStorage, writeStorage } from '../lib/storage';
import { SessionContext, type Session, type SessionStatus } from './session';

export const personaStorageKey = 'fieldapp.devPersona';

interface Resolved {
  key: string;
  status: SessionStatus;
  devPersonas: DevPersona[] | null;
}

async function resolveSession(persona: string | null, signal: AbortSignal): Promise<Omit<Resolved, 'key'>> {
  let devPersonas: DevPersona[] | null = null;
  try {
    devPersonas = await apiGet<DevPersona[]>('/dev/personas', { persona: null, signal });
  } catch (error) {
    if (!isApiError(error, 404)) {
      throw error;
    }
  }

  if (devPersonas && !persona) {
    return { status: { kind: 'choose-persona' }, devPersonas };
  }

  try {
    const user = await apiGet<CurrentUser>('/me', { persona, signal });
    return { status: { kind: 'signed-in', user }, devPersonas };
  } catch (error) {
    if (isApiError(error, 401)) {
      return { status: devPersonas ? { kind: 'choose-persona' } : { kind: 'signed-out' }, devPersonas };
    }

    if (isApiError(error, 403)) {
      return { status: { kind: 'no-access' }, devPersonas };
    }

    throw error;
  }
}

export function SessionProvider({ children }: { children: ReactNode }) {
  const [persona, setPersona] = useState<string | null>(() => readStorage(personaStorageKey));
  const [attempt, setAttempt] = useState(0);
  const [resolved, setResolved] = useState<Resolved | null>(null);
  const key = `${persona ?? ''}#${String(attempt)}`;

  useEffect(() => {
    const controller = new AbortController();
    resolveSession(persona, controller.signal).then(
      (result) => {
        setResolved({ key, ...result });
      },
      (error: unknown) => {
        if (!controller.signal.aborted) {
          const message = error instanceof Error ? error.message : 'Unable to reach the server.';
          setResolved({ key, status: { kind: 'error', message }, devPersonas: null });
        }
      },
    );

    return () => {
      controller.abort();
    };
  }, [key, persona]);

  const choosePersona = useCallback((next: string | null) => {
    writeStorage(personaStorageKey, next);
    setPersona(next);
  }, []);

  const retry = useCallback(() => {
    setAttempt((value) => value + 1);
  }, []);

  const session = useMemo<Session>(() => {
    const current = resolved?.key === key ? resolved : null;
    return {
      status: current?.status ?? { kind: 'loading' },
      devPersonas: current?.devPersonas ?? resolved?.devPersonas ?? null,
      persona,
      choosePersona,
      retry,
    };
  }, [resolved, key, persona, choosePersona, retry]);

  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}
