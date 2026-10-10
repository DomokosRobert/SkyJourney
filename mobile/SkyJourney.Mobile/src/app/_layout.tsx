import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { queryClient } from '@/api/queryClient';
import { AuthProvider } from '@/auth/AuthProvider';
import { BookingDraftProvider } from '@/booking/BookingDraftContext';
import { colors } from '@/theme';

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
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
            <Stack.Screen name="booking/passengers" options={{ title: 'Passengers' }} />
            <Stack.Screen name="booking/[reference]/seats" options={{ title: 'Seats' }} />
            <Stack.Screen name="booking/[reference]/checkout" options={{ title: 'Payment' }} />
            <Stack.Screen
              name="booking/[reference]/confirmation"
              options={{ title: 'Confirmed', headerBackVisible: false, gestureEnabled: false }}
            />
            <Stack.Screen name="trips/[reference]" options={{ title: 'Booking' }} />
            <Stack.Screen name="auth/login" options={{ presentation: 'modal', title: 'Sign in' }} />
            <Stack.Screen name="auth/register" options={{ presentation: 'modal', title: 'Create account' }} />
          </Stack>
          </BookingDraftProvider>
        </AuthProvider>
      </QueryClientProvider>
    </SafeAreaProvider>
  );
}
