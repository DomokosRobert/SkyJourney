import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react-native';
import type { ReactElement } from 'react';

import { AuthProvider } from '@/auth/AuthProvider';
import { createSessionManager } from '@/auth/session';
import { authResponse, TRAVELLER } from './fixtures';

/**
 * Renders a screen inside the real query and auth providers. `signedIn` restores a stored session through a fake
 * refresh, as at app start. The query client schedules no cache timers, so Jest can exit after the tests.
 */
export async function renderWithProviders(ui: ReactElement, { signedIn = true } = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { gcTime: Infinity } },
  });
  const manager = createSessionManager({
    api: { login: jest.fn(), register: jest.fn(), refresh: jest.fn(async () => authResponse(2)), logout: jest.fn() },
    storage: {
      load: async () => (signedIn ? { refreshToken: 'refresh-1', user: TRAVELLER } : null),
      save: async () => undefined,
      clear: async () => undefined,
    },
  });
  const result = await render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider manager={manager}>{ui}</AuthProvider>
    </QueryClientProvider>,
  );
  return { ...result, queryClient, manager };
}
