import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { router } from 'expo-router';

import LoginScreen from '@/app/auth/login';
import { ApiError } from '@/api/client';
import { authResponse } from '@/test/fixtures';
import { AuthProvider } from '../AuthProvider';
import { createSessionManager, type SessionApi } from '../session';

jest.mock('expo-router', () => ({
  router: { back: jest.fn(), replace: jest.fn(), canGoBack: jest.fn(() => true) },
}));

function renderLogin(login: SessionApi['login']) {
  const manager = createSessionManager({
    api: { login, register: jest.fn(), refresh: jest.fn(), logout: jest.fn() },
    storage: { load: async () => null, save: async () => undefined, clear: async () => undefined },
  });
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <AuthProvider manager={manager}>
        <LoginScreen />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

async function fillAndSubmit(email: string, password: string) {
  await fireEvent.changeText(screen.getByLabelText('E-mail'), email);
  await fireEvent.changeText(screen.getByLabelText('Password'), password);
  await fireEvent.press(screen.getByRole('button', { name: 'Sign in' }));
}

describe('LoginScreen', () => {
  afterEach(() => jest.clearAllMocks());

  it('validates locally before calling the API', async () => {
    const login = jest.fn();
    await renderLogin(login);

    await fillAndSubmit('not-an-email', '');

    expect(await screen.findByText('Enter a valid e-mail address.')).toBeOnTheScreen();
    expect(screen.getByText('Enter your password.')).toBeOnTheScreen();
    expect(login).not.toHaveBeenCalled();
  });

  it('signs in with the trimmed e-mail and closes the modal', async () => {
    const login = jest.fn(async () => authResponse(1));
    await renderLogin(login);

    await fillAndSubmit(' ada@example.com ', 'Correct-Horse-9');

    await waitFor(() => expect(router.back).toHaveBeenCalled());
    expect(login).toHaveBeenCalledWith({ email: 'ada@example.com', password: 'Correct-Horse-9' });
  });

  it('shows a form-level message for wrong credentials and stays open', async () => {
    const login = jest.fn(async () => Promise.reject(new ApiError('x', 401, 'invalid_credentials')));
    await renderLogin(login);

    await fillAndSubmit('ada@example.com', 'wrong-password');

    expect(await screen.findByText('The e-mail address or password is incorrect.')).toBeOnTheScreen();
    expect(router.back).not.toHaveBeenCalled();
  });
});
