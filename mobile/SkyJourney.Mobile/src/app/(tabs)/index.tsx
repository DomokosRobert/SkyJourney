import { useQuery } from '@tanstack/react-query';
import { StyleSheet, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { apiRequest } from '@/api/client';
import { API_BASE_URL } from '@/api/config';
import { AppText, Card } from '@/components';
import { colors, spacing } from '@/theme';

export default function SearchHome() {
  const health = useQuery({
    queryKey: ['health'],
    queryFn: () => apiRequest<{ status: string } | string>('/health/ready', { anonymous: true }).then(() => true),
  });

  return (
    <SafeAreaView style={styles.safeArea} edges={['top']}>
      <View style={styles.content}>
        <AppText variant="title">Where to next?</AppText>
        <Card>
          <AppText variant="subheading">API connection</AppText>
          <AppText variant="caption">{API_BASE_URL}</AppText>
          <AppText color={health.isSuccess ? colors.success : health.isError ? colors.danger : colors.textMuted}>
            {health.isSuccess ? 'Connected' : health.isError ? 'Unreachable' : 'Checking…'}
          </AppText>
        </Card>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safeArea: { flex: 1, backgroundColor: colors.background },
  content: { padding: spacing.lg, gap: spacing.lg },
});
