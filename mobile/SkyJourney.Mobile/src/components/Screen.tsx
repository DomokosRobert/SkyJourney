import type { ReactNode } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { SafeAreaView, type Edge } from 'react-native-safe-area-context';

import { colors, spacing } from '@/theme';
import { OfflineBanner } from './OfflineBanner';

type Props = {
  children: ReactNode;
  /** Scrollable content (default). Use false for screens hosting their own FlatList. */
  scroll?: boolean;
  /** Content pinned below the scroll area, e.g. a primary action button. */
  footer?: ReactNode;
  edges?: Edge[];
};

export function Screen({ children, scroll = true, footer, edges = ['bottom'] }: Props) {
  return (
    <SafeAreaView style={styles.safeArea} edges={edges}>
      <OfflineBanner />
      {scroll ? (
        <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
          {children}
        </ScrollView>
      ) : (
        <View style={styles.fill}>{children}</View>
      )}
      {footer ? <View style={styles.footer}>{footer}</View> : null}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safeArea: { flex: 1, backgroundColor: colors.background },
  fill: { flex: 1 },
  content: { padding: spacing.lg, gap: spacing.lg },
  footer: {
    padding: spacing.lg,
    backgroundColor: colors.surface,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: colors.border,
  },
});
