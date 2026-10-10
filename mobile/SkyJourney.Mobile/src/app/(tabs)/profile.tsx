import { zodResolver } from '@hookform/resolvers/zod';
import { router } from 'expo-router';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { StyleSheet, View } from 'react-native';

import { authApi, type UserProfile } from '@/api/auth';
import { useAuthState, useSession } from '@/auth/AuthProvider';
import { describeAuthError, fieldErrorsFor } from '@/auth/errors';
import { profileSchema, type ProfileForm } from '@/auth/schemas';
import { AppText, Button, Card, EmptyState, FormMessage, FormTextField, LoadingState, Screen } from '@/components';
import { colors, spacing } from '@/theme';

const FIELDS = ['firstName', 'lastName'] as const;

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
      <EditName user={user} />
      <Button title="My trips" variant="secondary" onPress={() => router.navigate('/trips')} />
      <AppText variant="caption" color={colors.textMuted}>
        SkyJourney stores only your name, e-mail address and the passenger names you enter for bookings. Payments are
        simulated; no card details are ever collected.
      </AppText>
      <Button title="Sign out" variant="danger" loading={signingOut} onPress={onSignOut} />
    </Screen>
  );
}

/** The account holder's name, used to pre-fill the first passenger of new bookings. */
function EditName({ user }: { user: UserProfile }) {
  const session = useSession();
  const [editing, setEditing] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const { control, handleSubmit, setError, reset, formState } = useForm<ProfileForm>({
    resolver: zodResolver(profileSchema),
    values: { firstName: user.firstName, lastName: user.lastName },
  });

  const onSave = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await session.updateUser(await authApi.updateMe(values));
      setEditing(false);
    } catch (error) {
      const fieldErrors = fieldErrorsFor(error, FIELDS);
      Object.entries(fieldErrors).forEach(([field, message]) => setError(field as keyof ProfileForm, { message }));
      if (Object.keys(fieldErrors).length === 0) {
        setFormError(describeAuthError(error));
      }
    }
  });

  if (!editing) {
    return <Button title="Edit name" variant="secondary" onPress={() => setEditing(true)} />;
  }

  return (
    <Card>
      <FormMessage message={formError} />
      <FormTextField control={control} name="firstName" label="First name" autoComplete="given-name" textContentType="givenName" />
      <FormTextField control={control} name="lastName" label="Last name" autoComplete="family-name" textContentType="familyName" onSubmitEditing={onSave} />
      <Button title="Save" loading={formState.isSubmitting} onPress={onSave} />
      <Button
        title="Cancel"
        variant="secondary"
        disabled={formState.isSubmitting}
        onPress={() => {
          reset();
          setFormError(null);
          setEditing(false);
        }}
      />
    </Card>
  );
}

const styles = StyleSheet.create({
  signedOut: { flex: 1, justifyContent: 'center', padding: spacing.lg, gap: spacing.md },
});
