import { ApiError } from '@/api/client';
import { authResponse, TRAVELLER } from '@/test/fixtures';
import { createSessionManager, type SessionApi } from '../session';
import type { SessionStorage, StoredSession } from '../tokenStorage';

function memoryStorage(initial: StoredSession | null = null) {
  let value = initial;
  const storage: SessionStorage & { current: () => StoredSession | null } = {
    load: jest.fn(async () => value),
    save: jest.fn(async (session: StoredSession) => {
      value = session;
    }),
    clear: jest.fn(async () => {
      value = null;
    }),
    current: () => value,
  };
  return storage;
}

function fakeApi(overrides: Partial<SessionApi> = {}): jest.Mocked<SessionApi> {
  return {
    login: jest.fn(async () => authResponse(1)),
    register: jest.fn(async () => authResponse(1)),
    refresh: jest.fn(async () => authResponse(2)),
    logout: jest.fn(async () => undefined),
    ...overrides,
  } as jest.Mocked<SessionApi>;
}

const rejected = () => new ApiError('The session has expired. Sign in again.', 401, 'invalid_refresh_token');
const offline = () => new ApiError('Cannot reach SkyJourney.', 0, 'network_error');

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

describe('session manager', () => {
  it('starts signed out when nothing is stored', async () => {
    const api = fakeApi();
    const session = createSessionManager({ api, storage: memoryStorage() });

    expect(session.getState()).toEqual({ status: 'loading' });
    await session.restore();

    expect(session.getState()).toEqual({ status: 'signedOut' });
    expect(api.refresh).not.toHaveBeenCalled();
  });

  it('signs in, keeps the access token in memory and persists only the refresh token and profile', async () => {
    const storage = memoryStorage();
    const session = createSessionManager({ api: fakeApi(), storage });
    const listener = jest.fn();
    session.subscribe(listener);

    await session.signIn({ email: 'ada@example.com', password: 'Correct-Horse-9' });

    expect(session.getState()).toEqual({ status: 'signedIn', user: TRAVELLER });
    expect(session.getAccessToken()).toBe('access-1');
    expect(storage.current()).toEqual({ refreshToken: 'refresh-1', user: TRAVELLER });
    expect(JSON.stringify(storage.current())).not.toContain('access-1');
    expect(listener).toHaveBeenCalled();
  });

  it('restores a stored session by rotating its refresh token', async () => {
    const storage = memoryStorage({ refreshToken: 'refresh-1', user: TRAVELLER });
    const api = fakeApi();
    const session = createSessionManager({ api, storage });

    await session.restore();

    expect(api.refresh).toHaveBeenCalledWith('refresh-1');
    expect(session.getState()).toEqual({ status: 'signedIn', user: TRAVELLER });
    expect(session.getAccessToken()).toBe('access-2');
    expect(storage.current()?.refreshToken).toBe('refresh-2');
  });

  it('signs out and clears storage when the server rejects the stored token', async () => {
    const storage = memoryStorage({ refreshToken: 'refresh-1', user: TRAVELLER });
    const session = createSessionManager({ api: fakeApi({ refresh: jest.fn(async () => Promise.reject(rejected())) }), storage });

    await session.restore();

    expect(session.getState()).toEqual({ status: 'signedOut' });
    expect(storage.current()).toBeNull();
  });

  it('stays signed in with the cached profile when offline at start-up', async () => {
    const storage = memoryStorage({ refreshToken: 'refresh-1', user: TRAVELLER });
    const session = createSessionManager({ api: fakeApi({ refresh: jest.fn(async () => Promise.reject(offline())) }), storage });

    await session.restore();

    expect(session.getState()).toEqual({ status: 'signedIn', user: TRAVELLER });
    expect(session.getAccessToken()).toBeNull();
    expect(storage.current()?.refreshToken).toBe('refresh-1');
  });

  it('shares one refresh between concurrent 401s (the server treats a second use as token theft)', async () => {
    const pending = deferred<ReturnType<typeof authResponse>>();
    const api = fakeApi({ refresh: jest.fn(() => pending.promise) });
    const session = createSessionManager({ api, storage: memoryStorage() });
    await session.signIn({ email: 'ada@example.com', password: 'Correct-Horse-9' });

    const results = [session.refreshAfterUnauthorized(), session.refreshAfterUnauthorized(), session.refreshAfterUnauthorized()];
    pending.resolve(authResponse(2));

    await expect(Promise.all(results)).resolves.toEqual([true, true, true]);
    expect(api.refresh).toHaveBeenCalledTimes(1);
    expect(session.getAccessToken()).toBe('access-2');
  });

  it('uses the rotated refresh token for the next refresh', async () => {
    const api = fakeApi({
      refresh: jest.fn().mockResolvedValueOnce(authResponse(2)).mockResolvedValueOnce(authResponse(3)),
    });
    const session = createSessionManager({ api, storage: memoryStorage() });
    await session.signIn({ email: 'ada@example.com', password: 'Correct-Horse-9' });

    await session.refreshAfterUnauthorized();
    await session.refreshAfterUnauthorized();

    expect(api.refresh.mock.calls).toEqual([['refresh-1'], ['refresh-2']]);
    expect(session.getAccessToken()).toBe('access-3');
  });

  it('keeps the session when a refresh fails transiently, but ends it when the token is rejected', async () => {
    const api = fakeApi({
      refresh: jest.fn().mockRejectedValueOnce(offline()).mockRejectedValueOnce(rejected()),
    });
    const session = createSessionManager({ api, storage: memoryStorage() });
    await session.signIn({ email: 'ada@example.com', password: 'Correct-Horse-9' });

    await expect(session.refreshAfterUnauthorized()).resolves.toBe(false);
    expect(session.getState().status).toBe('signedIn');

    await expect(session.refreshAfterUnauthorized()).resolves.toBe(false);
    expect(session.getState().status).toBe('signedOut');
    expect(session.getAccessToken()).toBeNull();
  });

  it('does not refresh when signed out', async () => {
    const api = fakeApi();
    const session = createSessionManager({ api, storage: memoryStorage() });
    await session.restore();

    await expect(session.refreshAfterUnauthorized()).resolves.toBe(false);
    expect(api.refresh).not.toHaveBeenCalled();
  });

  it('signs out locally even when the logout call fails, and revokes the current token', async () => {
    const storage = memoryStorage();
    const api = fakeApi({ logout: jest.fn(async () => Promise.reject(offline())) });
    const session = createSessionManager({ api, storage });
    await session.signIn({ email: 'ada@example.com', password: 'Correct-Horse-9' });

    await session.signOut();

    expect(api.logout).toHaveBeenCalledWith('refresh-1');
    expect(session.getState()).toEqual({ status: 'signedOut' });
    expect(session.getAccessToken()).toBeNull();
    expect(storage.current()).toBeNull();
  });

  it('ignores a refresh that completes after sign-out', async () => {
    const pending = deferred<ReturnType<typeof authResponse>>();
    const storage = memoryStorage();
    const session = createSessionManager({ api: fakeApi({ refresh: jest.fn(() => pending.promise) }), storage });
    await session.signIn({ email: 'ada@example.com', password: 'Correct-Horse-9' });

    const refreshing = session.refreshAfterUnauthorized();
    await session.signOut();
    pending.resolve(authResponse(2));

    await expect(refreshing).resolves.toBe(false);
    expect(session.getState()).toEqual({ status: 'signedOut' });
    expect(session.getAccessToken()).toBeNull();
    expect(storage.current()).toBeNull();
  });

  it('propagates sign-in errors without changing state', async () => {
    const error = new ApiError('Wrong', 401, 'invalid_credentials');
    const session = createSessionManager({ api: fakeApi({ login: jest.fn(async () => Promise.reject(error)) }), storage: memoryStorage() });
    await session.restore();

    await expect(session.signIn({ email: 'ada@example.com', password: 'nope' })).rejects.toBe(error);
    expect(session.getState()).toEqual({ status: 'signedOut' });
  });

  it('updates the stored profile after an edit', async () => {
    const storage = memoryStorage();
    const session = createSessionManager({ api: fakeApi(), storage });
    await session.signIn({ email: 'ada@example.com', password: 'Correct-Horse-9' });

    const renamed = { ...TRAVELLER, firstName: 'Augusta' };
    await session.updateUser(renamed);

    expect(session.getState()).toEqual({ status: 'signedIn', user: renamed });
    expect(storage.current()).toEqual({ refreshToken: 'refresh-1', user: renamed });
  });
});
