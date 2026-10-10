import { zodResolver } from '@hookform/resolvers/zod';
import { router } from 'expo-router';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { StyleSheet, View } from 'react-native';

import { AppText, Button, FormMessage, FormTextField, Screen } from '@/components';
import { useSession } from '@/auth/AuthProvider';
import { closeAuthModal } from '@/auth/closeAuthModal';
import { describeAuthError, fieldErrorsFor } from '@/auth/errors';
import { loginSchema, type LoginForm } from '@/auth/schemas';
import { spacing } from '@/theme';

const FIELDS = ['email', 'password'] as const;

export default function LoginScreen() {
  const session = useSession();
  const [formError, setFormError] = useState<string | null>(null);
  const { control, handleSubmit, setError, formState } = useForm<LoginForm>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await session.signIn(values);
      closeAuthModal();
    } catch (error) {
      const fieldErrors = fieldErrorsFor(error, FIELDS);
      Object.entries(fieldErrors).forEach(([field, message]) => setError(field as keyof LoginForm, { message }));
      if (Object.keys(fieldErrors).length === 0) {
        setFormError(describeAuthError(error));
      }
    }
  });

  return (
    <Screen
      footer={<Button title="Sign in" onPress={onSubmit} loading={formState.isSubmitting} />}>
      <AppText variant="title">Welcome back</AppText>
      <AppText variant="caption">Sign in to book flights and see your trips.</AppText>
      <FormMessage message={formError} />
      <View style={styles.fields}>
        <FormTextField
          control={control}
          name="email"
          label="E-mail"
          autoCapitalize="none"
          autoComplete="email"
          keyboardType="email-address"
          textContentType="username"
          returnKeyType="next"
        />
        <FormTextField
          control={control}
          name="password"
          label="Password"
          secureTextEntry
          autoComplete="current-password"
          textContentType="password"
          returnKeyType="go"
          onSubmitEditing={onSubmit}
        />
      </View>
      <Button title="Create an account" variant="secondary" onPress={() => router.replace('/auth/register')} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  fields: { gap: spacing.md },
});
