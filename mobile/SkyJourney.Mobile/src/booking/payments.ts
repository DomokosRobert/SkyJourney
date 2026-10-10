import { ApiError } from '@/api/client';

/**
 * Simulated payment methods. The API's simulated gateway decides the outcome from these test tokens
 * (modelled on Stripe test mode). The app never asks for, sends or stores card details.
 */
export const TEST_PAYMENT_METHODS = [
  { token: 'sim_success', label: 'Test card · approved', description: 'The payment succeeds and the booking is confirmed.' },
  { token: 'sim_declined', label: 'Test card · declined', description: 'The bank declines the payment.' },
  {
    token: 'sim_insufficient_funds',
    label: 'Test card · insufficient funds',
    description: 'The payment fails for lack of funds.',
  },
] as const;

export type TestPaymentToken = (typeof TEST_PAYMENT_METHODS)[number]['token'];

/** Whether a failed request may have reached the server, so a retry must reuse its idempotency key. */
export function isRetryableFailure(error: unknown): boolean {
  return !(error instanceof ApiError) || error.isNetworkError || error.status >= 500;
}

/** User-facing text for booking and payment errors from the API. */
export function describeBookingError(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return 'Something went wrong. Please try again.';
  }
  switch (error.code) {
    case 'booking_expired':
      return 'Your booking hold has expired and the seats were released. Please search again.';
    case 'payment_in_progress':
      return 'A payment for this booking is still being processed. Wait a moment, then try again.';
    case 'booking_not_payable':
      return 'This booking can no longer be paid.';
    case 'insufficient_capacity':
      return 'Sorry, there are no longer enough seats on this flight. Please choose another flight.';
    case 'flight_not_bookable':
    case 'fare_unavailable':
      return 'This flight or fare is no longer available. Please choose another one.';
    case 'seat_unavailable':
      return 'Someone else just took that seat. Please choose another one.';
    case 'seats_locked':
      return 'Seats can only be changed before payment.';
    case 'rate_limited':
      return 'Too many attempts. Wait a minute, then try again.';
    default:
      return error.message;
  }
}
