import { Link } from 'react-router';
import type { CaptureDraft } from '../capture/drafts/draftModel';
import { draftListLabel } from '../capture/drafts/draftStatus';
import { useLocalDrafts, usePhotoUrl } from '../capture/drafts/useDrafts';
import { readLastProjectId } from '../capture/recent';
import { itemTypeLabels } from '../capture/selection';
import { ChevronRightIcon, PlusIcon } from '../components/icons';
import { Notice } from '../components/StatusMessage';
import { useCurrentUser } from '../session/session';

export function ItemsPage() {
  const user = useCurrentUser();
  const lastProjectId = readLastProjectId(user.id);
  const drafts = useLocalDrafts(user.id);

  return (
    <main className="page page--with-dock">
      <h1>Items</h1>

      {drafts && drafts.length > 0 ? (
        <section aria-labelledby="drafts-heading" className="section">
          <h2 id="drafts-heading">Captures on this device</h2>
          <ul className="card-list" aria-label="Captures on this device">
            {drafts.map((draft) => (
              <DraftCard key={draft.clientDraftId} draft={draft} />
            ))}
          </ul>
        </section>
      ) : null}

      <Notice>
        <p>Saved observations and punch-list items will be listed here once saving items is available.</p>
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

function DraftCard({ draft }: { draft: CaptureDraft }) {
  const photoUrl = usePhotoUrl(draft.photo);
  const location = [draft.areaPath, draft.locationDetail].filter(Boolean).join(' · ') || 'No location';

  return (
    <li>
      <Link className="card card--link" to={`/projects/${draft.projectId}/drafts/${draft.clientDraftId}`}>
        {photoUrl ? <img className="card__thumb" src={photoUrl} alt="" width={64} height={64} /> : null}
        <span className="card__body">
          <span className="card__title">
            {itemTypeLabels[draft.itemType]} · {draft.tradeName}
          </span>
          <span className="card__meta">{location}</span>
          <span className={`badge ${draft.status === 'ReadyForDescription' ? 'badge--ok' : 'badge--warn'}`}>
            {draftListLabel(draft)}
          </span>
        </span>
        <ChevronRightIcon />
      </Link>
    </li>
  );
}
