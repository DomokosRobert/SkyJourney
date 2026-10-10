import { StyleSheet, View } from 'react-native';

import { colors, radius, spacing } from '@/theme';
import { AppText } from './AppText';

/** A form-level error (e.g. wrong password), announced to screen readers. */
export function FormMessage({ message }: { message?: string | null }) {
  if (!message) {
    return null;
  }
  return (
    <View style={styles.container} accessibilityRole="alert" accessibilityLiveRegion="polite">
      <AppText color={colors.danger}>{message}</AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { backgroundColor: colors.dangerSoft, borderRadius: radius.md, padding: spacing.md },
});
