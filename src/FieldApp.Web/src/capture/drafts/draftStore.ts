import { openDB, type DBSchema, type IDBPDatabase } from 'idb';
import type { CaptureDraft } from './draftModel';

// Durable local capture drafts (IndexedDB). This is connection-interruption protection for the current device:
// not an offline replica of project data and not a multi-device sync queue.

interface DraftDb extends DBSchema {
  drafts: {
    key: string;
    value: CaptureDraft;
    indexes: { byUser: string };
  };
}

const databaseName = 'fieldapp-capture';
const databaseVersion = 1;

let database: Promise<IDBPDatabase<DraftDb>> | null = null;

function db(): Promise<IDBPDatabase<DraftDb>> {
  database ??= openDB<DraftDb>(databaseName, databaseVersion, {
    upgrade(upgradeDb) {
      const store = upgradeDb.createObjectStore('drafts', { keyPath: 'clientDraftId' });
      store.createIndex('byUser', 'userId');
    },
  });
  return database;
}

type Listener = (draft: CaptureDraft | null, clientDraftId: string) => void;
const listeners = new Set<Listener>();

/** Notified whenever a draft is saved or deleted in this tab. */
export function subscribeToDrafts(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export async function saveDraft(draft: CaptureDraft): Promise<void> {
  await (await db()).put('drafts', draft);
  listeners.forEach((listener) => {
    listener(draft, draft.clientDraftId);
  });
}

export async function getDraft(clientDraftId: string): Promise<CaptureDraft | undefined> {
  return (await db()).get('drafts', clientDraftId);
}

/** Read and update in one transaction so sync progress cannot overwrite description edits. */
export async function updateDraft(
  clientDraftId: string,
  change: (draft: CaptureDraft) => CaptureDraft,
): Promise<CaptureDraft | undefined> {
  const transaction = (await db()).transaction('drafts', 'readwrite');
  const current = await transaction.store.get(clientDraftId);
  if (!current) return undefined;
  const updated = change(current);
  await transaction.store.put(updated);
  await transaction.done;
  listeners.forEach((listener) => {
    listener(updated, clientDraftId);
  });
  return updated;
}

/** The user's drafts on this device, newest first, optionally for one project. */
export async function listDrafts(userId: string, projectId?: string): Promise<CaptureDraft[]> {
  const drafts = await (await db()).getAllFromIndex('drafts', 'byUser', userId);
  return drafts
    .filter((draft) => projectId === undefined || draft.projectId === projectId)
    .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
}

export async function deleteDraft(clientDraftId: string): Promise<void> {
  await (await db()).delete('drafts', clientDraftId);
  listeners.forEach((listener) => {
    listener(null, clientDraftId);
  });
}

/** Test helper: forget the cached connection (used after resetting fake-indexeddb). */
export function resetDraftStoreConnection(): void {
  database = null;
}
