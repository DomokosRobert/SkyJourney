import { zodResolver } from '@hookform/resolvers/zod';
import { router } from 'expo-router';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { StyleSheet, View } from 'react-native';

import { AppText, Button, FormMessage, FormTextField, Screen } from '@/components';
import { useSession } from '@/auth/AuthProvider';
import { closeAuthModal } from '@/auth/closeAuthModal';
import { describeAuthError, fieldErrorsFor } from '@/auth/errors';
import { PASSWORD_MIN_LENGTH, registerSchema, type RegisterForm } from '@/auth/schemas';
import { spacing } from '@/theme';

const FIELDS = ['firstName', 'lastName', 'email', 'password'] as const;

export default function RegisterScreen() {
  const session = useSession();
  const [formError, setFormError] = useState<string | null>(null);
  const { control, handleSubmit, setError, formState } = useForm<RegisterForm>({
    resolver: zodResolver(registerSchema),
    defaultValues: { firstName: '', lastName: '', email: '', password: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await session.register(values);
      closeAuthModal();
    } catch (error) {
      const fieldErrors = fieldErrorsFor(error, FIELDS);
      Object.entries(fieldErrors).forEach(([field, message]) => setError(field as keyof RegisterForm, { message }));
      if (Object.keys(fieldErrors).length === 0) {
        setFormError(describeAuthError(error));
      }
    }
  });

  return (
    <Screen footer={<Button title="Create account" onPress={onSubmit} loading={formState.isSubmitting} />}>
      <AppText variant="title">Create your account</AppText>
      <AppText variant="caption">
        We only ask for what a booking needs. Passenger names are entered per booking.
      </AppText>
      <FormMessage message={formError} />
      <View style={styles.fields}>
        <FormTextField control={control} name="firstName" label="First name" autoComplete="given-name" textContentType="givenName" />
        <FormTextField control={control} name="lastName" label="Last name" autoComplete="family-name" textContentType="familyName" />
        <FormTextField
          control={control}
          name="email"
          label="E-mail"
          autoCapitalize="none"
          autoComplete="email"
          keyboardType="email-address"
          textContentType="username"
        />
        <FormTextField
          control={control}
          name="password"
          label="Password"
          secureTextEntry
          autoComplete="new-password"
          textContentType="newPassword"
          passwordRules={`minlength: ${PASSWORD_MIN_LENGTH}; required: lower; required: upper; required: digit;`}
          onSubmitEditing={onSubmit}
        />
        <AppText variant="caption">
          At least {PASSWORD_MIN_LENGTH} characters with upper- and lowercase letters and a digit.
        </AppText>
      </View>
      <Button title="I already have an account" variant="secondary" onPress={() => router.replace('/auth/login')} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  fields: { gap: spacing.md },
});
