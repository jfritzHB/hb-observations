import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { fakeTransport, testDraft } from '../../test/drafts';
import { applyDraftEvent, InvalidDraftTransition, nextStep } from './draftModel';
import { describeDraftStatus } from './draftStatus';
import { deleteDraft, getDraft, listDrafts, saveDraft } from './draftStore';
import { syncDraft } from './draftSync';

describe('draft model', () => {
  it('derives the next server step from recorded facts', () => {
    const draft = testDraft();
    expect(nextStep(draft)).toBe('createDraft');
    expect(nextStep({ ...draft, serverItemId: 'i' })).toBe('reserveUpload');
    expect(nextStep({ ...draft, serverItemId: 'i', serverPhotoId: 'p', serverUploadUrl: '/u' })).toBe(
      'upload',
    );
    expect(
      nextStep({
        ...draft,
        serverItemId: 'i',
        serverPhotoId: 'p',
        serverUploadUrl: '/u',
        photoUploaded: true,
      }),
    ).toBe('finalize');
    expect(
      nextStep({
        ...draft,
        serverItemId: 'i',
        serverPhotoId: 'p',
        serverUploadUrl: '/u',
        photoUploaded: true,
        photoFinalized: true,
      }),
    ).toBeNull();
  });

  it('walks the explicit states to ReadyForDescription', () => {
    let draft = testDraft();
    expect(draft.status).toBe('PhotoCaptured');
    draft = applyDraftEvent(draft, { type: 'stepStarted', step: 'createDraft' });
    expect(draft.status).toBe('CreatingServerDraft');
    draft = applyDraftEvent(draft, { type: 'draftCreated', itemId: 'item-1' });
    expect(draft.status).toBe('Uploading');
    draft = applyDraftEvent(draft, { type: 'stepStarted', step: 'reserveUpload' });
    draft = applyDraftEvent(draft, { type: 'uploadReserved', photoId: 'photo-1', uploadUrl: '/u' });
    draft = applyDraftEvent(draft, { type: 'stepStarted', step: 'upload' });
    draft = applyDraftEvent(draft, { type: 'uploaded' });
    expect(draft.status).toBe('Finalizing');
    draft = applyDraftEvent(draft, { type: 'stepStarted', step: 'finalize' });
    draft = applyDraftEvent(draft, { type: 'finalized', thumbnailUrl: '/t' });
    expect(draft.status).toBe('ReadyForDescription');
  });

  it('rejects impossible transitions', () => {
    const draft = testDraft();
    expect(() => applyDraftEvent(draft, { type: 'uploaded' })).toThrow(InvalidDraftTransition);
    expect(() => applyDraftEvent(draft, { type: 'stepStarted', step: 'finalize' })).toThrow(
      InvalidDraftTransition,
    );
    const ready = { ...draft, status: 'ReadyForDescription' as const, photoFinalized: true };
    expect(() =>
      applyDraftEvent(ready, { type: 'failed', step: 'upload', kind: 'offline', message: 'x' }),
    ).toThrow(InvalidDraftTransition);
  });

  it('never describes a local-only draft as saved to the server', () => {
    const draft = testDraft({
      status: 'Failed',
      failure: { step: 'createDraft', kind: 'offline', message: 'x', at: 'now' },
    });
    expect(describeDraftStatus(draft, false).where).toBe('On this device only');
    expect(describeDraftStatus({ ...draft, status: 'Uploading' }, true).where).toBe('On this device only');
    expect(describeDraftStatus({ ...draft, status: 'ReadyForDescription' }, true).where).toBe(
      'Saved to server',
    );
  });

  it('caps the description at 2000 characters', () => {
    const draft = applyDraftEvent(testDraft(), { type: 'descriptionChanged', text: 'x'.repeat(2500) });
    expect(draft.descriptionEnglish).toHaveLength(2000);
  });
});

describe('draft store', () => {
  it('persists drafts, including the photo bytes, per user and project', async () => {
    await saveDraft(testDraft());
    await saveDraft(testDraft({ clientDraftId: 'draft-2', projectId: 'p-002' }));
    await saveDraft(testDraft({ clientDraftId: 'draft-3', userId: 'someone-else' }));

    const restored = await getDraft('draft-1');
    expect(restored?.locationDetail).toBe('North wall');
    expect(new Uint8Array(restored?.photo.data ?? new ArrayBuffer(0))[0]).toBe(0xff);
    expect((await listDrafts('u-super')).map((d) => d.clientDraftId).sort()).toEqual(['draft-1', 'draft-2']);
    expect((await listDrafts('u-super', 'p-002')).map((d) => d.clientDraftId)).toEqual(['draft-2']);

    await deleteDraft('draft-1');
    expect(await getDraft('draft-1')).toBeUndefined();
  });
});

describe('draft sync', () => {
  it('creates, reserves, uploads and finalizes, saving each fact locally', async () => {
    await saveDraft(testDraft());
    const transport = fakeTransport();
    const progress: number[] = [];

    const result = await syncDraft('draft-1', {
      persona: 'superintendent',
      transport,
      onProgress: (p) => progress.push(p),
    });

    expect(result?.status).toBe('ReadyForDescription');
    const stored = await getDraft('draft-1');
    expect(stored).toMatchObject({
      serverItemId: 'item-1',
      serverPhotoId: 'photo-1',
      photoUploaded: true,
      photoFinalized: true,
    });
    expect(progress).toContain(1);
  });

  it('keeps the photo locally on a connection failure and resumes without recreating the server item', async () => {
    await saveDraft(testDraft());
    const transport = fakeTransport();
    vi.mocked(transport.upload).mockRejectedValueOnce(new TypeError('Network error'));

    const failed = await syncDraft('draft-1', { persona: null, transport });
    expect(failed?.status).toBe('Failed');
    expect(failed?.failure).toMatchObject({ step: 'upload', kind: 'offline' });
    expect((await getDraft('draft-1'))?.photo.byteLength).toBe(12);

    const resumed = await syncDraft('draft-1', { persona: null, transport });
    expect(resumed?.status).toBe('ReadyForDescription');
    expect(transport.calls.createDraft).toBe(1);
    expect(transport.calls.reserveUpload).toBe(1);
  });

  it('adopts an already finalized primary photo instead of uploading again', async () => {
    await saveDraft(testDraft({ serverItemId: 'item-1', status: 'Failed' }));
    const transport = fakeTransport();
    vi.mocked(transport.reserveUpload).mockRejectedValueOnce(
      new ApiError(409, { title: 'already has its primary photo' }),
    );
    vi.mocked(transport.finalizedPrimaryPhoto).mockResolvedValueOnce({
      photoId: 'photo-1',
      thumbnailUrl: '/t',
    });

    const result = await syncDraft('draft-1', { persona: null, transport });

    expect(result?.status).toBe('ReadyForDescription');
    expect(transport.calls.upload).toBe(0);
    expect(result?.serverPhotoId).toBe('photo-1');
  });

  it('re-uploads when the server did not receive the bytes', async () => {
    await saveDraft(testDraft());
    const transport = fakeTransport();
    vi.mocked(transport.finalize).mockRejectedValueOnce(new ApiError(409, { title: 'not uploaded yet' }));

    const result = await syncDraft('draft-1', { persona: null, transport });

    expect(result?.status).toBe('ReadyForDescription');
    expect(transport.calls.upload).toBe(2);
  });

  it('asks for a different photo when the server rejects it', async () => {
    await saveDraft(testDraft());
    const transport = fakeTransport();
    vi.mocked(transport.reserveUpload).mockRejectedValue(new ApiError(415, { title: 'unsupported' }));

    const result = await syncDraft('draft-1', { persona: null, transport });

    expect(result?.failure).toMatchObject({ step: 'reserveUpload', kind: 'rejected' });
  });

  it('never leaves a draft looking in progress after repeated verification failures', async () => {
    await saveDraft(testDraft());
    const transport = fakeTransport();
    vi.mocked(transport.finalize).mockRejectedValue(new ApiError(422, { title: 'checksum' }));

    const result = await syncDraft('draft-1', { persona: null, transport });

    expect(result?.status).toBe('Failed');
    expect(result?.failure?.kind).toBe('rejected');
  });

  it('runs one sync per draft at a time', async () => {
    await saveDraft(testDraft());
    const transport = fakeTransport();

    await Promise.all([
      syncDraft('draft-1', { persona: null, transport }),
      syncDraft('draft-1', { persona: null, transport }),
    ]);

    expect(transport.calls.createDraft).toBe(1);
  });
});
