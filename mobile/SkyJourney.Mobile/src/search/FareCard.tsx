import Ionicons from '@expo/vector-icons/Ionicons';
import { StyleSheet, View } from 'react-native';

import type { FareOffer } from '@/api/types';
import { AppText, Button, Card } from '@/components';
import { formatMoney } from '@/lib/format';
import { colors, spacing } from '@/theme';

type Props = { fare: FareOffer; passengers: number; onSelect: () => void; disabled?: boolean };

export function describeConditions(c: FareOffer['conditions']): { included: boolean; text: string }[] {
  return [
    { included: true, text: 'Personal item under the seat' },
    { included: c.includesCabinTrolley, text: c.includesCabinTrolley ? 'Cabin trolley' : 'No cabin trolley' },
    {
      included: c.checkedBaggageKg > 0,
      text: c.checkedBaggageKg > 0 ? `Checked bag (${c.checkedBaggageKg} kg)` : 'No checked bag',
    },
    {
      included: c.includesSeatSelection,
      text: c.includesSeatSelection ? 'Free seat selection' : 'Seat selection for a fee',
    },
    {
      included: c.refundPercentage > 0,
      text:
        c.refundPercentage === 0
          ? 'Non-refundable'
          : `${c.refundPercentage}% refund if cancelled 24 h+ before departure`,
    },
  ];
}

export function FareCard({ fare, passengers, onSelect, disabled }: Props) {
  return (
    <Card>
      <View style={styles.header}>
        <AppText variant="heading">{fare.family}</AppText>
        <View style={styles.price}>
          <AppText variant="heading" color={colors.primary}>
            {formatMoney(fare.pricePerPassenger)}
          </AppText>
          <AppText variant="caption">
            per passenger{passengers > 1 ? ` · ${formatMoney(fare.totalPrice)} total` : ''}
          </AppText>
        </View>
      </View>
      {describeConditions(fare.conditions).map((item) => (
        <View key={item.text} style={styles.condition}>
          <Ionicons
            name={item.included ? 'checkmark-circle' : 'close-circle-outline'}
            size={18}
            color={item.included ? colors.success : colors.disabled}
          />
          <AppText variant="body" color={item.included ? colors.text : colors.textMuted}>
            {item.text}
          </AppText>
        </View>
      ))}
      <Button title={`Select ${fare.family}`} onPress={onSelect} disabled={disabled} />
    </Card>
  );
}

const styles = StyleSheet.create({
  header: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start' },
  price: { alignItems: 'flex-end' },
  condition: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
});
