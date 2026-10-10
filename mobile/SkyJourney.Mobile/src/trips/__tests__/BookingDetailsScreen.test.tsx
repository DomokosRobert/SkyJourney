import { fireEvent, screen } from '@testing-library/react-native';

import { bookingsApi, type Booking } from '@/api/bookings';
import { ApiError } from '@/api/client';
import BookingDetailsScreen from '@/app/trips/[reference]';
import { booking } from '@/test/fixtures';
import { renderWithProviders } from '@/test/renderWithProviders';

jest.mock('expo-router', () => ({
  router: { push: jest.fn() },
  useLocalSearchParams: () => ({ reference: 'SJ7K2Q' }),
}));

/** A booking whose flight leaves in ten days, whatever today's date is. */
function upcoming(overrides: Partial<Booking> = {}): Booking {
  const base = booking(overrides);
  const departure = new Date(Date.now() + 10 * 24 * 3_600_000).toISOString();
  return {
    ...base,
    segments: base.segments.map((s) => ({ ...s, flight: { ...s.flight, departureUtc: departure } })),
  };
}

const confirmed = () => upcoming({ status: 'Confirmed', expiresAt: null, confirmedAt: '2026-10-10T12:05:00Z' });

describe('BookingDetailsScreen', () => {
  afterEach(() => jest.restoreAllMocks());

  it('shows the refund from the server quote and cancels after confirmation', async () => {
    jest.spyOn(bookingsApi, 'get').mockResolvedValue(confirmed());
    jest
      .spyOn(bookingsApi, 'cancellationQuote')
      .mockResolvedValue({ canCancel: true, refund: { amount: 100, currency: 'EUR' }, reasonCode: null, reason: null });
    const cancel = jest
      .spyOn(bookingsApi, 'cancel')
      .mockResolvedValue(
        upcoming({ status: 'Cancelled', expiresAt: null, refundedAmount: { amount: 100, currency: 'EUR' } }),
      );
    await renderWithProviders(<BookingDetailsScreen />);

    await fireEvent.press(await screen.findByRole('button', { name: 'Cancel booking' }));
    expect(await screen.findByText(/refunded €100\.00/)).toBeOnTheScreen();
    expect(cancel).not.toHaveBeenCalled();

    await fireEvent.press(screen.getByRole('button', { name: 'Yes, cancel booking' }));

    expect(await screen.findByText('Refunded (simulated)')).toBeOnTheScreen();
    expect(screen.getByText('Cancelled')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Cancel booking' })).toBeNull();
    expect(cancel).toHaveBeenCalledWith('SJ7K2Q');
  });

  it('explains why a booking can no longer be cancelled', async () => {
    jest.spyOn(bookingsApi, 'get').mockResolvedValue(confirmed());
    jest.spyOn(bookingsApi, 'cancellationQuote').mockResolvedValue({
      canCancel: false,
      refund: null,
      reasonCode: 'cancellation_window_closed',
      reason: 'Bookings can be cancelled until 24 hours before departure.',
    });
    const cancel = jest.spyOn(bookingsApi, 'cancel');
    await renderWithProviders(<BookingDetailsScreen />);

    await fireEvent.press(await screen.findByRole('button', { name: 'Cancel booking' }));

    expect(await screen.findByText('Bookings can be cancelled until 24 hours before departure.')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Yes, cancel booking' })).toBeNull();
    expect(cancel).not.toHaveBeenCalled();
  });

  it('shows the error when the server refuses the cancellation', async () => {
    jest.spyOn(bookingsApi, 'get').mockResolvedValue(confirmed());
    jest
      .spyOn(bookingsApi, 'cancellationQuote')
      .mockResolvedValue({ canCancel: true, refund: { amount: 0, currency: 'EUR' }, reasonCode: null, reason: null });
    jest.spyOn(bookingsApi, 'cancel').mockRejectedValue(new ApiError('Too late to cancel.', 409, 'cancellation_window_closed'));
    await renderWithProviders(<BookingDetailsScreen />);

    await fireEvent.press(await screen.findByRole('button', { name: 'Cancel booking' }));
    expect(await screen.findByText(/non-refundable/)).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Yes, cancel booking' }));

    expect(await screen.findByText('Too late to cancel.')).toBeOnTheScreen();
  });

  it('offers to finish paying an unpaid booking', async () => {
    jest
      .spyOn(bookingsApi, 'get')
      .mockResolvedValue(upcoming({ expiresAt: new Date(Date.now() + 10 * 60_000).toISOString() }));
    await renderWithProviders(<BookingDetailsScreen />);

    expect(await screen.findByRole('button', { name: 'Continue to payment' })).toBeOnTheScreen();
    expect(screen.getByText('Awaiting payment')).toBeOnTheScreen();
  });
});
