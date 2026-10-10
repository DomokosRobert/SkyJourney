import { API_BASE_URL } from './config';

const REQUEST_TIMEOUT_MS = 15_000;

/** An RFC 9457 Problem Details response from the API, or a transport failure. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code: string,
    readonly fieldErrors: Record<string, string[]> = {},
    readonly correlationId?: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  get isNetworkError() {
    return this.status === 0;
  }
}

type ProblemDetails = {
  title?: string;
  detail?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
  correlationId?: string;
};

export type RequestOptions = {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  headers?: Record<string, string>;
  signal?: AbortSignal;
  /** Skip the auth hook, e.g. for login and token refresh themselves. */
  anonymous?: boolean;
};

/**
 * Hooks installed by the auth layer: one supplies the current access token, the other tries a
 * token refresh after a 401 and reports whether the request should be retried.
 */
type AuthHooks = {
  getAccessToken: () => string | null;
  refreshAfterUnauthorized: () => Promise<boolean>;
};

let authHooks: AuthHooks | null = null;

export function setAuthHooks(hooks: AuthHooks | null) {
  authHooks = hooks;
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await send(path, options);

  if (response.status === 401 && !options.anonymous && authHooks && (await authHooks.refreshAfterUnauthorized())) {
    return parse<T>(await send(path, options));
  }

  return parse<T>(response);
}

async function send(path: string, { method = 'GET', body, headers, signal, anonymous }: RequestOptions) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);
  signal?.addEventListener('abort', () => controller.abort());

  const token = anonymous ? null : authHooks?.getAccessToken();
  try {
    return await fetch(`${API_BASE_URL}${path}`, {
      method,
      signal: controller.signal,
      headers: {
        Accept: 'application/json',
        ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...headers,
      },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch {
    if (signal?.aborted) {
      throw new ApiError('Request cancelled.', 0, 'cancelled');
    }
    throw new ApiError('Cannot reach SkyJourney. Check your connection and try again.', 0, 'network_error');
  } finally {
    clearTimeout(timeout);
  }
}

async function parse<T>(response: Response): Promise<T> {
  if (response.ok) {
    return (response.status === 204 ? undefined : await response.json()) as T;
  }

  let problem: ProblemDetails = {};
  try {
    problem = (await response.json()) as ProblemDetails;
  } catch {
    // Non-JSON error body (e.g. a proxy error page).
  }

  throw new ApiError(
    problem.detail ?? problem.title ?? `Request failed (${response.status}).`,
    response.status,
    problem.code ?? 'http_error',
    problem.errors ?? {},
    problem.correlationId,
  );
}

export function toQueryString(params: Record<string, string | number | undefined | null>): string {
  const entries = Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== '');
  return entries.map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`).join('&');
}
