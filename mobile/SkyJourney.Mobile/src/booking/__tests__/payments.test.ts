import { ApiError } from '@/api/client';
import { describeBookingError, isRetryableFailure } from '../payments';

describe('isRetryableFailure', () => {
  it.each([
    ['a network error', new ApiError('offline', 0, 'network_error'), true],
    ['a server error', new ApiError('boom', 503, 'unavailable'), true],
    ['an unknown error', new Error('?'), true],
    ['a conflict', new ApiError('x', 409, 'booking_expired'), false],
    ['a validation error', new ApiError('x', 400, 'validation_failed'), false],
  ])('treats %s as retryable: %s', (_, error, expected) => {
    expect(isRetryableFailure(error)).toBe(expected);
  });
});

describe('describeBookingError', () => {
  it('explains known booking conflicts', () => {
    expect(describeBookingError(new ApiError('x', 409, 'seat_unavailable'))).toBe(
      'Someone else just took that seat. Please choose another one.',
    );
    expect(describeBookingError(new ApiError('x', 409, 'booking_expired'))).toMatch(/hold has expired/);
  });

  it('falls back to the server message for other API errors', () => {
    expect(describeBookingError(new ApiError('Flight not found.', 404, 'not_found'))).toBe('Flight not found.');
  });
});
