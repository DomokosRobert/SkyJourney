import Ionicons from '@expo/vector-icons/Ionicons';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { StyleSheet, View } from 'react-native';

import { bookingKeys } from '@/api/bookings';
import { AppText } from '@/components';
import { colors, radius, spacing } from '@/theme';
import { formatCountdown, holdRemainingMs, useNow } from './hold';

const WARNING_MS = 5 * 60_000;

/** Time left to pay. When it runs out, the booking is reloaded so the screen shows the server's verdict. */
export function HoldBanner({ reference, expiresAt }: { reference: string; expiresAt: string }) {
  const queryClient = useQueryClient();
  const remaining = holdRemainingMs(expiresAt, useNow());
  const lapsed = remaining === 0;

  useEffect(() => {
    if (lapsed) {
      void queryClient.invalidateQueries({ queryKey: bookingKeys.detail(reference) });
    }
  }, [lapsed, queryClient, reference]);

  const urgent = remaining < WARNING_MS;
  return (
    <View
      style={[styles.banner, urgent && styles.urgent]}
      accessibilityRole="timer"
      accessibilityLabel={`Seats held for ${formatCountdown(remaining)} minutes`}>
      <Ionicons name="time-outline" size={18} color={urgent ? colors.danger : colors.warning} />
      <AppText variant="label" color={urgent ? colors.danger : colors.warning}>
        {lapsed ? 'Your hold has expired' : `Seats held for ${formatCountdown(remaining)}`}
      </AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  banner: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    padding: spacing.md,
    borderRadius: radius.md,
    backgroundColor: colors.warningSoft,
  },
  urgent: { backgroundColor: colors.dangerSoft },
});
