import Ionicons from '@expo/vector-icons/Ionicons';
import type { ComponentProps } from 'react';
import { ActivityIndicator, StyleSheet, View } from 'react-native';

import { ApiError } from '@/api/client';
import { colors, spacing } from '@/theme';
import { AppText } from './AppText';
import { Button } from './Button';

type IconName = ComponentProps<typeof Ionicons>['name'];

export function LoadingState({ message = 'Loading…' }: { message?: string }) {
  return (
    <View style={styles.container} accessibilityLiveRegion="polite">
      <ActivityIndicator size="large" color={colors.primary} />
      <AppText variant="caption">{message}</AppText>
    </View>
  );
}

type EmptyProps = { icon?: IconName; title: string; message?: string; actionLabel?: string; onAction?: () => void };

export function EmptyState({ icon = 'airplane-outline', title, message, actionLabel, onAction }: EmptyProps) {
  return (
    <View style={styles.container}>
      <Ionicons name={icon} size={48} color={colors.disabled} />
      <AppText variant="subheading" style={styles.center}>
        {title}
      </AppText>
      {message ? (
        <AppText variant="caption" style={styles.center}>
          {message}
        </AppText>
      ) : null}
      {actionLabel && onAction ? <Button title={actionLabel} variant="secondary" onPress={onAction} /> : null}
    </View>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <View style={styles.container} accessibilityRole="alert">
      <Ionicons name="cloud-offline-outline" size={48} color={colors.danger} />
      <AppText variant="subheading" style={styles.center}>
        Something went wrong
      </AppText>
      <AppText variant="caption" style={styles.center}>
        {describeError(error)}
      </AppText>
      {onRetry ? <Button title="Try again" variant="secondary" onPress={onRetry} /> : null}
    </View>
  );
}

export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    return error.message;
  }
  return 'Please check your connection and try again.';
}

const styles = StyleSheet.create({
  container: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: spacing.xl, gap: spacing.md },
  center: { textAlign: 'center' },
});
