import { useEffect, useMemo } from 'react';
import { Sheet } from '../components/Sheet';
import type { ProcessedPhoto } from './photo/photoSettings';

interface PhotoPreviewSheetProps {
  photo: ProcessedPhoto | null;
  onUse: () => void;
  onRetake: () => void;
  onChooseDifferent: () => void;
  onDiscard: () => void;
  error?: string | null;
}

/** Preview before committing: use the photo, retake it, choose another, or discard it. */
export function PhotoPreviewSheet({
  photo,
  onUse,
  onRetake,
  onChooseDifferent,
  onDiscard,
  error,
}: PhotoPreviewSheetProps) {
  const url = useMemo(() => (photo ? URL.createObjectURL(photo.blob) : null), [photo]);

  useEffect(
    () => () => {
      if (url) URL.revokeObjectURL(url);
    },
    [url],
  );

  return (
    <Sheet open={photo !== null} title="Photo preview" onClose={onDiscard}>
      {url && photo ? (
        <div className="preview">
          {error ? <p role="alert">{error}</p> : null}
          <img
            className="preview__image"
            src={url}
            alt="Preview of the captured photo"
            width={photo.width}
            height={photo.height}
          />
          <p className="field-hint">
            Not sent yet. Tap Use photo to protect it on this device before sending.
          </p>
          <div className="preview__actions">
            <button type="button" className="button button--primary button--large" onClick={onUse}>
              Use photo
            </button>
            <div className="preview__secondary">
              <button type="button" className="button button--secondary" onClick={onRetake}>
                Retake
              </button>
              <button type="button" className="button button--secondary" onClick={onChooseDifferent}>
                Choose different
              </button>
            </div>
            <button type="button" className="text-button" onClick={onDiscard}>
              Remove photo
            </button>
          </div>
        </div>
      ) : null}
    </Sheet>
  );
}
