import { normalizePhoto } from './normalizePhoto';
import type { ProcessedPhoto } from './photoSettings';

type WorkerReply = { ok: true; photo: ProcessedPhoto } | { ok: false; message: string };

/**
 * Normalizes a captured or chosen photo, preferring a Web Worker (spec: compress off the main thread). Falls back to
 * the main thread where workers cannot decode images (for example no OffscreenCanvas).
 */
export async function processPhoto(file: Blob): Promise<ProcessedPhoto> {
  if (typeof Worker !== 'undefined' && typeof OffscreenCanvas !== 'undefined') {
    try {
      return await inWorker(file);
    } catch {
      // Fall through to the main thread.
    }
  }

  return normalizePhoto(file);
}

function inWorker(file: Blob): Promise<ProcessedPhoto> {
  return new Promise((resolve, reject) => {
    const worker = new Worker(new URL('./photoWorker.ts', import.meta.url), { type: 'module' });
    worker.onmessage = (event: MessageEvent<WorkerReply>) => {
      worker.terminate();
      if (event.data.ok) {
        resolve(event.data.photo);
      } else {
        reject(new Error(event.data.message));
      }
    };
    worker.onerror = (event) => {
      worker.terminate();
      reject(new Error(event.message || 'The photo could not be processed.'));
    };
    worker.postMessage(file);
  });
}
