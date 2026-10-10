import { router } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, View } from 'react-native';

import { useAuthState, useSession } from '@/auth/AuthProvider';
import { AppText, Button, Card, EmptyState, LoadingState, Screen } from '@/components';
import { spacing } from '@/theme';

export default function ProfileScreen() {
  const auth = useAuthState();
  const session = useSession();
  const [signingOut, setSigningOut] = useState(false);

  if (auth.status === 'loading') {
    return (
      <Screen scroll={false}>
        <LoadingState />
      </Screen>
    );
  }

  if (auth.status === 'signedOut') {
    return (
      <Screen scroll={false}>
        <View style={styles.signedOut}>
          <EmptyState icon="person-circle-outline" title="Your profile" message="Sign in to book flights and manage your trips." />
          <Button title="Sign in" onPress={() => router.push('/auth/login')} />
          <Button title="Create an account" variant="secondary" onPress={() => router.push('/auth/register')} />
        </View>
      </Screen>
    );
  }

  const { user } = auth;
  const onSignOut = async () => {
    setSigningOut(true);
    try {
      await session.signOut();
    } finally {
      setSigningOut(false);
    }
  };

  return (
    <Screen edges={['top']}>
      <AppText variant="title">
        {user.firstName} {user.lastName}
      </AppText>
      <Card>
        <AppText variant="label">E-mail</AppText>
        <AppText>{user.email}</AppText>
        {user.roles.includes('Admin') ? <AppText variant="caption">Administrator</AppText> : null}
      </Card>
      <Button title="Sign out" variant="danger" loading={signingOut} onPress={onSignOut} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  signedOut: { flex: 1, justifyContent: 'center', padding: spacing.lg, gap: spacing.md },
});
