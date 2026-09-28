import { useEffect, useMemo, useState } from 'react';
import type { CaptureDraft, LocalPhoto } from './draftModel';
import { getDraft, listDrafts, subscribeToDrafts } from './draftStore';

/** One local draft, kept current as it is saved (for example by the sync runner). */
export function useDraft(clientDraftId: string): { loaded: boolean; draft: CaptureDraft | undefined } {
  const [state, setState] = useState<{ id: string; loaded: boolean; draft: CaptureDraft | undefined }>({
    id: clientDraftId,
    loaded: false,
    draft: undefined,
  });

  useEffect(() => {
    let cancelled = false;
    getDraft(clientDraftId).then(
      (draft) => {
        if (!cancelled) setState({ id: clientDraftId, loaded: true, draft });
      },
      () => {
        if (!cancelled) setState({ id: clientDraftId, loaded: true, draft: undefined });
      },
    );
    const unsubscribe = subscribeToDrafts((draft, id) => {
      if (id === clientDraftId) setState({ id, loaded: true, draft: draft ?? undefined });
    });
    return () => {
      cancelled = true;
      unsubscribe();
    };
  }, [clientDraftId]);

  return state.id === clientDraftId ? state : { loaded: false, draft: undefined };
}

/** The user's local drafts (optionally for one project), newest first, kept current. */
export function useLocalDrafts(userId: string, projectId?: string): CaptureDraft[] | null {
  const [drafts, setDrafts] = useState<{ key: string; drafts: CaptureDraft[] } | null>(null);
  const key = `${userId}|${projectId ?? ''}`;

  useEffect(() => {
    let cancelled = false;
    const load = () => {
      listDrafts(userId, projectId).then(
        (result) => {
          if (!cancelled) setDrafts({ key, drafts: result });
        },
        () => {
          if (!cancelled) setDrafts({ key, drafts: [] });
        },
      );
    };
    load();
    const unsubscribe = subscribeToDrafts(load);
    return () => {
      cancelled = true;
      unsubscribe();
    };
  }, [key, userId, projectId]);

  return drafts?.key === key ? drafts.drafts : null;
}

/** An object URL for a locally stored photo, revoked when no longer needed. */
export function usePhotoUrl(photo: LocalPhoto | undefined): string | null {
  const url = useMemo(
    () => (photo ? URL.createObjectURL(new Blob([photo.data], { type: photo.mediaType })) : null),
    [photo],
  );

  useEffect(
    () => () => {
      if (url) URL.revokeObjectURL(url);
    },
    [url],
  );

  return url;
}
