import * as SecureStore from 'expo-secure-store';

import type { UserProfile } from '@/api/auth';

const REFRESH_TOKEN_KEY = 'skyjourney.refreshToken';
const USER_KEY = 'skyjourney.user';

// Readable after the first unlock (so background refreshes work) and never migrated to another device.
const OPTIONS: SecureStore.SecureStoreOptions = {
  keychainAccessible: SecureStore.AFTER_FIRST_UNLOCK_THIS_DEVICE_ONLY,
};

export type StoredSession = { refreshToken: string; user: UserProfile | null };

/** Persists the refresh token and the last known profile. The access token is never persisted. */
export type SessionStorage = {
  load: () => Promise<StoredSession | null>;
  save: (session: StoredSession) => Promise<void>;
  clear: () => Promise<void>;
};

export const secureSessionStorage: SessionStorage = {
  async load() {
    const refreshToken = await SecureStore.getItemAsync(REFRESH_TOKEN_KEY, OPTIONS);
    if (!refreshToken) {
      return null;
    }
    const userJson = await SecureStore.getItemAsync(USER_KEY, OPTIONS);
    return { refreshToken, user: parseUser(userJson) };
  },
  async save({ refreshToken, user }) {
    await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, refreshToken, OPTIONS);
    if (user) {
      await SecureStore.setItemAsync(USER_KEY, JSON.stringify(user), OPTIONS);
    }
  },
  async clear() {
    await Promise.all([
      SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY, OPTIONS),
      SecureStore.deleteItemAsync(USER_KEY, OPTIONS),
    ]);
  },
};

function parseUser(json: string | null): UserProfile | null {
  if (!json) {
    return null;
  }
  try {
    return JSON.parse(json) as UserProfile;
  } catch {
    return null;
  }
}
