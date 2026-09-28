import { useId, useRef, useState, type ChangeEvent } from 'react';
import { CameraIcon } from '../components/icons';
import { PhotoPreviewSheet } from './PhotoPreviewSheet';
import type { ProcessedPhoto } from './photo/photoSettings';
import { processPhoto } from './photo/processPhoto';

interface PhotoCaptureDockProps {
  /** Location, trade and type are set. */
  ready: boolean;
  /** The user accepted the previewed photo. */
  onPhotoAccepted: (photo: ProcessedPhoto) => Promise<void>;
}

type Phase =
  { kind: 'idle' } | { kind: 'processing' } | { kind: 'error'; message: string } | { kind: 'saving' };

/**
 * Take photo (device camera through the browser's capture input) and Choose existing photo (file picker fallback,
 * also used when camera permission is denied or on desktop). The photo is normalized in a worker, previewed, and
 * only committed on "Use photo". No native camera APIs are used.
 */
export function PhotoCaptureDock({ ready, onPhotoAccepted }: PhotoCaptureDockProps) {
  const cameraInput = useRef<HTMLInputElement>(null);
  const libraryInput = useRef<HTMLInputElement>(null);
  const [phase, setPhase] = useState<Phase>({ kind: 'idle' });
  const [preview, setPreview] = useState<ProcessedPhoto | null>(null);
  const noteId = useId();

  const handleFile = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = ''; // Allow choosing the same file again.
    if (!file) {
      return; // Cancelled, or camera permission denied: nothing changes.
    }

    setPhase({ kind: 'processing' });
    processPhoto(file).then(
      (photo) => {
        setPreview(photo);
        setPhase({ kind: 'idle' });
      },
      () => {
        setPhase({
          kind: 'error',
          message: 'That photo could not be read. Try again or choose a different photo.',
        });
      },
    );
  };

  const busy = phase.kind === 'processing' || phase.kind === 'saving';
  const note = !ready
    ? 'Set where, trade and type'
    : phase.kind === 'processing'
      ? 'Preparing photo…'
      : phase.kind === 'saving'
        ? 'Saving on this device…'
        : phase.kind === 'error'
          ? phase.message
          : null;

  return (
    <div className="capture-dock">
      <button
        type="button"
        className={`camera-button${ready ? ' camera-button--ready' : ''}`}
        aria-disabled={!ready || busy}
        aria-describedby={note ? noteId : undefined}
        onClick={() => {
          if (ready && !busy) cameraInput.current?.click();
        }}
      >
        <CameraIcon size={28} />
        Take photo
      </button>
      {ready ? (
        <button
          type="button"
          className="text-button text-button--small dock-secondary"
          disabled={busy}
          onClick={() => libraryInput.current?.click()}
        >
          Choose existing photo
        </button>
      ) : null}
      {note ? (
        <p
          id={noteId}
          className={`camera-note${phase.kind === 'error' ? ' camera-note--error' : ''}`}
          role="status"
        >
          {note}
        </p>
      ) : null}

      <input
        ref={cameraInput}
        className="visually-hidden"
        type="file"
        accept="image/*"
        capture="environment"
        tabIndex={-1}
        aria-hidden="true"
        data-testid="camera-input"
        onChange={handleFile}
      />
      <input
        ref={libraryInput}
        className="visually-hidden"
        type="file"
        accept="image/jpeg,image/png,image/webp,image/heic,image/heif"
        tabIndex={-1}
        aria-hidden="true"
        data-testid="library-input"
        onChange={handleFile}
      />

      <PhotoPreviewSheet
        photo={preview}
        error={phase.kind === 'error' ? phase.message : null}
        onUse={() => {
          if (!preview) return;
          const accepted = preview;
          setPreview(null);
          setPhase({ kind: 'saving' });
          onPhotoAccepted(accepted).catch(() => {
            setPreview(accepted);
            setPhase({ kind: 'error', message: 'The photo could not be saved on this device. Try again.' });
          });
        }}
        onRetake={() => {
          setPreview(null);
          cameraInput.current?.click();
        }}
        onChooseDifferent={() => {
          setPreview(null);
          libraryInput.current?.click();
        }}
        onDiscard={() => {
          setPreview(null);
        }}
      />
    </div>
  );
}
