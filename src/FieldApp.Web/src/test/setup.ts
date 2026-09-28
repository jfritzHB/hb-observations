import 'fake-indexeddb/auto';
import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { IDBFactory } from 'fake-indexeddb';
import { afterEach, beforeEach, vi } from 'vitest';
import { resetDraftStoreConnection } from '../capture/drafts/draftStore';

// jsdom does not implement modal dialogs; emulate just enough for <Sheet>. Real browsers are covered by Playwright.
HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
  this.open = true;
};
HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
  this.open = false;
  this.dispatchEvent(new Event('close'));
};

// jsdom has no object URLs for blobs.
URL.createObjectURL = () => 'blob:test-photo';
URL.revokeObjectURL = () => undefined;

beforeEach(() => {
  // A fresh, empty IndexedDB for every test.
  globalThis.indexedDB = new IDBFactory();
  resetDraftStoreConnection();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  window.localStorage.clear();
});
