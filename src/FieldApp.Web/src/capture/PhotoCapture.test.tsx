import { screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { installFakeApi } from '../test/fakeApi';
import { fakeTransport, testDraft } from '../test/drafts';
import { renderApp } from '../test/renderApp';
import { getDraft, listDrafts, saveDraft } from './drafts/draftStore';
import * as draftStore from './drafts/draftStore';
import type { DraftTransport } from './drafts/draftTransport';

// Photo processing needs real image decoding (covered by Playwright); here it returns a synthetic result.
vi.mock('./photo/processPhoto', () => ({
  processPhoto: vi.fn((file: Blob) =>
    file.size === 0
      ? Promise.reject(new Error('unreadable'))
      : Promise.resolve({
          blob: new Blob([new Uint8Array([0xff, 0xd8, 0xff, 0xe0])], { type: 'image/jpeg' }),
          mediaType: 'image/jpeg',
          width: 2560,
          height: 1920,
          byteLength: 4,
          sha256: 'synthetic-hash',
        }),
  ),
}));

// The server side of the draft is replaced by a controllable fake.
const transportHolder: { current: DraftTransport } = { current: fakeTransport() };
vi.mock('./drafts/draftTransport', () => ({
  get httpDraftTransport() {
    return transportHolder.current;
  },
}));

const syntheticFile = () =>
  new File([new Uint8Array([0xff, 0xd8, 0xff, 0xe0, 1, 2])], 'synthetic.jpg', { type: 'image/jpeg' });

async function readyCapture() {
  installFakeApi();
  const view = renderApp('/projects/p-001/new-item');
  const where = await screen.findByRole('combobox', { name: 'Where?' });
  await view.user.type(where, 'Unit 214');
  await view.user.click(screen.getByRole('button', { name: 'Drywall' }));
  await view.user.click(screen.getByRole('radio', { name: 'Punch List' }));
  return view;
}

describe('photo capture', () => {
  beforeEach(() => {
    transportHolder.current = fakeTransport();
  });

  it('opens the camera input from Take photo and the file picker from Choose existing photo', async () => {
    const { user } = await readyCapture();
    const camera = screen.getByTestId('camera-input');
    const library = screen.getByTestId('library-input');
    const cameraClick = vi.spyOn(camera, 'click');
    const libraryClick = vi.spyOn(library, 'click');

    expect(camera).toHaveAttribute('capture', 'environment');
    expect(library).not.toHaveAttribute('capture');

    await user.click(screen.getByRole('button', { name: /Take photo/ }));
    await user.click(screen.getByRole('button', { name: 'Choose existing photo' }));

    expect(cameraClick).toHaveBeenCalledOnce();
    expect(libraryClick).toHaveBeenCalledOnce();
  });

  it('previews the photo and lets the user retake, choose different, or discard it before continuing', async () => {
    const { user } = await readyCapture();
    const cameraClick = vi.spyOn(screen.getByTestId('camera-input'), 'click');

    await user.upload(screen.getByTestId('camera-input'), syntheticFile());
    const preview = await screen.findByRole('dialog', { name: 'Photo preview' });
    expect(within(preview).getByRole('img', { name: 'Preview of the captured photo' })).toBeInTheDocument();
    expect(within(preview).getByText(/Not sent yet/)).toBeInTheDocument();

    await user.click(within(preview).getByRole('button', { name: 'Retake' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(cameraClick).toHaveBeenCalled();

    await user.upload(screen.getByTestId('library-input'), syntheticFile());
    await user.click(
      within(await screen.findByRole('dialog', { name: 'Photo preview' })).getByRole('button', {
        name: /Close/,
      }),
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(await listDrafts('u-super')).toEqual([]);
  });

  it('explains when a photo cannot be read and keeps the capture context', async () => {
    const { user } = await readyCapture();

    await user.upload(screen.getByTestId('camera-input'), new File([], 'empty.jpg', { type: 'image/jpeg' }));

    expect(await screen.findByText(/That photo could not be read/)).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Where?' })).toHaveValue('Unit 214');
  });

  it('retains the preview and allows retry if local storage rejects the save', async () => {
    const { user } = await readyCapture();
    const save = vi.spyOn(draftStore, 'saveDraft').mockRejectedValueOnce(new Error('quota'));
    await user.upload(screen.getByTestId('library-input'), syntheticFile());
    await user.click(
      within(await screen.findByRole('dialog', { name: 'Photo preview' })).getByRole('button', {
        name: 'Use photo',
      }),
    );
    const preview = await screen.findByRole('dialog', { name: 'Photo preview' });
    expect(within(preview).getByRole('alert')).toHaveTextContent('could not be saved');
    expect(within(preview).getByRole('img')).toBeInTheDocument();
    expect(transportHolder.current.createDraft).not.toHaveBeenCalled();
    save.mockRestore();
    await user.click(within(preview).getByRole('button', { name: 'Use photo' }));
    expect(await screen.findByText('Saved to the server as a draft')).toBeInTheDocument();
  });

  it('protects the accepted photo on the device, then sends it and opens the description', async () => {
    const { user, router } = await readyCapture();

    await user.upload(screen.getByTestId('camera-input'), syntheticFile());
    await user.click(
      within(await screen.findByRole('dialog', { name: 'Photo preview' })).getByRole('button', {
        name: 'Use photo',
      }),
    );

    expect(await screen.findByText('Saved to the server as a draft')).toBeInTheDocument();
    expect(router.state.location.pathname).toMatch(/^\/projects\/p-001\/drafts\/[0-9a-f-]{36}$/);
    const [draft] = await listDrafts('u-super');
    expect(draft).toMatchObject({
      status: 'ReadyForDescription',
      locationDetail: 'Unit 214',
      tradeName: 'Drywall',
      responsibleCompanyName: 'Dune Sea Drywall Co.',
      itemType: 'PunchList',
      serverItemId: 'item-1',
    });
    expect(screen.getByRole('textbox', { name: 'Description (English)' })).toBeInTheDocument();
  });
});

describe('draft screen', () => {
  beforeEach(() => {
    transportHolder.current = fakeTransport();
  });

  it("conceals another user's local capture and never sends it", async () => {
    installFakeApi();
    await saveDraft(testDraft({ userId: 'someone-else' }));
    renderApp('/projects/p-001/drafts/draft-1');
    expect(await screen.findByRole('heading', { name: 'Capture not found' })).toBeInTheDocument();
    expect(transportHolder.current.createDraft).not.toHaveBeenCalled();
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it('preserves description edits made while the upload is pending', async () => {
    installFakeApi();
    const transport = fakeTransport();
    let finishUpload: (() => void) | undefined;
    vi.mocked(transport.upload).mockImplementation(
      () =>
        new Promise<void>((resolve) => {
          finishUpload = resolve;
        }),
    );
    transportHolder.current = transport;
    await saveDraft(testDraft());
    const { user } = renderApp('/projects/p-001/drafts/draft-1');
    await user.type(await screen.findByRole('textbox', { name: 'Description (English)' }), 'Keep my words.');
    await waitFor(async () => {
      expect((await getDraft('draft-1'))?.descriptionEnglish).toBe('Keep my words.');
    });
    finishUpload?.();
    expect(await screen.findByText('Saved to the server as a draft')).toBeInTheDocument();
    expect((await getDraft('draft-1'))?.descriptionEnglish).toBe('Keep my words.');
  });

  it('shows the failed boundary, keeps the photo, and retries to one server item', async () => {
    installFakeApi();
    const transport = fakeTransport();
    vi.mocked(transport.upload).mockRejectedValueOnce(new TypeError('Network error'));
    transportHolder.current = transport;
    await saveDraft(testDraft());
    const { user } = renderApp('/projects/p-001/drafts/draft-1');

    expect(await screen.findByText('Not sent yet: no connection')).toBeInTheDocument();
    expect(screen.getByText('On this device only')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: /Photo for Drywall/ })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Saved to the server as a draft')).toBeInTheDocument();
    expect(transport.calls.createDraft).toBe(1);
  });

  it('resumes an unfinished capture restored after a refresh', async () => {
    installFakeApi();
    await saveDraft(testDraft({ serverItemId: 'item-1', status: 'Uploading' }));

    renderApp('/projects/p-001/drafts/draft-1');

    expect(await screen.findByText('Saved to the server as a draft')).toBeInTheDocument();
    expect(vi.mocked(transportHolder.current.createDraft)).not.toHaveBeenCalled();
  });

  it('offers a different photo when the server rejects it', async () => {
    installFakeApi();
    const transport = fakeTransport();
    const { ApiError } = await import('../api/client');
    vi.mocked(transport.finalize).mockRejectedValue(new ApiError(415, { title: 'unsupported' }));
    transportHolder.current = transport;
    await saveDraft(testDraft());

    renderApp('/projects/p-001/drafts/draft-1');

    expect(await screen.findByRole('button', { name: 'Choose a different photo' })).toBeInTheDocument();
  });

  it('keeps the typed English description on the device across a reload', async () => {
    installFakeApi();
    await saveDraft(
      testDraft({
        serverItemId: 'item-1',
        serverPhotoId: 'photo-1',
        serverUploadUrl: '/u',
        photoUploaded: true,
        photoFinalized: true,
        status: 'ReadyForDescription',
      }),
    );
    const first = renderApp('/projects/p-001/drafts/draft-1');

    const editor = await screen.findByRole('textbox', { name: 'Description (English)' });
    await first.user.type(editor, 'Patch drywall damage at entry door.');
    await waitFor(async () => {
      expect((await getDraft('draft-1'))?.descriptionEnglish).toBe('Patch drywall damage at entry door.');
    });
    expect(screen.getByText(/Saved on this device/)).toBeInTheDocument();
    first.unmount();

    renderApp('/projects/p-001/drafts/draft-1');

    expect(await screen.findByRole('textbox', { name: 'Description (English)' })).toHaveValue(
      'Patch drywall damage at entry door.',
    );
    expect(screen.getByRole('button', { name: 'Generate description with AI' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
  });

  it('says so when the capture is not on this device', async () => {
    installFakeApi();

    renderApp('/projects/p-001/drafts/missing');

    expect(await screen.findByRole('heading', { name: 'Capture not found' })).toBeInTheDocument();
  });
});
