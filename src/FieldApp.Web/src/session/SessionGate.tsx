import type { ReactNode } from 'react';
import { ErrorMessage, Loading } from '../components/StatusMessage';
import { PersonaList } from './PersonaList';
import { useSession } from './session';

/** Renders the app only for a signed-in application user; otherwise explains what is needed. */
export function SessionGate({ children }: { children: ReactNode }) {
  const { status, devPersonas, choosePersona, retry } = useSession();

  switch (status.kind) {
    case 'loading':
      return (
        <main className="page page--centered">
          <Loading label="Signing in…" />
        </main>
      );
    case 'signed-in':
      return children;
    case 'choose-persona':
      return (
        <main className="page">
          <h1>Choose a development persona</h1>
          <p className="lede">
            Local development only. There are no passwords: pick a synthetic user to see the app with their
            project access.
          </p>
          {devPersonas ? (
            <PersonaList personas={devPersonas} current={null} onChoose={choosePersona} />
          ) : null}
        </main>
      );
    case 'no-access':
      return (
        <main className="page">
          <h1>No access yet</h1>
          <p className="lede">
            You are signed in, but your account has not been given access to this application. Ask an
            administrator to add you to a project.
          </p>
          {devPersonas ? (
            <button
              type="button"
              className="button button--secondary"
              onClick={() => {
                choosePersona(null);
              }}
            >
              Choose another persona
            </button>
          ) : null}
        </main>
      );
    case 'signed-out':
      return (
        <main className="page">
          <h1>Sign-in unavailable</h1>
          <p className="lede">Sign-in is not configured for this environment yet.</p>
        </main>
      );
    case 'error':
      return (
        <main className="page">
          <h1>Can’t reach the server</h1>
          <ErrorMessage message="Check your connection and try again." onRetry={retry} />
        </main>
      );
  }
}
