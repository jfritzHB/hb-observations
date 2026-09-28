import { ApiError } from '../../api/client';
import {
  applyDraftEvent,
  nextStep,
  type CaptureDraft,
  type DraftEvent,
  type FailureKind,
  type SyncStep,
} from './draftModel';
import { getDraft, updateDraft } from './draftStore';
import { httpDraftTransport, type DraftTransport } from './draftTransport';

export interface SyncOptions {
  persona: string | null;
  transport?: DraftTransport;
  onProgress?: (fraction: number) => void;
}

const inFlight = new Map<string, Promise<CaptureDraft | undefined>>();

/**
 * Pushes a local draft to the server from wherever it left off: create the server draft, reserve the upload,
 * upload, finalize. Every step is idempotent on the server, and progress is saved to IndexedDB after each step, so
 * a refresh, crash or connectivity drop resumes safely without duplicating the item or its photo.
 * On failure the draft is saved as Failed (the photo stays on the device) and the promise resolves normally.
 */
export function syncDraft(clientDraftId: string, options: SyncOptions): Promise<CaptureDraft | undefined> {
  const running = inFlight.get(clientDraftId);
  if (running) {
    return running;
  }

  const work = run(clientDraftId, options).finally(() => {
    inFlight.delete(clientDraftId);
  });
  inFlight.set(clientDraftId, work);
  return work;
}

async function run(
  clientDraftId: string,
  { persona, transport = httpDraftTransport, onProgress }: SyncOptions,
) {
  const stored = await getDraft(clientDraftId);
  if (!stored) {
    return undefined;
  }

  let draft: CaptureDraft = stored;
  const apply = async (event: DraftEvent) => {
    const updated = await updateDraft(clientDraftId, (current) => applyDraftEvent(current, event));
    if (!updated) throw new Error('Capture removed from this device.');
    draft = updated;
  };

  // Bounded so an inconsistent server can never cause an endless loop.
  for (let attempt = 0; attempt < 8; attempt++) {
    const step = nextStep(draft);
    if (!step) {
      return draft;
    }

    await apply({ type: 'stepStarted', step });

    try {
      switch (step) {
        case 'createDraft': {
          const created = await transport.createDraft(draft, persona);
          await apply({ type: 'draftCreated', ...created });
          break;
        }
        case 'reserveUpload': {
          const { photoId, uploadUrl } = await transport.reserveUpload(draft, persona);
          await apply({ type: 'uploadReserved', photoId, uploadUrl });
          break;
        }
        case 'upload':
          onProgress?.(0);
          await transport.upload(draft, draft.serverUploadUrl ?? '', persona, (fraction) =>
            onProgress?.(fraction),
          );
          await apply({ type: 'uploaded' });
          break;
        case 'finalize': {
          const { thumbnailUrl } = await transport.finalize(draft, persona);
          await apply({ type: 'finalized', thumbnailUrl });
          break;
        }
      }
    } catch (error) {
      const recovery = await recover(step, error, draft, persona, transport);
      if (recovery) {
        await apply(recovery);
        continue;
      }

      const { kind, message } = classify(step, error);
      await apply({ type: 'failed', step, kind, message });
      return draft;
    }
  }

  // Still unfinished after the bounded attempts: never leave the draft looking "in progress".
  const unfinished = nextStep(draft);
  if (unfinished) {
    await apply({
      type: 'failed',
      step: unfinished,
      kind: 'rejected',
      message: 'The server could not verify this photo. Choose a different photo.',
    });
  }

  return draft;
}

/** Server answers that mean "adjust and carry on" rather than failure. */
async function recover(
  step: SyncStep,
  error: unknown,
  draft: CaptureDraft,
  persona: string | null,
  transport: DraftTransport,
): Promise<DraftEvent | null> {
  if (!(error instanceof ApiError)) {
    return null;
  }

  // The primary photo is already finalized (an earlier response was lost): adopt the server's result.
  if (step === 'reserveUpload' && error.status === 409 && draft.serverItemId) {
    try {
      const primary = await transport.finalizedPrimaryPhoto(draft.serverItemId, persona);
      return primary
        ? { type: 'finalized', thumbnailUrl: primary.thumbnailUrl, photoId: primary.photoId }
        : null;
    } catch {
      return null;
    }
  }

  // Reservation expired or superseded: reserve again (same bytes return the same photo).
  if (step === 'upload' && error.status === 409) {
    return { type: 'reservationLost' };
  }

  // The bytes never arrived or did not match: send them again (bounded by the attempt limit).
  if (step === 'finalize' && (error.status === 409 || error.status === 422)) {
    return { type: 'uploadLost' };
  }

  return null;
}

function classify(step: SyncStep, error: unknown): { kind: FailureKind; message: string } {
  if (!(error instanceof ApiError)) {
    return { kind: 'offline', message: 'No connection to the server.' };
  }

  if (error.status === 401) {
    return { kind: 'auth', message: 'Your sign-in has expired. Sign in again, then retry.' };
  }

  if (
    error.status >= 500 ||
    error.status === 408 ||
    error.status === 429 ||
    (error.status === 409 && step === 'reserveUpload')
  ) {
    return { kind: 'server', message: 'The server could not finish this step.' };
  }

  if (error.status === 413 || error.status === 415 || (step === 'finalize' && error.status === 422)) {
    return { kind: 'rejected', message: 'The server could not accept this photo. Choose a different photo.' };
  }

  return { kind: 'rejected', message: error.problem?.title ?? 'The server rejected this capture.' };
}
