import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { queryClient } from '@/api/queryClient';
import { BookingDraftProvider } from '@/booking/BookingDraftContext';
import { colors } from '@/theme';

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>
        <BookingDraftProvider>
          <StatusBar style="dark" />
          <Stack
            screenOptions={{
              headerTintColor: colors.primary,
              headerTitleStyle: { color: colors.text },
              headerBackButtonDisplayMode: 'minimal',
              contentStyle: { backgroundColor: colors.background },
            }}>
            <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
            <Stack.Screen name="airport-picker" options={{ presentation: 'modal', title: 'Choose airport' }} />
            <Stack.Screen name="date-picker" options={{ presentation: 'modal', title: 'Choose dates' }} />
            <Stack.Screen name="search/results" options={{ title: 'Flights' }} />
            <Stack.Screen name="search/flight/[flightId]" options={{ title: 'Flight details' }} />
            <Stack.Screen name="search/summary" options={{ title: 'Your trip' }} />
          </Stack>
        </BookingDraftProvider>
      </QueryClientProvider>
    </SafeAreaProvider>
  );
}
