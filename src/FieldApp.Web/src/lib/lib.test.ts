import { describe, expect, it } from 'vitest';
import { fitWithin } from '../capture/photo/photoSettings';
import { newId } from './ids';
import { sha256, sha256Base64, toBase64 } from './sha256';

describe('sha256', () => {
  it('matches the FIPS 180-4 test vectors (pure fallback and WebCrypto)', async () => {
    const abc = new TextEncoder().encode('abc');
    expect(toBase64(sha256(abc))).toBe('ungWv48Bz+pBQUDeXa4iI7ADYaOWF3qctBD/YfIAFa0=');
    expect(toBase64(sha256(new Uint8Array(0)))).toBe('47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=');
    const long = new TextEncoder().encode('abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq');
    expect(toBase64(sha256(long))).toBe('JI1qYdIGOLjlwCaTDD5gOaM85Flk/yFn9uzt1BnbBsE=');
    expect(await sha256Base64(abc.buffer)).toBe('ungWv48Bz+pBQUDeXa4iI7ADYaOWF3qctBD/YfIAFa0=');
  });
});

describe('photo sizing', () => {
  it('fits the long edge within 2560px and never upscales', () => {
    expect(fitWithin(4032, 3024)).toEqual({ width: 2560, height: 1920 });
    expect(fitWithin(3024, 4032)).toEqual({ width: 1920, height: 2560 });
    expect(fitWithin(1600, 1200)).toEqual({ width: 1600, height: 1200 });
  });
});

describe('newId', () => {
  it('produces RFC 4122 version 4 identifiers', () => {
    expect(newId()).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  });
});
