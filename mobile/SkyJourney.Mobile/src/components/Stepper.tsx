import Ionicons from '@expo/vector-icons/Ionicons';
import { Pressable, StyleSheet, View } from 'react-native';

import { colors, spacing } from '@/theme';
import { AppText } from './AppText';

type Props = { label: string; hint?: string; value: number; min: number; max: number; onChange: (value: number) => void };

export function Stepper({ label, hint, value, min, max, onChange }: Props) {
  return (
    <View style={styles.row} accessible accessibilityRole="adjustable" accessibilityLabel={label} accessibilityValue={{ now: value, min, max }}
      accessibilityActions={[{ name: 'increment' }, { name: 'decrement' }]}
      onAccessibilityAction={(e) => {
        if (e.nativeEvent.actionName === 'increment' && value < max) onChange(value + 1);
        if (e.nativeEvent.actionName === 'decrement' && value > min) onChange(value - 1);
      }}>
      <View style={styles.text}>
        <AppText variant="subheading">{label}</AppText>
        {hint ? <AppText variant="caption">{hint}</AppText> : null}
      </View>
      <StepButton icon="remove" disabled={value <= min} onPress={() => onChange(value - 1)} label={`Fewer ${label}`} />
      <AppText variant="subheading" style={styles.value}>
        {value}
      </AppText>
      <StepButton icon="add" disabled={value >= max} onPress={() => onChange(value + 1)} label={`More ${label}`} />
    </View>
  );
}

function StepButton({ icon, disabled, onPress, label }: { icon: 'add' | 'remove'; disabled: boolean; onPress: () => void; label: string }) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={onPress}
      hitSlop={8}
      style={[styles.button, disabled && styles.disabled]}>
      <Ionicons name={icon} size={20} color={disabled ? colors.disabled : colors.primary} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  text: { flex: 1 },
  value: { minWidth: 24, textAlign: 'center' },
  button: {
    width: 36,
    height: 36,
    borderRadius: 18,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.surface,
  },
  disabled: { backgroundColor: colors.background },
});
