import { createContext, useContext } from 'react';
import type { CurrentUser, DevPersona } from '../api/types';

export type SessionStatus =
  | { kind: 'loading' }
  | { kind: 'signed-in'; user: CurrentUser }
  /** Development persona authentication is available but no valid persona is chosen. */
  | { kind: 'choose-persona' }
  /** No identity provider is available to this client. */
  | { kind: 'signed-out' }
  /** Authenticated, but not linked to an application user. */
  | { kind: 'no-access' }
  | { kind: 'error'; message: string };

export interface Session {
  status: SessionStatus;
  persona: string | null;
  /** Null when development personas are not available (any non-Development environment). */
  devPersonas: DevPersona[] | null;
  choosePersona: (key: string | null) => void;
  retry: () => void;
}

export const SessionContext = createContext<Session | null>(null);

export function useSession(): Session {
  const session = useContext(SessionContext);
  if (!session) {
    throw new Error('useSession must be used within SessionProvider.');
  }

  return session;
}

/** The signed-in user. Only valid beneath the session gate. */
export function useCurrentUser(): CurrentUser {
  const { status } = useSession();
  if (status.kind !== 'signed-in') {
    throw new Error('useCurrentUser requires a signed-in session.');
  }

  return status.user;
}
