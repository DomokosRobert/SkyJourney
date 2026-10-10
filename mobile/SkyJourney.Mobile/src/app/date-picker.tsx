import { router, Stack, useLocalSearchParams } from 'expo-router';
import { StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useBookingDraft } from '@/booking/BookingDraftContext';
import { AppText, Button } from '@/components';
import { addDays, formatDate, toIsoDate } from '@/lib/format';
import { Calendar } from '@/search/Calendar';
import { colors, spacing } from '@/theme';

/** The API sells flights up to 365 days ahead. */
const BOOKING_HORIZON_DAYS = 364;

export default function DatePickerScreen() {
  const { field } = useLocalSearchParams<{ field: 'departure' | 'return' }>();
  const { criteria, updateCriteria } = useBookingDraft();
  const isReturn = field === 'return';

  const today = toIsoDate(new Date());
  const minDate = isReturn && criteria.departureDate ? criteria.departureDate : today;
  const maxDate = addDays(today, BOOKING_HORIZON_DAYS);

  // Return prices are for the reverse route.
  const origin = (isReturn ? criteria.destination : criteria.origin)?.iataCode;
  const destination = (isReturn ? criteria.origin : criteria.destination)?.iataCode;

  const insets = useSafeAreaInsets();

  const onSelect = (date: string) => {
    if (isReturn) {
      updateCriteria({ returnDate: date });
      router.back();
      return;
    }
    updateCriteria({ departureDate: date });
    if (criteria.tripType === 'return') {
      // Continue straight to choosing the return date.
      router.setParams({ field: 'return' });
    } else {
      router.back();
    }
  };

  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title: isReturn ? 'Return date' : 'Departure date' }} />
      <View style={styles.header}>
        <AppText variant="caption">
          {origin && destination
            ? `Lowest price per passenger, ${origin} → ${destination}. Days without flights are disabled.`
            : 'Choose airports first to see prices.'}
        </AppText>
        {isReturn && criteria.departureDate ? (
          <AppText variant="label">Departing {formatDate(criteria.departureDate)}</AppText>
        ) : null}
      </View>
      <Calendar
        minDate={minDate}
        maxDate={maxDate}
        rangeStart={criteria.departureDate}
        rangeEnd={criteria.tripType === 'return' ? criteria.returnDate : null}
        origin={origin}
        destination={destination}
        onSelect={onSelect}
      />
      {isReturn ? (
        <View style={[styles.footer, { paddingBottom: spacing.lg + insets.bottom }]}>
          <Button
            title="One way instead"
            variant="secondary"
            onPress={() => {
              updateCriteria({ tripType: 'oneWay', returnDate: null });
              router.back();
            }}
          />
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.surface },
  header: { paddingHorizontal: spacing.lg, paddingTop: spacing.md, paddingBottom: spacing.sm, gap: spacing.xs },
  footer: { padding: spacing.lg, borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border },
});
