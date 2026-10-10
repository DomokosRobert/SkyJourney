import Ionicons from '@expo/vector-icons/Ionicons';
import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';

import { usePayBooking, type Booking } from '@/api/bookings';
import { PendingBookingGate } from '@/booking/BookingGate';
import { HoldBanner } from '@/booking/HoldBanner';
import { createIdempotencyKeys } from '@/booking/idempotency';
import { describeBookingError, isRetryableFailure, TEST_PAYMENT_METHODS, type TestPaymentToken } from '@/booking/payments';
import { AppText, Button, Card, FormMessage, Screen } from '@/components';
import { formatDate, formatLocalTime, formatMoney } from '@/lib/format';
import { colors, radius, spacing } from '@/theme';

export default function CheckoutScreen() {
  const { reference } = useLocalSearchParams<{ reference: string }>();
  return <PendingBookingGate reference={reference}>{(booking) => <Checkout booking={booking} />}</PendingBookingGate>;
}

function Checkout({ booking }: { booking: Booking }) {
  const [token, setToken] = useState<TestPaymentToken>('sim_success');
  const [message, setMessage] = useState<string | null>(null);
  // One key per payment request: a retry after a lost response replays the same attempt instead of charging twice.
  const [paymentKeys] = useState(() => createIdempotencyKeys());
  const pay = usePayBooking(booking.reference);

  const onPay = () => {
    setMessage(null);
    pay.mutate(
      { paymentToken: token, idempotencyKey: paymentKeys.keyFor({ reference: booking.reference, token }) },
      {
        onSuccess: (result) => {
          // Declined or approved, this attempt is settled; the next press is a new payment.
          paymentKeys.reset();
          if (result.status === 'Succeeded') {
            router.replace({ pathname: '/booking/[reference]/confirmation', params: { reference: booking.reference } });
          } else {
            setMessage(result.failureMessage ?? 'The payment was not approved. Choose another payment method.');
          }
        },
        onError: (error) => {
          if (!isRetryableFailure(error)) {
            paymentKeys.reset();
          }
          setMessage(describeBookingError(error));
        },
      },
    );
  };

  const seatsBySegment = booking.segments.map((s) => s.seats.reduce((sum, seat) => sum + seat.price.amount, 0));
  const passengerName = (id: string) => booking.passengers.find((p) => p.id === id)?.firstName ?? '';

  return (
    <Screen
      footer={
        <View style={styles.footer}>
          <View style={styles.row}>
            <AppText variant="subheading">Total to pay</AppText>
            <AppText variant="heading" color={colors.primary}>
              {formatMoney(booking.totalPrice)}
            </AppText>
          </View>
          <Button title={`Pay ${formatMoney(booking.totalPrice)}`} loading={pay.isPending} onPress={onPay} />
        </View>
      }>
      {booking.expiresAt ? <HoldBanner reference={booking.reference} expiresAt={booking.expiresAt} /> : null}
      <Card>
        <AppText variant="label" color={colors.textMuted}>
          Booking {booking.reference}
        </AppText>
        {booking.segments.map((segment, i) => (
          <View key={segment.id} style={styles.segment}>
            <View style={styles.row}>
              <AppText variant="subheading">
                {segment.flight.origin.iataCode} → {segment.flight.destination.iataCode}
              </AppText>
              <AppText variant="label">{segment.fareFamily}</AppText>
            </View>
            <AppText variant="caption">
              {formatDate(segment.flight.departureLocal)} · {formatLocalTime(segment.flight.departureLocal)} ·{' '}
              {segment.flight.flightNumber}
            </AppText>
            <View style={styles.row}>
              <AppText variant="body">
                Fare {formatMoney(segment.farePricePerPassenger)} × {booking.passengers.length}
              </AppText>
              <AppText variant="body">
                {formatMoney({
                  amount: Math.round(segment.farePricePerPassenger.amount * booking.passengers.length * 100) / 100,
                  currency: segment.farePricePerPassenger.currency,
                })}
              </AppText>
            </View>
            {segment.seats.length > 0 ? (
              <View style={styles.row}>
                <AppText variant="body" style={styles.shrink}>
                  Seats {segment.seats.map((s) => `${s.seatNumber} (${passengerName(s.passengerId)})`).join(', ')}
                </AppText>
                <AppText variant="body">
                  {formatMoney({ amount: Math.round((seatsBySegment[i] ?? 0) * 100) / 100, currency: booking.totalPrice.currency })}
                </AppText>
              </View>
            ) : (
              <AppText variant="caption">No seats chosen; seats are assigned at check-in.</AppText>
            )}
          </View>
        ))}
        <AppText variant="caption">
          Passengers: {booking.passengers.map((p) => `${p.firstName} ${p.lastName}`).join(', ')}
        </AppText>
      </Card>

      <View style={styles.notice} accessibilityRole="text">
        <Ionicons name="information-circle-outline" size={20} color={colors.primary} />
        <AppText variant="caption" style={styles.shrink}>
          Demo checkout: no real payment is taken and no card details are collected. Pick a simulated outcome below.
        </AppText>
      </View>

      <View accessibilityRole="radiogroup" style={styles.methods}>
        {TEST_PAYMENT_METHODS.map((method) => {
          const selected = method.token === token;
          return (
            <Pressable
              key={method.token}
              accessibilityRole="radio"
              accessibilityState={{ checked: selected, disabled: pay.isPending }}
              disabled={pay.isPending}
              onPress={() => setToken(method.token)}
              style={[styles.method, selected && styles.methodSelected]}>
              <Ionicons
                name={selected ? 'radio-button-on' : 'radio-button-off'}
                size={22}
                color={selected ? colors.primary : colors.textMuted}
              />
              <View style={styles.shrink}>
                <AppText variant="label">{method.label}</AppText>
                <AppText variant="caption">{method.description}</AppText>
              </View>
            </Pressable>
          );
        })}
      </View>
      <FormMessage message={message} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  footer: { gap: spacing.sm },
  row: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: spacing.sm },
  segment: { gap: spacing.xs, paddingVertical: spacing.sm },
  shrink: { flexShrink: 1 },
  notice: {
    flexDirection: 'row',
    gap: spacing.sm,
    padding: spacing.md,
    borderRadius: radius.md,
    backgroundColor: colors.primarySoft,
  },
  methods: { gap: spacing.sm },
  method: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    padding: spacing.md,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface,
  },
  methodSelected: { borderColor: colors.primary },
});
