import { router } from 'expo-router';
import { StyleSheet, View } from 'react-native';

import { useAuthState } from '@/auth/AuthProvider';
import { useBookingDraft, type FareSelection } from '@/booking/BookingDraftContext';
import { AppText, Button, Card, DemoDataNotice, EmptyState, Screen } from '@/components';
import { formatDate, formatLocalTime, formatMoney } from '@/lib/format';
import { passengerSummary } from '@/search/criteria';
import { colors, spacing } from '@/theme';

export default function TripSummaryScreen() {
  const { criteria, outbound, inbound } = useBookingDraft();
  const { status } = useAuthState();
  const needsReturn = criteria.tripType === 'return';

  if (!outbound || (needsReturn && !inbound)) {
    return (
      <EmptyState
        title="No flights selected"
        message="Search and choose your flights first."
        actionLabel="New search"
        onAction={() => router.dismissAll()}
      />
    );
  }

  const selections = [outbound, ...(inbound ? [inbound] : [])];
  const currency = outbound.fare.totalPrice.currency;
  const total = selections.reduce((sum, s) => sum + s.fare.totalPrice.amount, 0);

  return (
    <Screen
      footer={
        <Button
          title={status === 'signedIn' ? 'Continue to passengers' : 'Sign in to continue'}
          disabled={status === 'loading'}
          onPress={() => router.push(status === 'signedIn' ? '/booking/passengers' : '/auth/login')}
        />
      }>
      <AppText variant="heading">Your trip</AppText>
      <SegmentSummary title="Outbound" selection={outbound} />
      {inbound ? <SegmentSummary title="Return" selection={inbound} /> : null}
      <Card>
        <View style={styles.row}>
          <AppText variant="body">{passengerSummary(criteria.adults, criteria.children)}</AppText>
        </View>
        <View style={styles.row}>
          <AppText variant="subheading">Fare total</AppText>
          <AppText variant="heading" color={colors.primary}>
            {formatMoney({ amount: Math.round(total * 100) / 100, currency })}
          </AppText>
        </View>
        <AppText variant="caption">
          Indicative price from your search. The final price is confirmed by SkyJourney when the booking is created;
          optional paid seats can be added in the next steps.
        </AppText>
      </Card>
      <Button title="Change flights" variant="secondary" onPress={() => router.dismissTo('/search/results')} />
      <DemoDataNotice />
    </Screen>
  );
}

function SegmentSummary({ title, selection }: { title: string; selection: FareSelection }) {
  const { flight, fare } = selection;
  return (
    <Card>
      <View style={styles.row}>
        <AppText variant="label" color={colors.textMuted}>
          {title} · {formatDate(flight.departureLocal)}
        </AppText>
        <AppText variant="label">{fare.family}</AppText>
      </View>
      <AppText variant="subheading">
        {flight.origin.iataCode} {formatLocalTime(flight.departureLocal)} → {flight.destination.iataCode}{' '}
        {formatLocalTime(flight.arrivalLocal)}
      </AppText>
      <View style={styles.row}>
        <AppText variant="caption">
          {flight.flightNumber} · {flight.origin.city} to {flight.destination.city}
        </AppText>
        <AppText variant="body">{formatMoney(fare.totalPrice)}</AppText>
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: spacing.sm },
});
