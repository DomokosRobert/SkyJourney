import { StyleSheet, View } from 'react-native';

import { useIsOnline } from '@/lib/network';
import { colors, spacing } from '@/theme';
import { AppText } from './AppText';

export function OfflineBanner() {
  const online = useIsOnline();
  if (online) {
    return null;
  }

  return (
    <View style={styles.banner} accessibilityRole="alert">
      <AppText variant="label" color={colors.warning}>
        You are offline. Showing saved data where available.
      </AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  banner: { backgroundColor: colors.warningSoft, paddingVertical: spacing.sm, paddingHorizontal: spacing.lg },
});
