import type { ItemType } from '../selection';

/**
 * A capture draft kept on the device (IndexedDB) from the moment a photo is accepted. It is the source of truth for
 * recovery: after a refresh or a connectivity drop the app resumes from the facts recorded here.
 *
 * Lifecycle (persisted `status`):
 *   PhotoCaptured -> CreatingServerDraft -> Uploading -> Finalizing -> ReadyForDescription
 *   any in-flight state -> Failed (photo and context stay on the device) -> retry resumes at the failed step
 * "Editing" (choosing where/trade/type before the photo) is screen state only and is not persisted.
 */
export type DraftStatus =
  'PhotoCaptured' | 'CreatingServerDraft' | 'Uploading' | 'Finalizing' | 'ReadyForDescription' | 'Failed';

export type SyncStep = 'createDraft' | 'reserveUpload' | 'upload' | 'finalize';

/** Why a step failed: `offline` (no connection), `server` (retryable), `rejected` (needs a different photo or input). */
export type FailureKind = 'offline' | 'server' | 'rejected' | 'auth';

export interface DraftFailure {
  step: SyncStep;
  kind: FailureKind;
  message: string;
  at: string;
}

/** Stored as an ArrayBuffer: it structured-clones reliably in every IndexedDB (older Safari mishandled Blobs). */
export interface LocalPhoto {
  data: ArrayBuffer;
  mediaType: 'image/jpeg';
  width: number;
  height: number;
  byteLength: number;
  sha256: string;
  capturedAt: string;
}

export interface CaptureDraft {
  clientDraftId: string;
  userId: string;
  projectId: string;
  projectName: string;
  areaId: string | null;
  areaPath: string | null;
  locationDetail: string;
  tradeId: string;
  tradeName: string;
  responsibleCompanyName: string;
  itemType: ItemType;
  photo: LocalPhoto;
  status: DraftStatus;
  serverItemId: string | null;
  serverPhotoId: string | null;
  /** Relative API upload URL from the reservation (no credentials). */
  serverUploadUrl: string | null;
  photoUploaded: boolean;
  photoFinalized: boolean;
  thumbnailUrl: string | null;
  descriptionEnglish: string;
  failure: DraftFailure | null;
  createdAt: string;
  updatedAt: string;
}

export type DraftEvent =
  | { type: 'stepStarted'; step: SyncStep }
  | {
      type: 'draftCreated';
      itemId: string;
      responsibleCompanyName?: string;
      tradeName?: string;
      areaPath?: string | null;
    }
  | { type: 'uploadReserved'; photoId: string; uploadUrl: string }
  | { type: 'reservationLost' }
  | { type: 'uploaded' }
  | { type: 'uploadLost' }
  | { type: 'finalized'; thumbnailUrl: string | null; photoId?: string }
  | { type: 'failed'; step: SyncStep; kind: FailureKind; message: string }
  | { type: 'photoReplaced'; photo: LocalPhoto }
  | { type: 'descriptionChanged'; text: string };

export const descriptionMaxLength = 2000;

/** The next server step, derived from recorded facts so a resumed draft never repeats completed work. */
export function nextStep(draft: CaptureDraft): SyncStep | null {
  // A finalized primary photo completes the pipeline, even when adopted from the server after a lost response.
  if (draft.photoFinalized) return null;
  if (!draft.serverItemId) return 'createDraft';
  if (!draft.serverPhotoId || !draft.serverUploadUrl) return 'reserveUpload';
  if (!draft.photoUploaded) return 'upload';
  return 'finalize';
}

const statusForStep: Record<SyncStep, DraftStatus> = {
  createDraft: 'CreatingServerDraft',
  reserveUpload: 'Uploading',
  upload: 'Uploading',
  finalize: 'Finalizing',
};

export class InvalidDraftTransition extends Error {}

export function applyDraftEvent(
  draft: CaptureDraft,
  event: DraftEvent,
  now: Date = new Date(),
): CaptureDraft {
  const at = now.toISOString();
  const next = (changes: Partial<CaptureDraft>): CaptureDraft => ({ ...draft, ...changes, updatedAt: at });
  const invalid = () =>
    new InvalidDraftTransition(`Cannot apply ${event.type} to a draft that is ${draft.status}.`);

  switch (event.type) {
    case 'stepStarted':
      if (draft.status === 'ReadyForDescription' || nextStep(draft) !== event.step) throw invalid();
      return next({ status: statusForStep[event.step], failure: null });
    case 'draftCreated':
      if (draft.status !== 'CreatingServerDraft') throw invalid();
      return next({
        serverItemId: event.itemId,
        status: 'Uploading',
        responsibleCompanyName: event.responsibleCompanyName ?? draft.responsibleCompanyName,
        tradeName: event.tradeName ?? draft.tradeName,
        areaPath: event.areaPath === undefined ? draft.areaPath : event.areaPath,
      });
    case 'uploadReserved':
      if (draft.status !== 'Uploading') throw invalid();
      return next({ serverPhotoId: event.photoId, serverUploadUrl: event.uploadUrl });
    case 'reservationLost':
      // The reservation expired or was replaced: reserve again (the server returns the same photo for the same bytes).
      if (draft.status !== 'Uploading') throw invalid();
      return next({ serverPhotoId: null, serverUploadUrl: null, photoUploaded: false });
    case 'uploaded':
      if (draft.status !== 'Uploading' || !draft.serverPhotoId) throw invalid();
      return next({ photoUploaded: true, status: 'Finalizing' });
    case 'uploadLost':
      // The server did not receive (or could not verify) the bytes: upload them again.
      if (draft.status !== 'Finalizing') throw invalid();
      return next({ photoUploaded: false, status: 'Uploading' });
    case 'finalized':
      if (draft.status !== 'Finalizing' && draft.status !== 'Uploading') throw invalid();
      return next({
        photoUploaded: true,
        photoFinalized: true,
        serverPhotoId: event.photoId ?? draft.serverPhotoId,
        thumbnailUrl: event.thumbnailUrl,
        status: 'ReadyForDescription',
      });
    case 'failed':
      if (draft.status === 'ReadyForDescription') throw invalid();
      return next({
        status: 'Failed',
        failure: { step: event.step, kind: event.kind, message: event.message, at },
      });
    case 'photoReplaced':
      if (draft.photoFinalized) throw invalid();
      return next({
        photo: event.photo,
        serverPhotoId: null,
        serverUploadUrl: null,
        photoUploaded: false,
        photoFinalized: false,
        thumbnailUrl: null,
        failure: null,
        status: 'PhotoCaptured',
      });
    case 'descriptionChanged':
      return next({ descriptionEnglish: event.text.slice(0, descriptionMaxLength) });
  }
}

/** True once the server has confirmed the draft item and its primary photo. */
export function isServerConfirmed(draft: CaptureDraft): boolean {
  return draft.status === 'ReadyForDescription';
}

export function newCaptureDraft(
  fields: Omit<
    CaptureDraft,
    | 'status'
    | 'serverItemId'
    | 'serverPhotoId'
    | 'serverUploadUrl'
    | 'photoUploaded'
    | 'photoFinalized'
    | 'thumbnailUrl'
    | 'descriptionEnglish'
    | 'failure'
    | 'createdAt'
    | 'updatedAt'
  >,
  now: Date = new Date(),
): CaptureDraft {
  const at = now.toISOString();
  return {
    ...fields,
    status: 'PhotoCaptured',
    serverItemId: null,
    serverPhotoId: null,
    serverUploadUrl: null,
    photoUploaded: false,
    photoFinalized: false,
    thumbnailUrl: null,
    descriptionEnglish: '',
    failure: null,
    createdAt: at,
    updatedAt: at,
  };
}
