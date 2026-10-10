import Ionicons from '@expo/vector-icons/Ionicons';
import { router } from 'expo-router';
import { useState } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';

import { useBookingDraft } from '@/booking/BookingDraftContext';
import { AppText, Button, Card, Chip, DemoDataNotice, Screen, Stepper } from '@/components';
import { formatDate } from '@/lib/format';
import { MAX_PASSENGERS, validateCriteria, type CriteriaErrors } from '@/search/criteria';
import { colors, radius, spacing } from '@/theme';

export default function SearchScreen() {
  const { criteria, updateCriteria, swapAirports } = useBookingDraft();
  const [errors, setErrors] = useState<CriteriaErrors>({});

  const update = (changes: Parameters<typeof updateCriteria>[0]) => {
    setErrors({});
    updateCriteria(changes);
  };

  const onSearch = () => {
    const result = validateCriteria(criteria);
    if (!result.ok) {
      setErrors(result.errors);
      return;
    }
    router.push({ pathname: '/search/results', params: { leg: 'outbound' } });
  };

  return (
    <Screen edges={['top']} footer={<Button title="Search flights" onPress={onSearch} />}>
      <AppText variant="title">Where to next?</AppText>

      <View style={styles.tripTypes} accessibilityRole="radiogroup">
        <Chip label="Return" selected={criteria.tripType === 'return'} onPress={() => update({ tripType: 'return' })} />
        <Chip label="One way" selected={criteria.tripType === 'oneWay'} onPress={() => update({ tripType: 'oneWay', returnDate: null })} />
      </View>

      <Card>
        <Field
          label="From"
          icon="airplane-outline"
          value={criteria.origin ? `${criteria.origin.city} (${criteria.origin.iataCode})` : null}
          placeholder="City or airport"
          error={errors.origin}
          onPress={() => router.push({ pathname: '/airport-picker', params: { field: 'origin' } })}
        />
        <View style={styles.swapRow}>
          <View style={styles.divider} />
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Swap origin and destination"
            onPress={() => {
              setErrors({});
              swapAirports();
            }}
            hitSlop={8}
            style={styles.swap}>
            <Ionicons name="swap-vertical" size={20} color={colors.primary} />
          </Pressable>
        </View>
        <Field
          label="To"
          icon="location-outline"
          value={criteria.destination ? `${criteria.destination.city} (${criteria.destination.iataCode})` : null}
          placeholder="City or airport"
          error={errors.destination}
          onPress={() => router.push({ pathname: '/airport-picker', params: { field: 'destination' } })}
        />
      </Card>

      <Card>
        <View style={styles.dates}>
          <View style={styles.flex}>
            <Field
              label="Depart"
              icon="calendar-outline"
              value={criteria.departureDate ? formatDate(criteria.departureDate) : null}
              placeholder="Add date"
              error={errors.departureDate}
              onPress={() => router.push({ pathname: '/date-picker', params: { field: 'departure' } })}
            />
          </View>
          {criteria.tripType === 'return' ? (
            <View style={styles.flex}>
              <Field
                label="Return"
                icon="calendar-outline"
                value={criteria.returnDate ? formatDate(criteria.returnDate) : null}
                placeholder="Add date"
                error={errors.returnDate}
                onPress={() => router.push({ pathname: '/date-picker', params: { field: 'return' } })}
              />
            </View>
          ) : null}
        </View>
      </Card>

      <Card>
        <Stepper
          label="Adults"
          hint="12+ years"
          value={criteria.adults}
          min={1}
          max={MAX_PASSENGERS - criteria.children}
          onChange={(adults) => update({ adults })}
        />
        <Stepper
          label="Children"
          hint="2–11 years"
          value={criteria.children}
          min={0}
          max={MAX_PASSENGERS - criteria.adults}
          onChange={(children) => update({ children })}
        />
        {errors.children ? (
          <AppText variant="caption" color={colors.danger}>
            {errors.children}
          </AppText>
        ) : null}
      </Card>

      <DemoDataNotice />
    </Screen>
  );
}

type FieldProps = {
  label: string;
  icon: keyof typeof Ionicons.glyphMap;
  value: string | null;
  placeholder: string;
  error?: string;
  onPress: () => void;
};

function Field({ label, icon, value, placeholder, error, onPress }: FieldProps) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${label}: ${value ?? placeholder}`}
      accessibilityHint={error}
      onPress={onPress}
      style={({ pressed }) => [styles.field, pressed && styles.pressed]}>
      <Ionicons name={icon} size={20} color={colors.textMuted} />
      <View style={styles.flex}>
        <AppText variant="caption">{label}</AppText>
        <AppText variant="subheading" color={value ? colors.text : colors.disabled} numberOfLines={1}>
          {value ?? placeholder}
        </AppText>
        {error ? (
          <AppText variant="caption" color={colors.danger}>
            {error}
          </AppText>
        ) : null}
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  tripTypes: { flexDirection: 'row', gap: spacing.sm },
  field: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingVertical: spacing.xs, borderRadius: radius.sm },
  pressed: { backgroundColor: colors.primarySoft },
  flex: { flex: 1 },
  swapRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  divider: { flex: 1, height: StyleSheet.hairlineWidth, backgroundColor: colors.border },
  swap: {
    width: 36,
    height: 36,
    borderRadius: 18,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'center',
    justifyContent: 'center',
  },
  dates: { flexDirection: 'row', gap: spacing.lg },
});
