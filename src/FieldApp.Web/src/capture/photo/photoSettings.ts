/**
 * Client photo normalization (docs/02 and docs/06):
 * - Long edge capped at 2560px. A 12 MP phone photo (4032 x 3024) becomes 2560 x 1920 (~4.9 MP): cracks, fasteners,
 *   finish defects and labels stay legible at normal working distances, while files drop to roughly 0.8-2.5 MB so
 *   uploads finish on weak site connections. Smaller images are never upscaled.
 * - JPEG quality 0.86: visually lossless for inspection; lower settings smear fine texture such as drywall tape
 *   edges and hairline cracks.
 * - Orientation is applied from EXIF before encoding, and re-encoding writes no EXIF at all, so GPS/location and
 *   device metadata are stripped by default. Capture time is recorded separately by the app.
 */
export const photoSettings = {
  maxLongEdge: 2560,
  jpegQuality: 0.86,
  outputType: 'image/jpeg',
} as const;

export interface ProcessedPhoto {
  blob: Blob;
  mediaType: 'image/jpeg';
  width: number;
  height: number;
  byteLength: number;
  sha256: string;
}

/** Target size that fits within the long-edge limit, preserving aspect ratio and never upscaling. */
export function fitWithin(width: number, height: number, maxLongEdge: number = photoSettings.maxLongEdge) {
  const scale = Math.min(1, maxLongEdge / Math.max(width, height));
  return { width: Math.max(1, Math.round(width * scale)), height: Math.max(1, Math.round(height * scale)) };
}
