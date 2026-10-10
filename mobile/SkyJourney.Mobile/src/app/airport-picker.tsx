import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { FlatList, Pressable, StyleSheet, View } from 'react-native';

import { useAirportSearch, useDestinations } from '@/api/flights';
import type { Airport } from '@/api/types';
import { useBookingDraft } from '@/booking/BookingDraftContext';
import { AppText, EmptyState, ErrorState, LoadingState, TextField } from '@/components';
import { useDebouncedValue } from '@/lib/useDebouncedValue';
import { colors, spacing } from '@/theme';

export default function AirportPickerScreen() {
  const { field } = useLocalSearchParams<{ field: 'origin' | 'destination' }>();
  const { criteria, updateCriteria } = useBookingDraft();
  const [text, setText] = useState('');
  const query = useDebouncedValue(text.trim());

  const isDestination = field === 'destination';
  const other = isDestination ? criteria.origin : criteria.destination;
  // With no search text, suggest the airports actually served from the chosen origin.
  const showServed = isDestination && !!criteria.origin && query.length === 0;

  const search = useAirportSearch(query);
  const served = useDestinations(showServed ? criteria.origin?.iataCode : undefined);
  const active = showServed ? served : search;

  const airports = (active.data ?? []).filter((a) => a.iataCode !== other?.iataCode);

  const choose = (airport: Airport) => {
    updateCriteria(isDestination ? { destination: airport } : { origin: airport });
    router.back();
  };

  return (
    <View style={styles.container}>
      <View style={styles.search}>
        <TextField
          label={isDestination ? 'Flying to' : 'Flying from'}
          placeholder="City, airport or code"
          value={text}
          onChangeText={setText}
          autoFocus
          autoCorrect={false}
          autoCapitalize="none"
          returnKeyType="search"
          clearButtonMode="while-editing"
        />
        {showServed ? <AppText variant="caption">Destinations served from {criteria.origin?.city}</AppText> : null}
      </View>
      {active.isPending && !active.data ? (
        <LoadingState message="Finding airports…" />
      ) : active.isError && !active.data ? (
        <ErrorState error={active.error} onRetry={() => active.refetch()} />
      ) : (
        <FlatList
          data={airports}
          keyExtractor={(a) => a.iataCode}
          keyboardShouldPersistTaps="handled"
          ListEmptyComponent={<EmptyState icon="search-outline" title="No airports found" message="Try a city name or a 3-letter code." />}
          ItemSeparatorComponent={() => <View style={styles.separator} />}
          renderItem={({ item }) => (
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={`${item.city}, ${item.name}, ${item.iataCode}`}
              onPress={() => choose(item)}
              style={({ pressed }) => [styles.row, pressed && styles.pressed]}>
              <View style={styles.code}>
                <AppText variant="subheading" color={colors.primary}>
                  {item.iataCode}
                </AppText>
              </View>
              <View style={styles.flex}>
                <AppText variant="subheading">{item.city}</AppText>
                <AppText variant="caption" numberOfLines={1}>
                  {item.name} · {item.countryCode}
                </AppText>
              </View>
            </Pressable>
          )}
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.background },
  search: { padding: spacing.lg, gap: spacing.xs },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md, backgroundColor: colors.surface },
  pressed: { backgroundColor: colors.primarySoft },
  code: { width: 48 },
  flex: { flex: 1 },
  separator: { height: StyleSheet.hairlineWidth, backgroundColor: colors.border, marginLeft: spacing.lg },
});
