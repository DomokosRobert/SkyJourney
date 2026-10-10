import { Pressable, StyleSheet, View } from 'react-native';

import type { SeatMap, SeatMapSeat } from '@/api/bookings';
import { AppText } from '@/components';
import { formatMoney } from '@/lib/format';
import { colors, radius, spacing } from '@/theme';
import { CATEGORY_LABELS, categoryPrices, seatRows } from './seatLayout';

const SEAT_SIZE = 36;

type Props = {
  map: SeatMap;
  /** Initials to show on seats taken by this booking's passengers, keyed by passenger id. */
  initials: Record<string, string>;
  disabled?: boolean;
  onSelect: (seat: SeatMapSeat) => void;
};

/** Cabin plan: seats in rows with the aisle in the middle, colour-coded by category and availability. */
export function SeatMapView({ map, initials, disabled = false, onSelect }: Props) {
  const letters = map.seatLetters.split('');
  const aisleAfter = Math.ceil(letters.length / 2);
  const header = [...letters.slice(0, aisleAfter), '', ...letters.slice(aisleAfter)];

  return (
    <View style={styles.container}>
      <Legend map={map} />
      <View style={styles.row}>
        <View style={styles.rowNumber} />
        {header.map((letter, i) => (
          <View key={`h${i}`} style={letter ? styles.cell : styles.aisle}>
            <AppText variant="caption">{letter}</AppText>
          </View>
        ))}
      </View>
      {seatRows(map).map(({ row, cells }) => (
        <View key={row} style={styles.row}>
          <View style={styles.rowNumber}>
            <AppText variant="caption">{row}</AppText>
          </View>
          {cells.map((seat, i) =>
            seat ? (
              <Seat
                key={seat.seatNumber}
                seat={seat}
                label={seat.passengerId ? initials[seat.passengerId] : undefined}
                disabled={disabled}
                onPress={() => onSelect(seat)}
              />
            ) : (
              <View key={`a${i}`} style={i === aisleAfter ? styles.aisle : styles.cell} />
            ),
          )}
        </View>
      ))}
    </View>
  );
}

function Seat({
  seat,
  label,
  disabled,
  onPress,
}: {
  seat: SeatMapSeat;
  label?: string;
  disabled: boolean;
  onPress: () => void;
}) {
  const occupied = seat.availability === 'Occupied';
  const selected = seat.availability === 'Selected';
  const price = seat.price.amount === 0 ? 'free' : formatMoney(seat.price);
  const state = occupied ? 'taken' : selected ? 'selected' : `available, ${price}`;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`Seat ${seat.seatNumber}, ${CATEGORY_LABELS[seat.category]}, ${state}`}
      accessibilityState={{ disabled: occupied || disabled, selected }}
      disabled={occupied || disabled}
      onPress={onPress}
      style={[
        styles.cell,
        styles.seat,
        { backgroundColor: categoryColors[seat.category] },
        occupied && styles.occupied,
        selected && styles.selected,
      ]}>
      {selected ? (
        <AppText variant="caption" color={colors.textOnPrimary}>
          {label ?? '✓'}
        </AppText>
      ) : null}
    </Pressable>
  );
}

function Legend({ map }: { map: SeatMap }) {
  return (
    <View style={styles.legend}>
      {categoryPrices(map).map(({ category, price }) => (
        <LegendItem
          key={category}
          color={categoryColors[category]}
          label={`${CATEGORY_LABELS[category]} · ${price.amount === 0 ? 'free' : formatMoney(price)}`}
        />
      ))}
      <LegendItem color={colors.primary} label="Your seats" />
      <LegendItem color={colors.border} label="Taken" />
    </View>
  );
}

function LegendItem({ color, label }: { color: string; label: string }) {
  return (
    <View style={styles.legendItem}>
      <View style={[styles.swatch, { backgroundColor: color }]} />
      <AppText variant="caption">{label}</AppText>
    </View>
  );
}

const categoryColors: Record<SeatMapSeat['category'], string> = {
  ExtraLegroom: colors.warningSoft,
  Front: colors.successSoft,
  Standard: colors.primarySoft,
};

const styles = StyleSheet.create({
  container: { alignItems: 'center', gap: spacing.xs },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs },
  rowNumber: { width: 24, alignItems: 'center' },
  cell: { width: SEAT_SIZE, height: SEAT_SIZE, alignItems: 'center', justifyContent: 'center' },
  aisle: { width: SEAT_SIZE / 2, height: SEAT_SIZE },
  seat: { borderRadius: radius.sm, borderWidth: 1, borderColor: colors.border },
  occupied: { backgroundColor: colors.border, borderColor: colors.border, opacity: 0.6 },
  selected: { backgroundColor: colors.primary, borderColor: colors.primary },
  legend: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md, marginBottom: spacing.sm, justifyContent: 'center' },
  legendItem: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs },
  swatch: { width: 14, height: 14, borderRadius: 4, borderWidth: 1, borderColor: colors.border },
});
