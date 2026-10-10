import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { router } from 'expo-router';
import { useState } from 'react';
import { useFieldArray, useForm } from 'react-hook-form';
import { StyleSheet, View } from 'react-native';

import { bookingKeys, bookingsApi, type CreateBookingRequest } from '@/api/bookings';
import { useAuthState } from '@/auth/AuthProvider';
import { useBookingDraft } from '@/booking/BookingDraftContext';
import { describeBookingError, isRetryableFailure } from '@/booking/payments';
import {
  initialPassengers,
  passengerLabel,
  passengersSchema,
  toCreateBookingRequest,
  type PassengersForm,
} from '@/booking/passengers';
import { AppText, Button, Card, EmptyState, FormMessage, FormTextField, Screen } from '@/components';
import { spacing } from '@/theme';

export default function PassengersScreen() {
  const auth = useAuthState();
  const { criteria, outbound, inbound, createBookingKeys } = useBookingDraft();
  const queryClient = useQueryClient();
  const [formError, setFormError] = useState<string | null>(null);

  const accountHolder = auth.status === 'signedIn' ? auth.user : undefined;
  const { control, handleSubmit, getValues } = useForm<PassengersForm>({
    resolver: zodResolver(passengersSchema),
    defaultValues: initialPassengers(criteria.adults, criteria.children, accountHolder),
  });
  const { fields } = useFieldArray({ control, name: 'passengers' });

  const create = useMutation({
    mutationFn: (request: CreateBookingRequest) => bookingsApi.create(request, createBookingKeys.keyFor(request)),
    onSuccess: (booking) => {
      queryClient.setQueryData(bookingKeys.detail(booking.reference), booking);
      void queryClient.invalidateQueries({ queryKey: bookingKeys.all, refetchType: 'none' });
      router.replace({ pathname: '/booking/[reference]/seats', params: { reference: booking.reference } });
    },
    onError: (error) => {
      // A definite rejection (sold out, validation) means nothing was created; a lost response keeps its key for the retry.
      if (!isRetryableFailure(error)) {
        createBookingKeys.reset();
      }
      setFormError(describeBookingError(error));
    },
  });

  if (auth.status !== 'signedIn') {
    return (
      <EmptyState
        icon="person-circle-outline"
        title="Sign in to book"
        message="Your booking is saved to your account, so you can find it again in Trips."
        actionLabel="Sign in"
        onAction={() => router.push('/auth/login')}
      />
    );
  }

  if (!outbound || (criteria.tripType === 'return' && !inbound)) {
    return (
      <EmptyState
        title="No flights selected"
        message="Search and choose your flights first."
        actionLabel="New search"
        onAction={() => router.dismissAll()}
      />
    );
  }

  const onSubmit = handleSubmit((form) => {
    setFormError(null);
    create.mutate(toCreateBookingRequest(outbound, inbound, form));
  });

  return (
    <Screen footer={<Button title="Continue to seats" onPress={onSubmit} loading={create.isPending} />}>
      <AppText variant="heading">Who is travelling?</AppText>
      <AppText variant="caption">
        Enter names as they appear on each traveller&apos;s passport or ID. We only ask for what the booking needs.
      </AppText>
      <FormMessage message={formError} />
      {fields.map((field, index) => (
        <Card key={field.id}>
          <AppText variant="subheading">{passengerLabel(getValues('passengers'), index)}</AppText>
          <View style={styles.fields}>
            <FormTextField
              control={control}
              name={`passengers.${index}.firstName`}
              label="First name"
              autoCapitalize="words"
              autoComplete={index === 0 ? 'given-name' : 'off'}
              textContentType={index === 0 ? 'givenName' : 'none'}
            />
            <FormTextField
              control={control}
              name={`passengers.${index}.lastName`}
              label="Last name"
              autoCapitalize="words"
              autoComplete={index === 0 ? 'family-name' : 'off'}
              textContentType={index === 0 ? 'familyName' : 'none'}
            />
          </View>
        </Card>
      ))}
      <AppText variant="caption">
        Your seats are held for 20 minutes once you continue. You can choose seats next or skip that step.
      </AppText>
    </Screen>
  );
}

const styles = StyleSheet.create({
  fields: { gap: spacing.md },
});
