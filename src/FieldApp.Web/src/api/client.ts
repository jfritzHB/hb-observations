import type { ProblemDetails } from './types';

const apiBase = '/api/v1';

/** Header carrying the synthetic development persona. Replaced by an Entra bearer token in a later slice. */
export const devPersonaHeader = 'X-Dev-Persona';

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | null;

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.title ?? `Request failed with status ${String(status)}.`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }
}

export interface RequestOptions {
  persona: string | null;
  signal?: AbortSignal;
}

export async function apiGet<T>(path: string, { persona, signal }: RequestOptions): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (persona) {
    headers[devPersonaHeader] = persona;
  }

  const response = await fetch(`${apiBase}${path}`, { headers, signal });
  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }

  return (await response.json()) as T;
}

export interface SendOptions extends RequestOptions {
  body?: unknown;
  headers?: Record<string, string>;
}

/** Sends a JSON request to the API. Paths may be relative to /api/v1 or absolute (/api/v1/...). */
export async function apiSend<T>(method: string, path: string, options: SendOptions): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json', ...options.headers };
  if (options.persona) {
    headers[devPersonaHeader] = options.persona;
  }
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(path.startsWith(apiBase) ? path : `${apiBase}${path}`, {
    method,
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    signal: options.signal,
  });
  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }

  const contentType = response.headers.get('Content-Type') ?? '';
  return (contentType.includes('json') ? await response.json() : null) as T;
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  const contentType = response.headers.get('Content-Type') ?? '';
  if (!contentType.includes('json')) {
    return null;
  }

  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return null;
  }
}

export function isApiError(error: unknown, status?: number): error is ApiError {
  return error instanceof ApiError && (status === undefined || error.status === status);
}
