import Ionicons from '@expo/vector-icons/Ionicons';
import { router, useLocalSearchParams } from 'expo-router';
import { useEffect } from 'react';
import { StyleSheet, View } from 'react-native';

import { useBooking } from '@/api/bookings';
import { useBookingDraft } from '@/booking/BookingDraftContext';
import { AppText, Button, Card, DemoDataNotice, ErrorState, LoadingState, Screen } from '@/components';
import { formatDate, formatLocalTime, formatMoney } from '@/lib/format';
import { colors, spacing } from '@/theme';

export default function ConfirmationScreen() {
  const { reference } = useLocalSearchParams<{ reference: string }>();
  const booking = useBooking(reference);
  const { clearSelection, createBookingKeys } = useBookingDraft();

  // The trip is booked: the next search starts a fresh draft and a fresh booking request.
  useEffect(() => {
    clearSelection();
    createBookingKeys.reset();
  }, [clearSelection, createBookingKeys]);

  if (booking.isPending) {
    return <LoadingState message="Loading your booking…" />;
  }
  if (booking.isError) {
    return <ErrorState error={booking.error} onRetry={() => booking.refetch()} />;
  }

  const data = booking.data;
  const confirmed = data.status === 'Confirmed';

  return (
    <Screen
      footer={
        <View style={styles.footer}>
          <Button
            title="Go to my trips"
            onPress={() => {
              router.dismissAll();
              router.navigate('/trips');
            }}
          />
          <Button title="Done" variant="secondary" onPress={() => router.dismissAll()} />
        </View>
      }>
      <View style={styles.hero}>
        <Ionicons
          name={confirmed ? 'checkmark-circle' : 'alert-circle'}
          size={64}
          color={confirmed ? colors.success : colors.warning}
        />
        <AppText variant="title" accessibilityRole="header">
          {confirmed ? 'You’re booked!' : `Booking ${data.status.toLowerCase()}`}
        </AppText>
        <AppText variant="body" color={colors.textMuted}>
          Booking reference
        </AppText>
        <AppText variant="title" color={colors.primary} selectable accessibilityLabel={`Booking reference ${data.reference.split('').join(' ')}`}>
          {data.reference}
        </AppText>
      </View>
      <Card>
        {data.segments.map((segment) => (
          <View key={segment.id} style={styles.segment}>
            <AppText variant="label" color={colors.textMuted}>
              {segment.direction === 'Outbound' ? 'Outbound' : 'Return'} · {formatDate(segment.flight.departureLocal)}
            </AppText>
            <AppText variant="subheading">
              {segment.flight.origin.iataCode} {formatLocalTime(segment.flight.departureLocal)} →{' '}
              {segment.flight.destination.iataCode} {formatLocalTime(segment.flight.arrivalLocal)}
            </AppText>
            <AppText variant="caption">
              {segment.flight.flightNumber} · {segment.fareFamily}
              {segment.seats.length > 0 ? ` · Seats ${segment.seats.map((s) => s.seatNumber).join(', ')}` : ''}
            </AppText>
          </View>
        ))}
        <View style={styles.row}>
          <AppText variant="body">
            {data.passengers.length} {data.passengers.length === 1 ? 'passenger' : 'passengers'}
          </AppText>
          <AppText variant="subheading">{formatMoney(data.totalPrice)}{confirmed ? ' paid' : ''}</AppText>
        </View>
      </Card>
      <DemoDataNotice />
    </Screen>
  );
}

const styles = StyleSheet.create({
  hero: { alignItems: 'center', gap: spacing.sm, paddingVertical: spacing.lg },
  segment: { gap: spacing.xs, paddingBottom: spacing.sm },
  row: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  footer: { gap: spacing.sm },
});
