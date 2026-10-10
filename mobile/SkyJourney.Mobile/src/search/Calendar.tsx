import { memo, useMemo } from 'react';
import { FlatList, Pressable, StyleSheet, View } from 'react-native';

import { useLowestFares } from '@/api/flights';
import { AppText } from '@/components';
import { colors, radius, spacing } from '@/theme';
import { compactPrice, firstDay, lastDay, monthGrid, monthKey, monthLabel, monthsBetween, type Month } from './calendarGrid';

type Props = {
  minDate: string;
  maxDate: string;
  /** The highlighted date or range. */
  rangeStart: string | null;
  rangeEnd: string | null;
  /** Route whose cheapest fares are shown under each date. */
  origin?: string;
  destination?: string;
  onSelect: (date: string) => void;
};

const WEEKDAYS = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'];

export function Calendar({ minDate, maxDate, rangeStart, rangeEnd, origin, destination, onSelect }: Props) {
  const months = useMemo(() => monthsBetween(minDate, maxDate), [minDate, maxDate]);
  const initialIndex = Math.max(0, months.findIndex((m) => rangeStart?.startsWith(monthKey(m))));

  return (
    <View style={styles.container}>
      <View style={styles.weekdays}>
        {WEEKDAYS.map((d) => (
          <AppText key={d} variant="caption" style={styles.weekday}>
            {d}
          </AppText>
        ))}
      </View>
      <FlatList
        data={months}
        keyExtractor={monthKey}
        initialScrollIndex={initialIndex}
        onScrollToIndexFailed={() => undefined}
        initialNumToRender={3}
        windowSize={5}
        renderItem={({ item }) => (
          <MonthView
            month={item}
            minDate={minDate}
            maxDate={maxDate}
            rangeStart={rangeStart}
            rangeEnd={rangeEnd}
            origin={origin}
            destination={destination}
            onSelect={onSelect}
          />
        )}
      />
    </View>
  );
}

type MonthProps = Omit<Props, 'minDate' | 'maxDate'> & { month: Month; minDate: string; maxDate: string };

const MonthView = memo(function MonthView({ month, minDate, maxDate, rangeStart, rangeEnd, origin, destination, onSelect }: MonthProps) {
  const from = firstDay(month) < minDate ? minDate : firstDay(month);
  const to = lastDay(month) > maxDate ? maxDate : lastDay(month);
  const fares = useLowestFares(origin, destination, from, to);

  const prices = useMemo(() => {
    const map = new Map<string, { label: string; amount: number }>();
    for (const day of fares.data?.days ?? []) {
      map.set(day.date, {
        label: compactPrice(day.pricePerPassenger.amount, day.pricePerPassenger.currency),
        amount: day.pricePerPassenger.amount,
      });
    }
    return map;
  }, [fares.data]);
  const cheapest = prices.size > 0 ? Math.min(...[...prices.values()].map((p) => p.amount)) : null;
  const pricesLoaded = !!origin && !!destination && fares.isSuccess;

  return (
    <View style={styles.month}>
      <AppText variant="subheading" style={styles.monthLabel}>
        {monthLabel(month)}
      </AppText>
      {monthGrid(month).map((week, i) => (
        <View key={i} style={styles.week}>
          {week.map((date, j) => {
            if (!date) {
              return <View key={j} style={styles.cell} />;
            }
            const outOfRange = date < minDate || date > maxDate;
            const price = prices.get(date);
            // Once prices are loaded, a day without a price has no bookable flights.
            const noFlights = pricesLoaded && !price;
            const disabled = outOfRange || noFlights;
            const isEdge = date === rangeStart || date === rangeEnd;
            const inRange = !!rangeStart && !!rangeEnd && date > rangeStart && date < rangeEnd;
            return (
              <Pressable
                key={date}
                accessibilityRole="button"
                accessibilityLabel={`${date}${price ? `, from ${price.label}` : noFlights && !outOfRange ? ', no flights' : ''}`}
                accessibilityState={{ disabled, selected: isEdge }}
                disabled={disabled}
                onPress={() => onSelect(date)}
                style={[styles.cell, inRange && styles.inRange, isEdge && styles.selected]}>
                <AppText
                  variant="label"
                  color={isEdge ? colors.textOnPrimary : disabled ? colors.disabled : colors.text}>
                  {Number(date.slice(8, 10))}
                </AppText>
                {price && !outOfRange ? (
                  <AppText
                    style={styles.price}
                    color={isEdge ? colors.textOnPrimary : price.amount === cheapest ? colors.success : colors.textMuted}>
                    {price.label}
                  </AppText>
                ) : null}
              </Pressable>
            );
          })}
        </View>
      ))}
    </View>
  );
});

const styles = StyleSheet.create({
  container: { flex: 1 },
  weekdays: {
    flexDirection: 'row',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border,
    backgroundColor: colors.surface,
  },
  weekday: { flex: 1, textAlign: 'center' },
  month: { paddingHorizontal: spacing.md, paddingBottom: spacing.lg },
  monthLabel: { paddingVertical: spacing.md, paddingHorizontal: spacing.xs },
  week: { flexDirection: 'row' },
  cell: { flex: 1, aspectRatio: 0.9, alignItems: 'center', justifyContent: 'center', borderRadius: radius.sm },
  inRange: { backgroundColor: colors.primarySoft, borderRadius: 0 },
  selected: { backgroundColor: colors.primary },
  price: { fontSize: 10, marginTop: 2 },
});
