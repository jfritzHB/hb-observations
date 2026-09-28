import { vi } from 'vitest';
import { newCaptureDraft, type CaptureDraft } from '../capture/drafts/draftModel';
import type { DraftTransport } from '../capture/drafts/draftTransport';

/** A small synthetic "photo" (JPEG signature plus filler). Never a real photo. */
export function syntheticPhotoBytes(): ArrayBuffer {
  return new Uint8Array([0xff, 0xd8, 0xff, 0xe0, 1, 2, 3, 4, 5, 6, 7, 8]).buffer;
}

export function testDraft(overrides: Partial<CaptureDraft> = {}): CaptureDraft {
  return {
    ...newCaptureDraft({
      clientDraftId: 'draft-1',
      userId: 'u-super',
      projectId: 'p-001',
      projectName: 'Mos Eisley Municipal Center',
      areaId: 'a-A2-201',
      areaPath: 'Building A / Level 2 / Office 201',
      locationDetail: 'North wall',
      tradeId: 'dry',
      tradeName: 'Drywall',
      responsibleCompanyName: 'Dune Sea Drywall Co.',
      itemType: 'PunchList',
      photo: {
        data: syntheticPhotoBytes(),
        mediaType: 'image/jpeg',
        width: 2560,
        height: 1920,
        byteLength: 12,
        sha256: 'abc',
        capturedAt: '2026-04-01T12:00:00.000Z',
      },
    }),
    ...overrides,
  };
}

/** A controllable fake server for the draft sync runner. */
export function fakeTransport(): DraftTransport & { calls: Record<string, number> } {
  const calls: Record<string, number> = {
    createDraft: 0,
    reserveUpload: 0,
    upload: 0,
    finalize: 0,
    finalizedPrimaryPhoto: 0,
  };
  return {
    calls,
    createDraft: vi.fn(() => {
      calls.createDraft = (calls.createDraft ?? 0) + 1;
      return Promise.resolve({ itemId: 'item-1' });
    }),
    reserveUpload: vi.fn(() => {
      calls.reserveUpload = (calls.reserveUpload ?? 0) + 1;
      return Promise.resolve({
        photoId: 'photo-1',
        uploadUrl: '/api/v1/items/item-1/photos/photo-1/content',
      });
    }),
    upload: vi.fn((_draft, _url, _persona, onProgress: (fraction: number) => void) => {
      calls.upload = (calls.upload ?? 0) + 1;
      onProgress(0.5);
      onProgress(1);
      return Promise.resolve();
    }),
    finalize: vi.fn(() => {
      calls.finalize = (calls.finalize ?? 0) + 1;
      return Promise.resolve({
        thumbnailUrl: '/api/v1/items/item-1/photos/photo-1/content?variant=thumbnail',
      });
    }),
    finalizedPrimaryPhoto: vi.fn(() => {
      calls.finalizedPrimaryPhoto = (calls.finalizedPrimaryPhoto ?? 0) + 1;
      return Promise.resolve(null);
    }),
  };
}
