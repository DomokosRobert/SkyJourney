import { ActivityIndicator, Pressable, StyleSheet, type PressableProps, type ViewStyle } from 'react-native';

import { colors, radius, spacing } from '@/theme';
import { AppText } from './AppText';

type Variant = 'primary' | 'secondary' | 'danger';

type Props = Omit<PressableProps, 'style' | 'children'> & {
  title: string;
  variant?: Variant;
  loading?: boolean;
  style?: ViewStyle;
};

export function Button({ title, variant = 'primary', loading = false, disabled, style, ...rest }: Props) {
  const isDisabled = disabled || loading;
  const palette = variants[variant];

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled: !!isDisabled, busy: loading }}
      disabled={isDisabled}
      style={({ pressed }) => [
        styles.base,
        { backgroundColor: pressed ? palette.pressed : palette.background, borderColor: palette.border },
        isDisabled && styles.disabled,
        style,
      ]}
      {...rest}>
      {loading ? (
        <ActivityIndicator color={palette.text} />
      ) : (
        <AppText variant="subheading" color={palette.text}>
          {title}
        </AppText>
      )}
    </Pressable>
  );
}

const variants: Record<Variant, { background: string; pressed: string; text: string; border: string }> = {
  primary: { background: colors.primary, pressed: colors.primaryPressed, text: colors.textOnPrimary, border: colors.primary },
  secondary: { background: colors.surface, pressed: colors.primarySoft, text: colors.primary, border: colors.border },
  danger: { background: colors.surface, pressed: colors.dangerSoft, text: colors.danger, border: colors.danger },
};

const styles = StyleSheet.create({
  base: {
    minHeight: 52,
    borderRadius: radius.md,
    borderWidth: 1,
    paddingHorizontal: spacing.lg,
    alignItems: 'center',
    justifyContent: 'center',
  },
  disabled: { opacity: 0.5 },
});
