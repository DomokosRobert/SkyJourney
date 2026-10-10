import { router, useLocalSearchParams } from 'expo-router';
import { StyleSheet, View } from 'react-native';

import { useFlight } from '@/api/flights';
import type { FareOffer, FlightOffer } from '@/api/types';
import { useBookingDraft, type Leg } from '@/booking/BookingDraftContext';
import { AppText, Card, DemoDataNotice, ErrorState, LoadingState, Screen } from '@/components';
import { dayOffset, formatDate, formatDuration, formatLocalTime } from '@/lib/format';
import { FareCard } from '@/search/FareCard';
import { isValidReturn } from '@/search/results';
import { colors, spacing } from '@/theme';

export default function FlightDetailsScreen() {
  const { flightId, leg = 'outbound' } = useLocalSearchParams<{ flightId: string; leg?: Leg }>();
  const { criteria, outbound, selectFare } = useBookingDraft();
  const passengers = criteria.adults + criteria.children;
  const flight = useFlight(flightId, passengers);

  if (flight.isPending) {
    return <LoadingState message="Loading flight…" />;
  }
  if (flight.isError) {
    return <ErrorState error={flight.error} onRetry={() => flight.refetch()} />;
  }

  const offer = flight.data;
  const tooSoon = leg === 'return' && !isValidReturn(outbound?.flight, offer);

  const choose = (fare: FareOffer) => {
    selectFare(leg, { flight: offer, fare });
    if (leg === 'outbound' && criteria.tripType === 'return') {
      router.push({ pathname: '/search/results', params: { leg: 'return' } });
    } else {
      router.push('/search/summary');
    }
  };

  return (
    <Screen>
      <FlightTimeline offer={offer} />
      {!offer.isBookable || offer.fares.length === 0 ? (
        <Card>
          <AppText variant="subheading" color={colors.danger}>
            This flight is no longer available
          </AppText>
          <AppText variant="caption">It may have sold out or closed for sale. Please choose another flight.</AppText>
        </Card>
      ) : (
        <>
          {tooSoon ? (
            <AppText variant="label" color={colors.danger}>
              This flight departs too soon after your outbound flight lands.
            </AppText>
          ) : null}
          <AppText variant="heading">Choose your fare</AppText>
          {offer.fares.map((fare) => (
            <FareCard key={fare.family} fare={fare} passengers={passengers} onSelect={() => choose(fare)} disabled={tooSoon} />
          ))}
        </>
      )}
      <DemoDataNotice />
    </Screen>
  );
}

export function FlightTimeline({ offer }: { offer: FlightOffer }) {
  const plusDays = dayOffset(offer.departureLocal, offer.arrivalLocal);
  return (
    <Card>
      <AppText variant="caption">
        {offer.airline.name} · {offer.flightNumber} · {offer.aircraftName}
      </AppText>
      <View style={styles.stop}>
        <AppText variant="heading" style={styles.time}>
          {formatLocalTime(offer.departureLocal)}
        </AppText>
        <View style={styles.flex}>
          <AppText variant="subheading">
            {offer.origin.city} ({offer.origin.iataCode})
          </AppText>
          <AppText variant="caption">
            {offer.origin.name} · {formatDate(offer.departureLocal)}
          </AppText>
        </View>
      </View>
      <AppText variant="caption" style={styles.duration}>
        Direct · {formatDuration(offer.durationMinutes)}
      </AppText>
      <View style={styles.stop}>
        <AppText variant="heading" style={styles.time}>
          {formatLocalTime(offer.arrivalLocal)}
        </AppText>
        <View style={styles.flex}>
          <AppText variant="subheading">
            {offer.destination.city} ({offer.destination.iataCode})
          </AppText>
          <AppText variant="caption">
            {offer.destination.name} · {formatDate(offer.arrivalLocal)}
            {plusDays > 0 ? ` (+${plusDays} day)` : ''}
          </AppText>
        </View>
      </View>
      <AppText variant="caption">Times are local to each airport.</AppText>
      {offer.seatsLeft !== null && offer.isBookable ? (
        <AppText variant="label" color={colors.warning}>
          Only {offer.seatsLeft} seat{offer.seatsLeft === 1 ? '' : 's'} left at this price
        </AppText>
      ) : null}
    </Card>
  );
}

const styles = StyleSheet.create({
  stop: { flexDirection: 'row', gap: spacing.md, alignItems: 'flex-start' },
  time: { width: 64 },
  flex: { flex: 1 },
  duration: { marginLeft: 64 + spacing.md },
});
