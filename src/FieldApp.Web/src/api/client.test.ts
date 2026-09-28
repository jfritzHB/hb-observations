import { describe, expect, it, vi } from 'vitest';
import { ApiError, apiGet, devPersonaHeader } from './client';

describe('apiGet', () => {
  it('sends the development persona header when a persona is chosen', async () => {
    const fetchMock = vi.fn(() =>
      Promise.resolve(new Response('[]', { headers: { 'Content-Type': 'application/json' } })),
    );
    vi.stubGlobal('fetch', fetchMock);

    await apiGet('/projects', { persona: 'superintendent' });

    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe('/api/v1/projects');
    expect(new Headers(init.headers).get(devPersonaHeader)).toBe('superintendent');
  });

  it('omits the persona header when none is chosen', async () => {
    const fetchMock = vi.fn(() =>
      Promise.resolve(new Response('{}', { headers: { 'Content-Type': 'application/json' } })),
    );
    vi.stubGlobal('fetch', fetchMock);

    await apiGet('/me', { persona: null });

    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(new Headers(init.headers).has(devPersonaHeader)).toBe(false);
  });

  it('raises ApiError carrying the Problem Details', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve(
          new Response(JSON.stringify({ status: 404, title: 'Project not found.', correlationId: 'c-1' }), {
            status: 404,
            headers: { 'Content-Type': 'application/problem+json' },
          }),
        ),
      ),
    );

    const error = await apiGet('/projects/x', { persona: 'superintendent' }).catch(
      (caught: unknown) => caught,
    );

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(404);
    expect((error as ApiError).problem?.correlationId).toBe('c-1');
    expect((error as ApiError).message).toBe('Project not found.');
  });
});
