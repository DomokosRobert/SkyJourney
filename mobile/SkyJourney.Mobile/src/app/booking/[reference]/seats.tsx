import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, View } from 'react-native';

import { useSelectSeat, useSeatMap, type Booking, type SeatMapSeat } from '@/api/bookings';
import { PendingBookingGate } from '@/booking/BookingGate';
import { HoldBanner } from '@/booking/HoldBanner';
import { describeBookingError } from '@/booking/payments';
import { nextUnseatedPassenger } from '@/booking/seatLayout';
import { SeatMapView } from '@/booking/SeatMapView';
import { AppText, Button, Card, Chip, ErrorState, FormMessage, LoadingState, Screen } from '@/components';
import { formatMoney } from '@/lib/format';
import { colors, spacing } from '@/theme';

export default function SeatsScreen() {
  const { reference } = useLocalSearchParams<{ reference: string }>();
  return <PendingBookingGate reference={reference}>{(booking) => <SeatSelection booking={booking} />}</PendingBookingGate>;
}

function SeatSelection({ booking }: { booking: Booking }) {
  const [segmentIndex, setSegmentIndex] = useState(0);
  const segment = booking.segments[segmentIndex];
  const seatedIds = new Set(segment?.seats.map((s) => s.passengerId));
  const [activePassenger, setActivePassenger] = useState<string | null>(() =>
    nextUnseatedPassenger(booking.passengers, seatedIds, null),
  );
  const [message, setMessage] = useState<string | null>(null);
  const seatMap = useSeatMap(booking.reference, segment?.id);
  const selectSeat = useSelectSeat(booking.reference, segment?.id ?? '');

  const initials = Object.fromEntries(
    booking.passengers.map((p) => [p.id, `${p.firstName[0] ?? ''}${p.lastName[0] ?? ''}`.toUpperCase()]),
  );
  const seatsTotal = booking.segments.flatMap((s) => s.seats).reduce((sum, s) => sum + s.price.amount, 0);
  const passenger = booking.passengers.find((p) => p.id === activePassenger) ?? booking.passengers[0];
  if (!segment || !passenger) {
    return null; // Every booking has at least one segment and one passenger.
  }

  const switchSegment = (index: number) => {
    setSegmentIndex(index);
    setMessage(null);
    const seated = new Set(booking.segments[index]?.seats.map((s) => s.passengerId));
    setActivePassenger(nextUnseatedPassenger(booking.passengers, seated, null) ?? passenger.id);
  };

  const onSelect = (seat: SeatMapSeat) => {
    setMessage(null);
    if (seat.availability === 'Selected') {
      // Tapping one of this booking's seats picks that passenger, so their seat can be changed.
      if (seat.passengerId) {
        setActivePassenger(seat.passengerId);
      }
      return;
    }
    selectSeat.mutate(
      { passengerId: passenger.id, seatNumber: seat.seatNumber },
      {
        onSuccess: (updated) => {
          const seated = new Set(updated.segments[segmentIndex]?.seats.map((s) => s.passengerId));
          setActivePassenger(nextUnseatedPassenger(updated.passengers, seated, passenger.id) ?? passenger.id);
        },
        onError: (error) => setMessage(describeBookingError(error)),
      },
    );
  };

  return (
    <Screen
      footer={
        <View style={styles.footer}>
          <View style={styles.between}>
            <AppText variant="caption">Seats selected</AppText>
            <AppText variant="label">{formatMoney({ amount: Math.round(seatsTotal * 100) / 100, currency: booking.totalPrice.currency })}</AppText>
          </View>
          <Button
            title="Continue to payment"
            onPress={() =>
              router.push({ pathname: '/booking/[reference]/checkout', params: { reference: booking.reference } })
            }
          />
        </View>
      }>
      {booking.expiresAt ? <HoldBanner reference={booking.reference} expiresAt={booking.expiresAt} /> : null}
      {booking.segments.length > 1 ? (
        <View style={styles.chips}>
          {booking.segments.map((s, i) => (
            <Chip
              key={s.id}
              label={`${s.flight.origin.iataCode} → ${s.flight.destination.iataCode}`}
              selected={i === segmentIndex}
              onPress={() => switchSegment(i)}
            />
          ))}
        </View>
      ) : null}
      <Card>
        <AppText variant="subheading">Choosing a seat for</AppText>
        <View style={styles.chips}>
          {booking.passengers.map((p) => {
            const seat = segment.seats.find((s) => s.passengerId === p.id);
            return (
              <Chip
                key={p.id}
                label={`${p.firstName}${seat ? ` · ${seat.seatNumber}` : ''}`}
                selected={p.id === passenger.id}
                onPress={() => setActivePassenger(p.id)}
              />
            );
          })}
        </View>
        <AppText variant="caption">
          Seat selection is optional. {segment.fareConditions.includesSeatSelection ? 'Seats are included in your fare.' : 'Prices are per seat and flight.'}
        </AppText>
      </Card>
      <FormMessage message={message} />
      {seatMap.isPending ? (
        <LoadingState message="Loading seat map…" />
      ) : seatMap.isError ? (
        <ErrorState error={seatMap.error} onRetry={() => seatMap.refetch()} />
      ) : (
        <Card>
          <AppText variant="caption" style={styles.center}>
            {seatMap.data.flightNumber} · {seatMap.data.aircraftName} · front of the aircraft
          </AppText>
          <SeatMapView map={seatMap.data} initials={initials} disabled={selectSeat.isPending} onSelect={onSelect} />
        </Card>
      )}
      <AppText variant="caption" color={colors.textMuted} style={styles.center}>
        Demo seat map. Seats and prices are generated for this portfolio project.
      </AppText>
    </Screen>
  );
}

const styles = StyleSheet.create({
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  footer: { gap: spacing.sm },
  between: { flexDirection: 'row', justifyContent: 'space-between' },
  center: { textAlign: 'center' },
});
