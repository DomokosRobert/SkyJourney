import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, View } from 'react-native';

import { useBooking, useCancelBooking, useCancellationQuote, type Booking } from '@/api/bookings';
import { useNow } from '@/booking/hold';
import { HoldBanner } from '@/booking/HoldBanner';
import { describeBookingError } from '@/booking/payments';
import { AppText, Button, Card, DemoDataNotice, ErrorState, FormMessage, LoadingState, Screen } from '@/components';
import { formatDate, formatDuration, formatLocalTime, formatMoney } from '@/lib/format';
import { colors, spacing } from '@/theme';
import { StatusBadge } from '@/trips/StatusBadge';

export default function BookingDetailsScreen() {
  const { reference } = useLocalSearchParams<{ reference: string }>();
  const booking = useBooking(reference);

  if (booking.isPending) {
    return <LoadingState message="Loading your booking…" />;
  }
  if (booking.isError) {
    return <ErrorState error={booking.error} onRetry={() => booking.refetch()} />;
  }
  return <BookingDetails booking={booking.data} />;
}

function BookingDetails({ booking }: { booking: Booking }) {
  const now = useNow(60_000);
  const firstDeparture = booking.segments[0]?.flight.departureUtc;
  const isUpcoming = !!firstDeparture && new Date(firstDeparture).getTime() > now;
  const passengerName = (id: string) => {
    const p = booking.passengers.find((x) => x.id === id);
    return p ? `${p.firstName} ${p.lastName}` : '';
  };

  return (
    <Screen>
      <View style={styles.row}>
        <View>
          <AppText variant="caption" color={colors.textMuted}>
            Booking reference
          </AppText>
          <AppText variant="title" selectable>
            {booking.reference}
          </AppText>
        </View>
        <StatusBadge status={booking.status} isUpcoming={isUpcoming} />
      </View>

      {booking.status === 'PendingPayment' && booking.expiresAt ? (
        <>
          <HoldBanner reference={booking.reference} expiresAt={booking.expiresAt} />
          <Button
            title="Continue to payment"
            onPress={() => router.push({ pathname: '/booking/[reference]/checkout', params: { reference: booking.reference } })}
          />
        </>
      ) : null}

      {booking.segments.map((segment) => (
        <Card key={segment.id}>
          <View style={styles.row}>
            <AppText variant="label" color={colors.textMuted}>
              {segment.direction === 'Outbound' ? 'Outbound' : 'Return'} · {formatDate(segment.flight.departureLocal)}
            </AppText>
            <AppText variant="label">{segment.fareFamily}</AppText>
          </View>
          <AppText variant="subheading">
            {segment.flight.origin.iataCode} {formatLocalTime(segment.flight.departureLocal)} →{' '}
            {segment.flight.destination.iataCode} {formatLocalTime(segment.flight.arrivalLocal)}
          </AppText>
          <AppText variant="caption">
            {segment.flight.flightNumber} · {segment.flight.airline.name} · {formatDuration(segment.flight.durationMinutes)} ·{' '}
            {segment.flight.aircraftName}
          </AppText>
          {segment.flight.status === 'Cancelled' ? (
            <AppText variant="label" color={colors.danger}>
              This flight was cancelled by the airline.
            </AppText>
          ) : null}
          <AppText variant="caption">
            {segment.fareConditions.checkedBaggageKg > 0
              ? `${segment.fareConditions.checkedBaggageKg} kg checked bag`
              : 'No checked bag'}
            {segment.fareConditions.includesCabinTrolley ? ' · cabin trolley' : ''} · {segment.fareConditions.refundPercentage}%
            refundable
          </AppText>
          {segment.seats.length > 0 ? (
            <AppText variant="caption">
              Seats: {segment.seats.map((s) => `${s.seatNumber} ${passengerName(s.passengerId)}`).join(', ')}
            </AppText>
          ) : null}
        </Card>
      ))}

      <Card>
        <AppText variant="subheading">Passengers</AppText>
        {booking.passengers.map((p) => (
          <AppText key={p.id} variant="body">
            {p.firstName} {p.lastName} · {p.type}
          </AppText>
        ))}
        <View style={styles.row}>
          <AppText variant="subheading">Total</AppText>
          <AppText variant="subheading">{formatMoney(booking.totalPrice)}</AppText>
        </View>
        {booking.status === 'Cancelled' ? (
          <View style={styles.row}>
            <AppText variant="body">Refunded (simulated)</AppText>
            <AppText variant="body" color={colors.success}>
              {formatMoney(booking.refundedAmount)}
            </AppText>
          </View>
        ) : null}
      </Card>

      {(booking.status === 'Confirmed' || booking.status === 'PendingPayment') && isUpcoming ? (
        <CancelBooking reference={booking.reference} paid={booking.status === 'Confirmed'} />
      ) : null}
      <DemoDataNotice />
    </Screen>
  );
}

/** Two-step cancellation: the first press loads the server's quote, the second confirms it. */
function CancelBooking({ reference, paid }: { reference: string; paid: boolean }) {
  const [confirming, setConfirming] = useState(false);
  const quote = useCancellationQuote(reference, confirming);
  const cancel = useCancelBooking(reference);

  if (!confirming) {
    return <Button title="Cancel booking" variant="secondary" onPress={() => setConfirming(true)} />;
  }

  return (
    <Card>
      <AppText variant="subheading">Cancel this booking?</AppText>
      {quote.isPending ? (
        <LoadingState message="Checking your refund…" />
      ) : quote.isError ? (
        <ErrorState error={quote.error} onRetry={() => quote.refetch()} />
      ) : quote.data.canCancel ? (
        <>
          <AppText variant="body">
            {!paid
              ? 'Nothing has been paid yet.'
              : quote.data.refund && quote.data.refund.amount > 0
                ? `You will be refunded ${formatMoney(quote.data.refund)} under your fare rules (simulated refund).`
                : 'Your fare is non-refundable: you will not get any money back.'}{' '}
            The seats are released and this cannot be undone.
          </AppText>
          <FormMessage message={cancel.isError ? describeBookingError(cancel.error) : null} />
          <Button title="Yes, cancel booking" variant="danger" loading={cancel.isPending} onPress={() => cancel.mutate()} />
        </>
      ) : (
        <AppText variant="body">{quote.data.reason ?? 'This booking can no longer be cancelled.'}</AppText>
      )}
      <Button title="Keep booking" variant="secondary" disabled={cancel.isPending} onPress={() => setConfirming(false)} />
    </Card>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: spacing.sm },
});
