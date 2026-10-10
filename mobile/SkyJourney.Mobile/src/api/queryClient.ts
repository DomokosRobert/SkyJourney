import NetInfo from '@react-native-community/netinfo';
import { focusManager, onlineManager, QueryClient } from '@tanstack/react-query';
import { AppState, Platform } from 'react-native';

import { ApiError } from './client';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 60_000,
      retry: (failureCount, error) =>
        failureCount < 2 && (!(error instanceof ApiError) || error.isNetworkError || error.status >= 500),
    },
    mutations: { retry: false },
  },
});

// Pause queries while offline and refetch on reconnect / when the app returns to the foreground.
onlineManager.setEventListener((setOnline) =>
  NetInfo.addEventListener((state) => setOnline(state.isConnected !== false)),
);

AppState.addEventListener('change', (status) => {
  if (Platform.OS !== 'web') {
    focusManager.setFocused(status === 'active');
  }
});
