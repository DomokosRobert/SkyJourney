import Ionicons from '@expo/vector-icons/Ionicons';
import { Pressable, StyleSheet, View } from 'react-native';

import type { FlightOffer } from '@/api/types';
import { AppText } from '@/components';
import { dayOffset, formatDuration, formatLocalTime, formatMoney } from '@/lib/format';
import { colors, radius, spacing } from '@/theme';

type Props = { offer: FlightOffer; onPress: () => void; disabledReason?: string };

export function FlightCard({ offer, onPress, disabledReason }: Props) {
  const plusDays = dayOffset(offer.departureLocal, offer.arrivalLocal);
  const unavailable = !offer.isBookable || !!disabledReason;
  const departure = formatLocalTime(offer.departureLocal);
  const arrival = formatLocalTime(offer.arrivalLocal);

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled: unavailable }}
      accessibilityLabel={`${offer.flightNumber}, departs ${departure}, arrives ${arrival}${plusDays > 0 ? ' next day' : ''}, ${formatDuration(offer.durationMinutes)}, ${
        offer.lowestPricePerPassenger ? `from ${formatMoney(offer.lowestPricePerPassenger)}` : 'sold out'
      }`}
      disabled={unavailable}
      onPress={onPress}
      style={({ pressed }) => [styles.card, pressed && styles.pressed, unavailable && styles.unavailable]}>
      <View style={styles.timeline}>
        <View>
          <AppText variant="heading">{departure}</AppText>
          <AppText variant="caption">{offer.origin.iataCode}</AppText>
        </View>
        <View style={styles.middle}>
          <AppText variant="caption">{formatDuration(offer.durationMinutes)}</AppText>
          <View style={styles.line}>
            <View style={styles.rule} />
            <Ionicons name="airplane" size={14} color={colors.textMuted} />
            <View style={styles.rule} />
          </View>
          <AppText variant="caption">Direct</AppText>
        </View>
        <View style={styles.right}>
          <AppText variant="heading">
            {arrival}
            {plusDays > 0 ? <AppText variant="caption" color={colors.warning}>{` +${plusDays}`}</AppText> : null}
          </AppText>
          <AppText variant="caption">{offer.destination.iataCode}</AppText>
        </View>
      </View>
      <View style={styles.footer}>
        <AppText variant="caption">
          {offer.flightNumber} · {offer.aircraftName}
        </AppText>
        <View style={styles.right}>
          {offer.lowestPricePerPassenger && offer.isBookable ? (
            <>
              <AppText variant="caption">from</AppText>
              <AppText variant="subheading" color={colors.primary}>
                {formatMoney(offer.lowestPricePerPassenger)}
              </AppText>
            </>
          ) : (
            <AppText variant="label" color={colors.danger}>
              Sold out
            </AppText>
          )}
        </View>
      </View>
      {offer.seatsLeft !== null && offer.isBookable ? (
        <AppText variant="caption" color={colors.warning}>
          {offer.seatsLeft === 1 ? 'Only 1 seat left' : `Only ${offer.seatsLeft} seats left`}
        </AppText>
      ) : null}
      {disabledReason ? (
        <AppText variant="caption" color={colors.danger}>
          {disabledReason}
        </AppText>
      ) : null}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.lg,
    padding: spacing.lg,
    gap: spacing.md,
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: colors.border,
  },
  pressed: { backgroundColor: colors.primarySoft },
  unavailable: { opacity: 0.55 },
  timeline: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  middle: { flex: 1, alignItems: 'center' },
  line: { flexDirection: 'row', alignItems: 'center', alignSelf: 'stretch', gap: spacing.xs },
  rule: { flex: 1, height: 1, backgroundColor: colors.border },
  right: { alignItems: 'flex-end' },
  footer: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-end' },
});
