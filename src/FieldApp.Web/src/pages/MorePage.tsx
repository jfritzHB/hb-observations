import { useNavigate } from 'react-router';
import { PersonaList } from '../session/PersonaList';
import { useCurrentUser, useSession } from '../session/session';

export function MorePage() {
  const user = useCurrentUser();
  const { devPersonas, persona, choosePersona } = useSession();
  const navigate = useNavigate();

  return (
    <main className="page">
      <h1>More</h1>

      <section aria-labelledby="account-heading" className="section">
        <h2 id="account-heading">Signed in as</h2>
        <p className="card__title">{user.displayName}</p>
        {user.email ? <p className="card__meta">{user.email}</p> : null}
      </section>

      {devPersonas ? (
        <section aria-labelledby="persona-heading" className="section">
          <h2 id="persona-heading">Development persona</h2>
          <p className="card__meta">
            Local development only. Switch to see the app as another synthetic user.
          </p>
          <PersonaList
            personas={devPersonas}
            current={persona}
            onChoose={(key) => {
              choosePersona(key);
              void navigate('/projects');
            }}
          />
        </section>
      ) : null}
    </main>
  );
}
