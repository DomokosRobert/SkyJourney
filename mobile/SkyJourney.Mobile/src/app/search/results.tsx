import Ionicons from '@expo/vector-icons/Ionicons';
import { router, Stack, useLocalSearchParams } from 'expo-router';
import { useMemo, useState } from 'react';
import { FlatList, Pressable, RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useFlightSearch } from '@/api/flights';
import type { FlightOffer } from '@/api/types';
import { useBookingDraft, type Leg } from '@/booking/BookingDraftContext';
import { AppText, Chip, DemoDataNotice, EmptyState, ErrorState, LoadingState, OfflineBanner } from '@/components';
import { addDays, formatDate, formatLocalTime, formatMoney, toIsoDate } from '@/lib/format';
import { passengerSummary, toSearchParams, validateCriteria } from '@/search/criteria';
import { FlightCard } from '@/search/FlightCard';
import {
  applyFilters,
  isValidReturn,
  MIN_STAY_MINUTES,
  noFilters,
  priceSteps,
  sortOffers,
  type ResultFilters,
  type SortOrder,
  type TimeOfDay,
} from '@/search/results';
import { colors, radius, spacing } from '@/theme';

const SORTS: { key: SortOrder; label: string }[] = [
  { key: 'price', label: 'Cheapest' },
  { key: 'departure', label: 'Earliest' },
  { key: 'arrival', label: 'Arrives first' },
  { key: 'duration', label: 'Shortest' },
];

const TIMES: { key: TimeOfDay; label: string }[] = [
  { key: 'morning', label: 'Morning' },
  { key: 'afternoon', label: 'Afternoon' },
  { key: 'evening', label: 'Evening' },
];

export default function ResultsScreen() {
  const { leg = 'outbound' } = useLocalSearchParams<{ leg?: Leg }>();
  const { criteria, outbound, updateCriteria } = useBookingDraft();
  const [sort, setSort] = useState<SortOrder>('price');
  const [filters, setFilters] = useState<ResultFilters>(noFilters);

  const valid = validateCriteria(criteria).ok;
  const params = valid ? toSearchParams(criteria) : null;
  const search = useFlightSearch(params);
  const isReturnLeg = leg === 'return';

  const offers = useMemo(
    () => (isReturnLeg ? search.data?.return ?? [] : search.data?.outbound ?? []),
    [search.data, isReturnLeg],
  );
  const visible = useMemo(() => sortOffers(applyFilters(offers, filters), sort), [offers, filters, sort]);
  const steps = useMemo(() => priceSteps(offers), [offers]);

  if (!params) {
    return <EmptyState title="Search details missing" message="Start a new search." actionLabel="New search" onAction={() => router.dismissAll()} />;
  }

  const date = isReturnLeg ? params.returnDate! : params.departureDate;
  const from = isReturnLeg ? params.destination : params.origin;
  const to = isReturnLeg ? params.origin : params.destination;
  const today = toIsoDate(new Date());

  const shiftDate = (days: number) => {
    const next = addDays(params.departureDate, days);
    updateCriteria({ departureDate: next });
  };
  const canShiftBack = !isReturnLeg && params.departureDate > today;
  const canShiftForward =
    !isReturnLeg && (criteria.tripType === 'oneWay' || !params.returnDate || addDays(params.departureDate, 1) <= params.returnDate);

  const toggleTime = (key: TimeOfDay) =>
    setFilters((f) => ({
      ...f,
      timesOfDay: f.timesOfDay.includes(key) ? f.timesOfDay.filter((t) => t !== key) : [...f.timesOfDay, key],
    }));
  const hasFilters = filters.timesOfDay.length > 0 || filters.maxPrice !== null;

  const open = (offer: FlightOffer) =>
    router.push({ pathname: '/search/flight/[flightId]', params: { flightId: offer.flightId, leg } });

  const header = (
    <View style={styles.header}>
      {isReturnLeg && outbound ? (
        <View style={styles.selected}>
          <Ionicons name="checkmark-circle" size={18} color={colors.success} />
          <AppText variant="caption" style={styles.flex}>
            Outbound {outbound.flight.flightNumber} · {formatDate(outbound.flight.departureLocal)}{' '}
            {formatLocalTime(outbound.flight.departureLocal)} · {outbound.fare.family}{' '}
            {formatMoney(outbound.fare.pricePerPassenger)}
          </AppText>
        </View>
      ) : null}
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.chips}>
        {SORTS.map((s) => (
          <Chip key={s.key} label={s.label} selected={sort === s.key} onPress={() => setSort(s.key)} />
        ))}
      </ScrollView>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.chips}>
        {TIMES.map((t) => (
          <Chip key={t.key} label={t.label} selected={filters.timesOfDay.includes(t.key)} onPress={() => toggleTime(t.key)} />
        ))}
        {steps.map((step) => (
          <Chip
            key={step}
            label={`Under ${formatMoney({ amount: step, currency: offers[0]?.lowestPricePerPassenger?.currency ?? 'EUR' })}`}
            selected={filters.maxPrice === step}
            onPress={() => setFilters((f) => ({ ...f, maxPrice: f.maxPrice === step ? null : step }))}
          />
        ))}
      </ScrollView>
      {search.isSuccess ? (
        <AppText variant="caption">
          {visible.length} of {offers.length} flights · prices per passenger
          {isReturnLeg ? ` · return must leave ${MIN_STAY_MINUTES / 60} h+ after you land` : ''}
        </AppText>
      ) : null}
    </View>
  );

  return (
    <SafeAreaView style={styles.container} edges={['bottom']}>
      <Stack.Screen options={{ title: `${from} → ${to}` }} />
      <OfflineBanner />
      <View style={styles.dateBar}>
        <DayButton icon="chevron-back" label="Previous day" disabled={!canShiftBack} onPress={() => shiftDate(-1)} />
        <View style={styles.dateText}>
          <AppText variant="subheading">{formatDate(date)}</AppText>
          <AppText variant="caption">
            {isReturnLeg ? 'Return' : 'Outbound'} · {passengerSummary(params.adults, params.children)}
          </AppText>
        </View>
        <DayButton icon="chevron-forward" label="Next day" disabled={!canShiftForward} onPress={() => shiftDate(1)} />
      </View>

      {search.isPending ? (
        <LoadingState message="Searching flights…" />
      ) : search.isError ? (
        <ErrorState error={search.error} onRetry={() => search.refetch()} />
      ) : (
        <FlatList
          data={visible}
          keyExtractor={(o) => o.flightId}
          contentContainerStyle={styles.list}
          ListHeaderComponent={header}
          ListFooterComponent={offers.length > 0 ? <DemoDataNotice /> : null}
          refreshControl={<RefreshControl refreshing={search.isRefetching} onRefresh={() => search.refetch()} />}
          ListEmptyComponent={
            offers.length === 0 ? (
              <EmptyState
                icon="calendar-outline"
                title="No flights on this date"
                message={`There are no direct SkyJourney flights from ${from} to ${to} on ${formatDate(date)}.`}
                actionLabel="Change search"
                onAction={() => router.back()}
              />
            ) : (
              <EmptyState
                icon="funnel-outline"
                title="No flights match your filters"
                actionLabel="Clear filters"
                onAction={() => setFilters(noFilters)}
              />
            )
          }
          renderItem={({ item }) => (
            <FlightCard
              offer={item}
              onPress={() => open(item)}
              disabledReason={
                isReturnLeg && !isValidReturn(outbound?.flight, item) ? 'Departs too soon after your outbound flight lands' : undefined
              }
            />
          )}
        />
      )}
      {hasFilters && visible.length > 0 ? (
        <Pressable accessibilityRole="button" onPress={() => setFilters(noFilters)} style={styles.clear}>
          <AppText variant="label" color={colors.primary}>
            Clear filters
          </AppText>
        </Pressable>
      ) : null}
    </SafeAreaView>
  );
}

function DayButton({ icon, label, disabled, onPress }: { icon: 'chevron-back' | 'chevron-forward'; label: string; disabled: boolean; onPress: () => void }) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={onPress}
      hitSlop={8}
      style={styles.dayButton}>
      <Ionicons name={icon} size={22} color={disabled ? colors.background : colors.primary} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.background },
  dateBar: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.surface,
    paddingVertical: spacing.sm,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border,
  },
  dateText: { flex: 1, alignItems: 'center' },
  dayButton: { paddingHorizontal: spacing.lg, paddingVertical: spacing.sm },
  list: { padding: spacing.lg, gap: spacing.md, flexGrow: 1 },
  header: { gap: spacing.sm, marginBottom: spacing.xs },
  chips: { gap: spacing.sm },
  selected: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    padding: spacing.sm,
    borderRadius: radius.sm,
    backgroundColor: colors.successSoft,
  },
  flex: { flex: 1 },
  clear: { alignItems: 'center', padding: spacing.md, backgroundColor: colors.surface },
});
