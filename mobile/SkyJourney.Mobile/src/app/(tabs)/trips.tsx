import { router } from 'expo-router';
import { FlatList, Pressable, RefreshControl, StyleSheet, View } from 'react-native';

import { useMyBookings, type BookingSummary } from '@/api/bookings';
import { useAuthState } from '@/auth/AuthProvider';
import { AppText, Card, EmptyState, ErrorState, LoadingState, OfflineBanner } from '@/components';
import { formatDate, formatLocalTime, formatMoney } from '@/lib/format';
import { colors, spacing } from '@/theme';
import { StatusBadge } from '@/trips/StatusBadge';

export default function TripsScreen() {
  const auth = useAuthState();
  const signedIn = auth.status === 'signedIn';
  const bookings = useMyBookings(signedIn);

  if (auth.status === 'loading') {
    return <LoadingState />;
  }
  if (!signedIn) {
    return (
      <EmptyState
        icon="person-circle-outline"
        title="Sign in to see your trips"
        message="Your bookings are saved to your account."
        actionLabel="Sign in"
        onAction={() => router.push('/auth/login')}
      />
    );
  }
  if (bookings.isPending) {
    return <LoadingState message="Loading your trips…" />;
  }
  if (bookings.isError) {
    return <ErrorState error={bookings.error} onRetry={() => bookings.refetch()} />;
  }

  const items = bookings.data.pages.flatMap((page) => page.items);
  return (
    <View style={styles.fill}>
      <OfflineBanner />
      <FlatList
        data={items}
        keyExtractor={(item) => item.id}
        renderItem={({ item }) => <TripCard trip={item} />}
        contentContainerStyle={items.length === 0 ? styles.emptyContent : styles.content}
        refreshControl={
          <RefreshControl refreshing={bookings.isRefetching && !bookings.isFetchingNextPage} onRefresh={() => bookings.refetch()} />
        }
        onEndReached={() => {
          if (bookings.hasNextPage && !bookings.isFetchingNextPage) {
            void bookings.fetchNextPage();
          }
        }}
        onEndReachedThreshold={0.5}
        ListEmptyComponent={
          <EmptyState
            title="No trips yet"
            message="Bookings you make will appear here."
            actionLabel="Search flights"
            onAction={() => router.navigate('/')}
          />
        }
        ListFooterComponent={bookings.isFetchingNextPage ? <LoadingState message="Loading more…" /> : null}
      />
    </View>
  );
}

function TripCard({ trip }: { trip: BookingSummary }) {
  const route = `${trip.origin.city} → ${trip.destination.city}`;
  const dates = trip.returnDepartureLocal
    ? `${formatDate(trip.departureLocal)} – ${formatDate(trip.returnDepartureLocal)}`
    : `${formatDate(trip.departureLocal)} · ${formatLocalTime(trip.departureLocal)}`;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${route}, ${dates}, booking ${trip.reference}`}
      onPress={() => router.push({ pathname: '/trips/[reference]', params: { reference: trip.reference } })}>
      <Card>
        <View style={styles.row}>
          <AppText variant="subheading" style={styles.shrink}>
            {trip.origin.iataCode} → {trip.destination.iataCode}
            {trip.returnDepartureLocal ? ' (return)' : ''}
          </AppText>
          <StatusBadge status={trip.status} isUpcoming={trip.isUpcoming} />
        </View>
        <AppText variant="body">{route}</AppText>
        <AppText variant="caption">{dates}</AppText>
        <View style={styles.row}>
          <AppText variant="caption" color={colors.textMuted}>
            {trip.reference} · {trip.passengerCount} {trip.passengerCount === 1 ? 'passenger' : 'passengers'}
          </AppText>
          <AppText variant="label">{formatMoney(trip.totalPrice)}</AppText>
        </View>
      </Card>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1, backgroundColor: colors.background },
  content: { padding: spacing.lg, gap: spacing.md },
  emptyContent: { flexGrow: 1 },
  row: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: spacing.sm },
  shrink: { flexShrink: 1 },
});
