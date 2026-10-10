import { resolveApiBaseUrl } from '../config';

describe('resolveApiBaseUrl', () => {
  it('prefers EXPO_PUBLIC_API_URL and strips trailing slashes', () => {
    expect(resolveApiBaseUrl({ envUrl: 'https://api.example.test/', hostUri: '192.168.1.20:8081', platform: 'ios' })).toBe(
      'https://api.example.test',
    );
  });

  it('uses the Metro host so a physical device on the same network reaches the API', () => {
    expect(resolveApiBaseUrl({ hostUri: '192.168.1.20:8081', platform: 'ios' })).toBe('http://192.168.1.20:5080');
  });

  it('falls back to localhost on iOS and the host loopback on Android', () => {
    expect(resolveApiBaseUrl({ hostUri: 'localhost:8081', platform: 'ios' })).toBe('http://localhost:5080');
    expect(resolveApiBaseUrl({ hostUri: null, platform: 'android' })).toBe('http://10.0.2.2:5080');
  });
});
