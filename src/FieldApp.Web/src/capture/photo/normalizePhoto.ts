import { sha256Base64 } from '../../lib/sha256';
import { fitWithin, photoSettings, type ProcessedPhoto } from './photoSettings';

/**
 * Decodes (applying EXIF orientation), downsizes and re-encodes a photo as JPEG without metadata. Runs in the photo
 * Web Worker when OffscreenCanvas is available, otherwise on the main thread.
 */
export async function normalizePhoto(source: Blob): Promise<ProcessedPhoto> {
  const bitmap = await createImageBitmap(source, { imageOrientation: 'from-image' });
  try {
    const { width, height } = fitWithin(bitmap.width, bitmap.height);
    const blob = await encode(bitmap, width, height);
    const buffer = await blob.arrayBuffer();

    return {
      blob,
      mediaType: photoSettings.outputType,
      width,
      height,
      byteLength: buffer.byteLength,
      sha256: await sha256Base64(buffer),
    };
  } finally {
    bitmap.close();
  }
}

async function encode(bitmap: ImageBitmap, width: number, height: number): Promise<Blob> {
  if (typeof OffscreenCanvas !== 'undefined') {
    const canvas = new OffscreenCanvas(width, height);
    const context = canvas.getContext('2d');
    if (!context) {
      throw new Error('Canvas is not available.');
    }
    context.imageSmoothingQuality = 'high';
    context.drawImage(bitmap, 0, 0, width, height);
    return canvas.convertToBlob({ type: photoSettings.outputType, quality: photoSettings.jpegQuality });
  }

  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext('2d');
  if (!context) {
    throw new Error('Canvas is not available.');
  }
  context.imageSmoothingQuality = 'high';
  context.drawImage(bitmap, 0, 0, width, height);
  return new Promise<Blob>((resolve, reject) => {
    canvas.toBlob(
      (blob) => {
        if (blob) {
          resolve(blob);
        } else {
          reject(new Error('The photo could not be encoded.'));
        }
      },
      photoSettings.outputType,
      photoSettings.jpegQuality,
    );
  });
}
