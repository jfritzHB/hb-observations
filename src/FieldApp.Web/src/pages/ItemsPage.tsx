import { Link } from 'react-router';
import { readLastProjectId } from '../capture/recent';
import { PlusIcon } from '../components/icons';
import { Notice } from '../components/StatusMessage';
import { useCurrentUser } from '../session/session';

export function ItemsPage() {
  const user = useCurrentUser();
  const lastProjectId = readLastProjectId(user.id);

  return (
    <main className="page page--with-dock">
      <h1>Items</h1>
      <Notice>
        <p>Observations and punch-list items will be listed here once item capture is available.</p>
      </Notice>
      {lastProjectId ? (
        <div className="dock">
          <Link className="button button--primary button--large" to={`/projects/${lastProjectId}/new-item`}>
            <PlusIcon />
            New Item
          </Link>
        </div>
      ) : (
        <p>
          <Link to="/projects">Choose a project</Link> to start a new item.
        </p>
      )}
    </main>
  );
}
