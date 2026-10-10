import * as SecureStore from 'expo-secure-store';

import { TRAVELLER } from '@/test/fixtures';
import { secureSessionStorage } from '../tokenStorage';

jest.mock('expo-secure-store', () => {
  const items = new Map<string, string>();
  return {
    AFTER_FIRST_UNLOCK_THIS_DEVICE_ONLY: 'afterFirstUnlockThisDeviceOnly',
    getItemAsync: jest.fn(async (key: string) => items.get(key) ?? null),
    setItemAsync: jest.fn(async (key: string, value: string) => {
      items.set(key, value);
    }),
    deleteItemAsync: jest.fn(async (key: string) => {
      items.delete(key);
    }),
  };
});

describe('secureSessionStorage', () => {
  afterEach(() => secureSessionStorage.clear());

  it('round-trips the refresh token and profile with device-only keychain access', async () => {
    await secureSessionStorage.save({ refreshToken: 'refresh-1', user: TRAVELLER });

    await expect(secureSessionStorage.load()).resolves.toEqual({ refreshToken: 'refresh-1', user: TRAVELLER });
    expect(SecureStore.setItemAsync).toHaveBeenCalledWith('skyjourney.refreshToken', 'refresh-1', {
      keychainAccessible: 'afterFirstUnlockThisDeviceOnly',
    });
  });

  it('returns null when no session is stored, and after clearing', async () => {
    await expect(secureSessionStorage.load()).resolves.toBeNull();

    await secureSessionStorage.save({ refreshToken: 'refresh-1', user: TRAVELLER });
    await secureSessionStorage.clear();

    await expect(secureSessionStorage.load()).resolves.toBeNull();
  });

  it('tolerates a corrupted cached profile', async () => {
    await SecureStore.setItemAsync('skyjourney.refreshToken', 'refresh-1');
    await SecureStore.setItemAsync('skyjourney.user', '{not json');

    await expect(secureSessionStorage.load()).resolves.toEqual({ refreshToken: 'refresh-1', user: null });
  });
});
