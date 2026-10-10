import { Redirect, router } from 'expo-router';
import type { ReactNode } from 'react';

import { useBooking, type Booking } from '@/api/bookings';
import { EmptyState, ErrorState, LoadingState } from '@/components';

/**
 * Loads a booking for a step of the booking flow and renders `children` only while it still awaits payment.
 * A confirmed booking goes to its confirmation; an expired or cancelled one explains what happened.
 */
export function PendingBookingGate({ reference, children }: { reference: string; children: (booking: Booking) => ReactNode }) {
  const booking = useBooking(reference);

  if (booking.isPending) {
    return <LoadingState message="Loading your booking…" />;
  }
  if (booking.isError) {
    return <ErrorState error={booking.error} onRetry={() => booking.refetch()} />;
  }

  switch (booking.data.status) {
    case 'PendingPayment':
      return <>{children(booking.data)}</>;
    case 'Confirmed':
      return <Redirect href={{ pathname: '/booking/[reference]/confirmation', params: { reference } }} />;
    case 'Expired':
      return (
        <EmptyState
          icon="time-outline"
          title="Your hold has expired"
          message="The seats were released because the booking was not paid within 20 minutes. Please search again."
          actionLabel="New search"
          onAction={() => router.dismissAll()}
        />
      );
    case 'Cancelled':
      return (
        <EmptyState
          title="This booking was cancelled"
          actionLabel="New search"
          onAction={() => router.dismissAll()}
        />
      );
  }
}
