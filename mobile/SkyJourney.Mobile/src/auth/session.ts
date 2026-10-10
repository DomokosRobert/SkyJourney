import { ApiError } from '@/api/client';
import type { AuthResponse, LoginRequest, RegisterRequest, UserProfile } from '@/api/auth';
import type { SessionStorage } from './tokenStorage';

export type SessionState =
  | { status: 'loading' }
  | { status: 'signedOut' }
  | { status: 'signedIn'; user: UserProfile };

export type SessionApi = {
  login: (body: LoginRequest) => Promise<AuthResponse>;
  register: (body: RegisterRequest) => Promise<AuthResponse>;
  refresh: (refreshToken: string) => Promise<AuthResponse>;
  logout: (refreshToken: string) => Promise<void>;
};

export type SessionManager = ReturnType<typeof createSessionManager>;

/**
 * Owns the signed-in session independently of React. The access token lives only in memory; the
 * refresh token lives in secure storage and is rotated on every refresh.
 *
 * The API treats two refreshes with the same token as token theft and ends the session, so refreshes
 * are single-flight: concurrent callers share one in-flight request.
 */
export function createSessionManager({ api, storage }: { api: SessionApi; storage: SessionStorage }) {
  let state: SessionState = { status: 'loading' };
  let accessToken: string | null = null;
  let refreshToken: string | null = null;
  let inflightRefresh: Promise<boolean> | null = null;
  // Bumped on every sign-in/sign-out so a refresh that finishes afterwards cannot resurrect a stale session.
  let generation = 0;
  const listeners = new Set<() => void>();

  const setState = (next: SessionState) => {
    state = next;
    listeners.forEach((listener) => listener());
  };

  const adopt = async (response: AuthResponse) => {
    accessToken = response.accessToken;
    refreshToken = response.refreshToken;
    await storage.save({ refreshToken: response.refreshToken, user: response.user });
    setState({ status: 'signedIn', user: response.user });
  };

  const forget = async () => {
    generation++;
    accessToken = null;
    refreshToken = null;
    inflightRefresh = null;
    await storage.clear();
    setState({ status: 'signedOut' });
  };

  /** A rejected refresh token means the session is over; anything else (offline, 5xx, 429) is transient. */
  const isSessionRejected = (error: unknown) =>
    error instanceof ApiError && (error.status === 401 || error.status === 400);

  const doRefresh = async (): Promise<boolean> => {
    const token = refreshToken;
    if (!token) {
      return false;
    }
    const startedIn = generation;
    try {
      const response = await api.refresh(token);
      if (startedIn !== generation) {
        return false;
      }
      await adopt(response);
      return true;
    } catch (error) {
      if (startedIn === generation && isSessionRejected(error)) {
        await forget();
      }
      return false;
    }
  };

  const refresh = (): Promise<boolean> => {
    if (!inflightRefresh) {
      const current = doRefresh().finally(() => {
        if (inflightRefresh === current) {
          inflightRefresh = null;
        }
      });
      inflightRefresh = current;
    }
    return inflightRefresh;
  };

  return {
    getState: () => state,

    subscribe(listener: () => void) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },

    /** Restores a stored session at app start. Offline, the cached profile keeps the user signed in. */
    async restore() {
      let stored;
      try {
        stored = await storage.load();
      } catch {
        stored = null;
      }
      if (!stored) {
        setState({ status: 'signedOut' });
        return;
      }

      refreshToken = stored.refreshToken;
      if (await refresh()) {
        return;
      }
      if (state.status === 'signedOut') {
        return; // the server rejected the stored token
      }
      // Transient failure: stay signed in with the cached profile; the next API call retries the refresh.
      setState(stored.user ? { status: 'signedIn', user: stored.user } : { status: 'signedOut' });
    },

    async signIn(credentials: LoginRequest) {
      const response = await api.login(credentials);
      generation++;
      await adopt(response);
    },

    async register(details: RegisterRequest) {
      const response = await api.register(details);
      generation++;
      await adopt(response);
    },

    /** Revokes the session on the server when possible, but always signs out locally. */
    async signOut() {
      const token = refreshToken;
      await forget();
      if (token) {
        await api.logout(token).catch(() => undefined);
      }
    },

    /** Keeps the stored profile in sync after the user edits it. */
    async updateUser(user: UserProfile) {
      if (state.status !== 'signedIn' || !refreshToken) {
        return;
      }
      await storage.save({ refreshToken, user });
      setState({ status: 'signedIn', user });
    },

    getAccessToken: () => accessToken,

    /** Called by the API client after a 401: refreshes once and reports whether to retry the request. */
    refreshAfterUnauthorized: () => (state.status === 'signedIn' ? refresh() : Promise.resolve(false)),

    refresh,
  };
}
