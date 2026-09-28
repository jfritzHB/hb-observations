import { useCallback, useEffect, useId, useRef, useState, type ChangeEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { Notice } from '../components/StatusMessage';
import { useCurrentUser, useSession } from '../session/session';
import { applyDraftEvent, descriptionMaxLength, type CaptureDraft } from './drafts/draftModel';
import { describeDraftStatus } from './drafts/draftStatus';
import { deleteDraft, updateDraft } from './drafts/draftStore';
import { syncDraft } from './drafts/draftSync';
import { useDraft, usePhotoUrl } from './drafts/useDrafts';
import { processPhoto } from './photo/processPhoto';
import { itemTypeLabels } from './selection';

function useOnline(): boolean {
  const [online, setOnline] = useState(() => navigator.onLine);
  useEffect(() => {
    const update = () => {
      setOnline(navigator.onLine);
    };
    window.addEventListener('online', update);
    window.addEventListener('offline', update);
    return () => {
      window.removeEventListener('online', update);
      window.removeEventListener('offline', update);
    };
  }, []);
  return online;
}

/**
 * After the photo: shows the capture, sends it to the server (resuming after refresh or a lost connection), and,
 * once the server has the draft and photo, the manual English description. AI and translation come later.
 */
export function DraftPage() {
  const { projectId = '', clientDraftId = '' } = useParams();
  const { loaded, draft } = useDraft(clientDraftId);
  const user = useCurrentUser();

  if (!loaded) {
    return (
      <main className="page">
        <p className="status" role="status">
          Loading capture…
        </p>
      </main>
    );
  }

  if (!draft || draft.userId !== user.id || draft.projectId !== projectId) {
    return (
      <main className="page">
        <h1>Capture not found</h1>
        <p className="lede">This capture is not stored on this device.</p>
        <Link className="button button--secondary" to={`/projects/${projectId}`}>
          Back to project
        </Link>
      </main>
    );
  }

  return <DraftView key={draft.clientDraftId} draft={draft} />;
}

function DraftView({ draft }: { draft: CaptureDraft }) {
  const { persona } = useSession();
  const navigate = useNavigate();
  const online = useOnline();
  const [progress, setProgress] = useState<number | null>(null);
  const [localError, setLocalError] = useState<string | null>(null);
  const photoUrl = usePhotoUrl(draft.photo);
  const replaceInput = useRef<HTMLInputElement>(null);
  const statusId = useId();

  const ready = draft.status === 'ReadyForDescription';
  const needsUserAction =
    draft.status === 'Failed' && (draft.failure?.kind === 'rejected' || draft.failure?.kind === 'auth');

  const send = useCallback(() => {
    setProgress(null);
    setLocalError(null);
    void syncDraft(draft.clientDraftId, { persona, onProgress: setProgress })
      .catch(() => {
        setLocalError('Progress could not be saved on this device. Keep this page open and try again.');
      })
      .finally(() => {
        setProgress(null);
      });
  }, [draft.clientDraftId, persona]);

  // Resume automatically on open (including after a refresh) unless the user must act first.
  const autoStarted = useRef(false);
  useEffect(() => {
    if (!autoStarted.current && !ready && !needsUserAction) {
      autoStarted.current = true;
      send();
    }
  }, [ready, needsUserAction, send]);

  // Retry by itself only when the connection comes back (offline -> online), never in a loop.
  const wasOnline = useRef(online);
  useEffect(() => {
    const cameBack = online && !wasOnline.current;
    wasOnline.current = online;
    if (
      cameBack &&
      draft.status === 'Failed' &&
      (draft.failure?.kind === 'offline' || draft.failure?.kind === 'server')
    ) {
      send();
    }
  }, [online, draft.status, draft.failure?.kind, send]);

  const view = describeDraftStatus(draft, online);
  const location = [draft.areaPath, draft.locationDetail].filter(Boolean).join(' · ') || 'No location';

  const replacePhoto = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    void processPhoto(file)
      .then(async (photo) => {
        const data = await photo.blob.arrayBuffer();
        await updateDraft(draft.clientDraftId, (current) =>
          applyDraftEvent(current, {
            type: 'photoReplaced',
            photo: {
              data,
              mediaType: photo.mediaType,
              width: photo.width,
              height: photo.height,
              byteLength: photo.byteLength,
              sha256: photo.sha256,
              capturedAt: new Date().toISOString(),
            },
          }),
        );
        send();
      })
      .catch(() => {
        setLocalError(
          'The replacement photo could not be prepared or saved. Your previous photo is retained.',
        );
      });
  };

  return (
    <main className="page draft">
      <div className="context-bar">
        <span className="context-bar__project">
          <span className="context-bar__name">{draft.projectName}</span>
        </span>
      </div>

      <h1 className="capture-title">{ready ? 'Describe the item' : 'New item'}</h1>
      {localError ? (
        <p role="alert">
          {localError}{' '}
          <button type="button" className="text-button" onClick={send}>
            Try again
          </button>
        </p>
      ) : null}

      <div className="draft-summary">
        {photoUrl ? (
          <img
            className="draft-photo"
            src={photoUrl}
            alt={`Photo for ${draft.tradeName} at ${location}`}
            width={draft.photo.width}
            height={draft.photo.height}
          />
        ) : null}
        <dl className="draft-context">
          <div>
            <dt className="visually-hidden">Type</dt>
            <dd className="badge badge--type">{itemTypeLabels[draft.itemType]}</dd>
          </div>
          <div>
            <dt className="visually-hidden">Location</dt>
            <dd className="draft-context__primary">{location}</dd>
          </div>
          <div>
            <dt className="visually-hidden">Trade</dt>
            <dd>{draft.tradeName}</dd>
          </div>
          <div>
            <dt className="visually-hidden">Responsible company</dt>
            <dd className="draft-context__muted">Responsible: {draft.responsibleCompanyName}</dd>
          </div>
        </dl>
      </div>

      <section
        id={statusId}
        className={`sync-status sync-status--${view.tone}`}
        aria-label="Capture status"
        data-status={draft.status}
      >
        <p className="sync-status__where">{view.where}</p>
        <p className="sync-status__title" role="status">
          {view.title}
          {draft.status === 'Uploading' && progress !== null ? ` ${String(Math.round(progress * 100))}%` : ''}
        </p>
        {draft.status === 'Uploading' && progress !== null ? (
          <progress
            className="sync-status__progress"
            max={1}
            value={progress}
            aria-label="Photo upload progress"
          />
        ) : null}
        <p className="sync-status__detail">{view.detail}</p>
        {draft.status === 'Failed' ? (
          <div className="sync-status__actions">
            {draft.failure?.kind === 'rejected' ? (
              <button
                type="button"
                className="button button--primary"
                onClick={() => replaceInput.current?.click()}
              >
                Choose a different photo
              </button>
            ) : (
              <button type="button" className="button button--primary" onClick={send}>
                Try again
              </button>
            )}
          </div>
        ) : null}
        <input
          ref={replaceInput}
          className="visually-hidden"
          type="file"
          accept="image/*"
          tabIndex={-1}
          aria-hidden="true"
          onChange={replacePhoto}
        />
      </section>

      <DescriptionEditor draft={draft} />

      <div className="draft-later">
        <button
          type="button"
          className="button button--secondary"
          aria-disabled="true"
          aria-describedby="ai-note"
        >
          Generate description with AI
        </button>
        <p id="ai-note" className="field-hint">
          Coming in a later release. Type your own description; AI is never required.
        </p>
      </div>

      <button
        type="button"
        className="text-button text-button--small"
        onClick={() => {
          if (window.confirm('Discard this capture? The photo will be removed from this device.')) {
            void deleteDraft(draft.clientDraftId).then(() => navigate(`/projects/${draft.projectId}`));
          }
        }}
      >
        Discard this capture
      </button>

      {!online ? (
        <Notice>
          <p>You are offline. Your capture is safe on this device.</p>
        </Notice>
      ) : null}
    </main>
  );
}

/** Manual English description, saved to this device as the user types (the manual path is always available). */
function DescriptionEditor({ draft }: { draft: CaptureDraft }) {
  const [text, setText] = useState(draft.descriptionEnglish);
  const [saved, setSaved] = useState<'idle' | 'pending' | 'saved' | 'error'>('idle');
  const inputId = useId();
  const hintId = useId();
  const revision = useRef(0);
  const writes = useRef(Promise.resolve());

  const persist = useCallback(
    (value: string) => {
      const version = ++revision.current;
      setSaved('pending');
      writes.current = writes.current.then(async () => {
        try {
          const updated = await updateDraft(draft.clientDraftId, (current) =>
            applyDraftEvent(current, { type: 'descriptionChanged', text: value }),
          );
          if (!updated) throw new Error('Capture missing');
          if (version === revision.current) setSaved('saved');
        } catch {
          if (version === revision.current) setSaved('error');
        }
      });
    },
    [draft.clientDraftId],
  );

  // Warn only while a write is pending or failed; never rely on asynchronous page-exit writes.
  useEffect(() => {
    if (saved !== 'pending' && saved !== 'error') return;
    const warn = (event: BeforeUnloadEvent) => {
      event.preventDefault();
    };
    window.addEventListener('beforeunload', warn);
    return () => {
      window.removeEventListener('beforeunload', warn);
    };
  }, [saved]);

  return (
    <div className="description">
      <label className="capture-field__label" htmlFor={inputId}>
        Description (English)
      </label>
      <textarea
        id={inputId}
        className="description__input"
        rows={4}
        maxLength={descriptionMaxLength}
        value={text}
        placeholder="e.g. Patch drywall damage at entry door."
        aria-describedby={hintId}
        onChange={(event) => {
          const value = event.target.value;
          setText(value);
          persist(value);
        }}
      />
      <p id={hintId} className="field-hint" aria-live="polite">
        {saved === 'saved' ? 'Saved on this device. ' : ''}
        {text.length}/{descriptionMaxLength}
      </p>
      {saved === 'error' ? (
        <p role="alert">
          Description could not be saved on this device. Keep this page open.{' '}
          <button
            type="button"
            className="text-button"
            onClick={() => {
              persist(text);
            }}
          >
            Retry saving description
          </button>
        </p>
      ) : null}
    </div>
  );
}
