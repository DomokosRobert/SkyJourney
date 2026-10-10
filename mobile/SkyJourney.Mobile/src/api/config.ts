import Constants from 'expo-constants';
import { Platform } from 'react-native';

export const DEFAULT_API_PORT = 5080;

type Environment = { envUrl?: string; hostUri?: string | null; platform: string };

/**
 * Where the API lives:
 * 1. `EXPO_PUBLIC_API_URL` when set (staging/production builds, or an explicit override).
 * 2. In development, the machine running Metro. Its LAN IP comes from `hostUri`, so a physical
 *    iPhone on the same Wi-Fi reaches the API without extra configuration.
 * 3. Simulator/emulator fallback: `localhost` on iOS, `10.0.2.2` (the host loopback) on Android.
 */
export function resolveApiBaseUrl({ envUrl, hostUri, platform }: Environment): string {
  if (envUrl) {
    return envUrl.replace(/\/+$/, '');
  }

  const host = hostUri?.split(':')[0];
  if (host && host !== 'localhost' && host !== '127.0.0.1') {
    return `http://${host}:${DEFAULT_API_PORT}`;
  }

  return platform === 'android' ? `http://10.0.2.2:${DEFAULT_API_PORT}` : `http://localhost:${DEFAULT_API_PORT}`;
}

export const API_BASE_URL = resolveApiBaseUrl({
  envUrl: process.env.EXPO_PUBLIC_API_URL,
  hostUri: Constants.expoConfig?.hostUri,
  platform: Platform.OS,
});
