import { normalizePhoto } from './normalizePhoto';

// Minimal view of the dedicated worker scope (the app's TypeScript project uses the DOM lib).
const scope = self as unknown as {
  onmessage: ((event: MessageEvent<Blob>) => void) | null;
  postMessage: (message: unknown) => void;
};

// Expensive decode/resize/encode/hash work runs off the UI thread.
scope.onmessage = (event) => {
  normalizePhoto(event.data).then(
    (photo) => {
      scope.postMessage({ ok: true, photo });
    },
    (error: unknown) => {
      scope.postMessage({
        ok: false,
        message: error instanceof Error ? error.message : 'The photo could not be processed.',
      });
    },
  );
};
