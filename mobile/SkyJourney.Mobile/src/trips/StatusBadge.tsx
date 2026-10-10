import { StyleSheet, View } from 'react-native';

import type { BookingStatus } from '@/api/bookings';
import { AppText } from '@/components';
import { radius, spacing } from '@/theme';
import { statusPresentation } from './status';

export function StatusBadge({ status, isUpcoming }: { status: BookingStatus; isUpcoming: boolean }) {
  const { label, color, background } = statusPresentation(status, isUpcoming);
  return (
    <View style={[styles.badge, { backgroundColor: background }]}>
      <AppText variant="caption" color={color}>
        {label}
      </AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  badge: { paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: radius.pill, alignSelf: 'flex-start' },
});
