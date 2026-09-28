import { ApiError, apiSend, devPersonaHeader } from '../../api/client';
import type { ProblemDetails } from '../../api/types';
import type { CaptureDraft } from './draftModel';

interface ItemResponse {
  id: string;
  responsibleCompany: { name: string };
  tradeName: string;
  areaPath: string | null;
  photos: { id: string; status: string; isPrimary: boolean; thumbnailUrl: string | null }[];
}

interface ReservationResponse {
  photoId: string;
  uploadUrl: string;
}

interface PhotoResponse {
  thumbnailUrl: string | null;
}

/** The server calls a capture draft needs. Swappable in tests. */
export interface DraftTransport {
  createDraft: (
    draft: CaptureDraft,
    persona: string | null,
  ) => Promise<{
    itemId: string;
    responsibleCompanyName?: string;
    tradeName?: string;
    areaPath?: string | null;
  }>;
  reserveUpload: (
    draft: CaptureDraft,
    persona: string | null,
  ) => Promise<{ photoId: string; uploadUrl: string }>;
  upload: (
    draft: CaptureDraft,
    uploadUrl: string,
    persona: string | null,
    onProgress: (fraction: number) => void,
  ) => Promise<void>;
  finalize: (draft: CaptureDraft, persona: string | null) => Promise<{ thumbnailUrl: string | null }>;
  /** The server's finalized primary photo for the item, if any. */
  finalizedPrimaryPhoto: (
    itemId: string,
    persona: string | null,
  ) => Promise<{ photoId: string; thumbnailUrl: string | null } | null>;
}

export const httpDraftTransport: DraftTransport = {
  async createDraft(draft, persona) {
    const item = await apiSend<ItemResponse>('POST', `/projects/${draft.projectId}/items`, {
      persona,
      // The clientDraftId doubles as the Idempotency-Key: every retry of this draft is the same creation attempt.
      headers: { 'Idempotency-Key': draft.clientDraftId },
      body: {
        clientDraftId: draft.clientDraftId,
        type: draft.itemType,
        areaId: draft.areaId,
        locationDetail: draft.locationDetail.trim() || null,
        tradeId: draft.tradeId,
      },
    });
    return {
      itemId: item.id,
      responsibleCompanyName: item.responsibleCompany.name,
      tradeName: item.tradeName,
      areaPath: item.areaPath,
    };
  },

  async reserveUpload(draft, persona) {
    const reservation = await apiSend<ReservationResponse>(
      'POST',
      `/items/${draft.serverItemId ?? ''}/photos/uploads`,
      {
        persona,
        body: {
          fileName: 'capture.jpg',
          contentType: draft.photo.mediaType,
          byteLength: draft.photo.byteLength,
          sha256: draft.photo.sha256,
          capturedAt: draft.photo.capturedAt,
        },
      },
    );
    return { photoId: reservation.photoId, uploadUrl: reservation.uploadUrl };
  },

  upload(draft, uploadUrl, persona, onProgress) {
    return new Promise<void>((resolve, reject) => {
      const request = new XMLHttpRequest();
      request.open('PUT', uploadUrl);
      request.timeout = 120_000;
      request.setRequestHeader('Content-Type', draft.photo.mediaType);
      if (persona) {
        request.setRequestHeader(devPersonaHeader, persona);
      }
      request.upload.onprogress = (event) => {
        if (event.lengthComputable && event.total > 0) {
          onProgress(event.loaded / event.total);
        }
      };
      request.onload = () => {
        if (request.status >= 200 && request.status < 300) {
          onProgress(1);
          resolve();
        } else {
          reject(new ApiError(request.status, parseProblem(request.responseText)));
        }
      };
      request.onerror = () => {
        reject(new TypeError('Network error during upload.'));
      };
      request.ontimeout = request.onerror;
      request.send(new Blob([draft.photo.data], { type: draft.photo.mediaType }));
    });
  },

  async finalize(draft, persona) {
    const photo = await apiSend<PhotoResponse>(
      'POST',
      `/items/${draft.serverItemId ?? ''}/photos/${draft.serverPhotoId ?? ''}/finalize`,
      { persona },
    );
    return { thumbnailUrl: photo.thumbnailUrl };
  },

  async finalizedPrimaryPhoto(itemId, persona) {
    const item = await apiSend<ItemResponse>('GET', `/items/${itemId}`, { persona });
    const primary = item.photos.find((photo) => photo.isPrimary && photo.status === 'Finalized');
    return primary ? { photoId: primary.id, thumbnailUrl: primary.thumbnailUrl } : null;
  },
};

function parseProblem(text: string): ProblemDetails | null {
  try {
    return JSON.parse(text) as ProblemDetails;
  } catch {
    return null;
  }
}
